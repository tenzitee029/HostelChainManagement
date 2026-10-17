using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Technician;

namespace QuanLyChuoiNhaTro.Views.Technician
{
    public partial class ThongBaoKyThuatView : UserControl
    {
        private bool _loading;

        public bool IsBusy => _loading;

        public ThongBaoKyThuatView()
        {
            InitializeComponent();
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private async void Notifications_Click(
            object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_loading)
                return;

            _loading = true;
            editorPanel.IsEnabled = false;
            txtStatus.Text = "Đang tải thông báo...";

            var session = UserSession.CurrentUser;

            try
            {
                var table =
                    await ThongBaoKyThuatService.GetNotificationsAsync();

                if (!IsLoaded
                    || !ReferenceEquals(
                        session, UserSession.CurrentUser))
                {
                    return;
                }

                notificationsGrid.ItemsSource = table.DefaultView;
                txtNotification.Clear();

                txtStatus.Text = table.Rows.Count == 0
                    ? "Bạn chưa có thông báo."
                    : "";
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không tải được thông báo. "
                    + "Vui lòng kiểm tra SQL Server.";
            }
            catch (Exception ex)
            {
                txtStatus.Text = ex.Message;
            }
            finally
            {
                _loading = false;
                editorPanel.IsEnabled = true;
            }
        }

        private async void Notification_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            if (notificationsGrid.SelectedItem
                is not DataRowView row)
            {
                return;
            }

            txtNotification.Text =
                row["TieuDe"] + "\n\n" + row["NoiDung"];

            if ((bool)row["DaDoc"])
                return;

            try
            {
                await ThongBaoKyThuatService.MarkReadAsync(
                    row["MaThongBao"].ToString()!);

                row["DaDoc"] = true;
            }
            catch (Exception ex)
            {
                txtStatus.Text =
                    "Không đánh dấu đã đọc: " + ex.Message;
            }
        }
    }
}