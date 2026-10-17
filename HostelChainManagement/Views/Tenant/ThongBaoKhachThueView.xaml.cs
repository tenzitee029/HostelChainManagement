using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Tenant;

namespace QuanLyChuoiNhaTro.Views.Tenant;

public partial class ThongBaoKhachThueView : UserControl
{
    private DataTable? _notifications;
    private bool _busy;
    private string? _selectedId;

    // Giữ tương thích với số chưa đọc trên nút chuông.
    public event Action<int>? UnreadCountChanged;

    public ThongBaoKhachThueView()
    {
        InitializeComponent();
    }

    private void SetBusy(bool value)
    {
        _busy = value;
        IsEnabled = !value;
    }

    private void UpdateUnreadCount()
    {
        if (_notifications == null)
            return;

        int count = _notifications.AsEnumerable()
            .Count(row => !row.Field<bool>("DaDoc"));

        UnreadCountChanged?.Invoke(count);
    }

    private void DisplayNotification(DataRowView? row)
    {
        if (row == null)
        {
            _selectedId = null;

            txtTieuDe.Text = "Chọn một thông báo";
            txtThongTin.Text = "";
            txtNotification.Clear();

            return;
        }

        _selectedId =
            Convert.ToString(row["MaThongBao"]) ?? "";

        txtTieuDe.Text =
            Convert.ToString(row["TieuDe"]) ?? "";

        txtNotification.Text =
            Convert.ToString(row["NoiDung"]) ?? "";

        string nguoiGui =
            Convert.ToString(row["NguoiGui"]) ?? "";

        string ngayGui = row["NgayGui"] == DBNull.Value
            ? ""
            : Convert.ToDateTime(row["NgayGui"])
                .ToString("dd/MM/yyyy HH:mm");

        txtThongTin.Text =
            $"Người gửi: {nguoiGui}\nNgày gửi: {ngayGui}";
    }

    private async void View_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void Refresh_Click(
        object sender,
        RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    // Timer trong KhachThueWindow vẫn có thể gọi hàm này.
    public async Task RefreshAsync(bool automatic = false)
    {
        if (_busy)
            return;

        SetBusy(true);

        var session = UserSession.CurrentUser;
        string? selectedId = _selectedId;

        if (!automatic)
            txtStatus.Text = "Đang tải thông báo...";

        try
        {
            var table =
                await ThongBaoKhachThueService.GetAsync();

            if (!IsLoaded
                || !ReferenceEquals(
                    session,
                    UserSession.CurrentUser))
            {
                return;
            }

            _notifications = table;

            notificationsGrid.ItemsSource =
                table.DefaultView;

            var selectedRow = table.DefaultView
                .Cast<DataRowView>()
                .FirstOrDefault(
                    row => (string)row["MaThongBao"] == selectedId);

            // Không tự đánh dấu các thông báo khác là đã đọc.
            // Sự kiện chọn dòng được bỏ qua trong lúc _busy.
            notificationsGrid.SelectedItem = selectedRow;

            DisplayNotification(selectedRow);

            UpdateUnreadCount();

            UpdateStatusSummary();
        }
        catch (SqlException)
        {
            if (!automatic)
            {
                txtStatus.Text =
                    "Không tải được thông báo. " +
                    "Vui lòng kiểm tra kết nối SQL Server.";
            }
        }
        catch (Exception ex)
        {
            if (!automatic)
                txtStatus.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void UpdateStatusSummary()
    {
        if (_notifications == null)
        {
            txtStatus.Text = "";
            return;
        }

        int unread = _notifications.AsEnumerable()
            .Count(row => !row.Field<bool>("DaDoc"));

        txtStatus.Text = _notifications.Rows.Count == 0
            ? "Bạn chưa có thông báo."
            : $"{_notifications.Rows.Count} thông báo — "
              + $"{unread} chưa đọc.";
    }

    private async void Notification_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_busy)
            return;

        var row =
            notificationsGrid.SelectedItem as DataRowView;

        DisplayNotification(row);

        if (row == null || (bool)row["DaDoc"])
            return;

        var session = UserSession.CurrentUser;

        SetBusy(true);

        try
        {
            DateTime readAt =
                await ThongBaoKhachThueService.MarkReadAsync(
                    (string)row["MaThongBao"]);

            if (!IsLoaded
                || !ReferenceEquals(
                    session,
                    UserSession.CurrentUser))
            {
                return;
            }

            row["DaDoc"] = true;
            row["NgayDoc"] = readAt;

            UpdateUnreadCount();

            UpdateStatusSummary();
        }
        catch (SqlException)
        {
            txtStatus.Text =
                "Không đánh dấu được đã đọc. " +
                "Vui lòng tải lại và thử lại.";
        }
        catch (Exception ex)
        {
            txtStatus.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }
}