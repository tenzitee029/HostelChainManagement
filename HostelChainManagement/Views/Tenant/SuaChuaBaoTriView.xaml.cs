using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Tenant;

namespace QuanLyChuoiNhaTro.Views.Tenant
{
    public partial class SuaChuaBaoTriView : UserControl
    {
        private bool _busy;
        private string? _selectedFile;

        public SuaChuaBaoTriView()
        {
            InitializeComponent();
        }

        private static string GetComboText(ComboBox combo)
        {
            return (combo.SelectedItem as ComboBoxItem)?
                .Content?.ToString() ?? "";
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;

            btnRefresh.IsEnabled = !busy;

            formPanel.IsEnabled =
                !busy
                && cboHopDong.SelectedItem is DataRowView;
        }

        private async void View_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy)
                return;

            SetBusy(true);

            try
            {
                await ReloadAsync();
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void Refresh_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy)
                return;

            SetBusy(true);

            try
            {
                await ReloadAsync();
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task ReloadAsync(string? success = null)
        {
            var session = UserSession.CurrentUser;

            string? selectedContract =
                (cboHopDong.SelectedItem as DataRowView)?
                ["MaHopDong"] as string;

            cboHopDong.ItemsSource = null;
            dgSuCo.ItemsSource = null;

            contractPanel.DataContext = null;

            detailPanel.DataContext = null;
            detailPanel.Visibility = Visibility.Collapsed;

            txtStatus.Text =
                success ?? "Đang tải dữ liệu...";

            try
            {
                var data =
                    await SuCoKhachThueService.GetAsync();

                if (!IsLoaded
                    || !ReferenceEquals(
                        session,
                        UserSession.CurrentUser))
                {
                    return;
                }

                cboHopDong.ItemsSource =
                    data.HopDong.DefaultView;

                cboHopDong.SelectedValuePath = "MaHopDong";

                if (selectedContract != null)
                {
                    cboHopDong.SelectedValue =
                        selectedContract;
                }

                if (cboHopDong.SelectedIndex < 0
                    && data.HopDong.Rows.Count > 0)
                {
                    cboHopDong.SelectedIndex = 0;
                }

                dgSuCo.ItemsSource =
                    data.SuCo.DefaultView;

                txtEmpty.Visibility =
                    data.SuCo.Rows.Count == 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                txtStatus.Text = success
                    ?? (data.HopDong.Rows.Count == 0
                        ? "Bạn chưa có hợp đồng đang thuê " +
                          "còn hiệu lực để gửi yêu cầu."
                        : "");
            }
            catch (Exception ex)
            {
                string prefix = success == null
                    ? ""
                    : success
                      + "\nChưa tải lại được danh sách. ";

                txtStatus.Text = prefix
                    + (ex is SqlException
                        ? "Không tải được dữ liệu. " +
                          "Vui lòng kiểm tra SQL Server."
                        : ex.Message);
            }
        }

        private void Contract_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            contractPanel.DataContext =
                cboHopDong.SelectedItem;

            formPanel.IsEnabled =
                !_busy
                && cboHopDong.SelectedItem is DataRowView;
        }

        private void ChooseFile_Click(
            object sender,
            RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn ảnh hoặc video hiện trường",
                Filter =
                    "Ảnh/video|*.png;*.jpg;*.jpeg;*.mp4|" +
                    "Ảnh PNG/JPG|*.png;*.jpg;*.jpeg|" +
                    "Video MP4|*.mp4",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var file = new FileInfo(dialog.FileName);

                if (file.Length == 0)
                {
                    txtStatus.Text =
                        "Tệp đính kèm không có nội dung.";

                    return;
                }

                if (file.Length
                    > SuCoKhachThueService.MaxFileSize)
                {
                    txtStatus.Text =
                        "Dung lượng tệp đính kèm vượt quá 20 MB. " +
                        "Vui lòng chọn tệp dung lượng nhỏ hơn.";

                    return;
                }

                string extension =
                    file.Extension.ToLowerInvariant();

                if (extension is not (
                    ".png" or ".jpg" or ".jpeg" or ".mp4"))
                {
                    txtStatus.Text =
                        "Chỉ nhận ảnh PNG/JPG hoặc video MP4.";

                    return;
                }

                _selectedFile = dialog.FileName;

                txtFileName.Text =
                    $"{file.Name} " +
                    $"({file.Length / 1024d / 1024d:N2} MB)";

                txtStatus.Text = "";
            }
            catch (Exception)
            {
                txtStatus.Text =
                    "Không đọc được tệp. Vui lòng chọn lại.";
            }
        }

        private void RemoveFile_Click(
            object sender,
            RoutedEventArgs e)
        {
            _selectedFile = null;
            txtFileName.Text = "Chưa chọn tệp.";
        }

        private void Incident_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            // Có thể phát sinh sự kiện trong lúc tạo control.
            if (detailPanel == null
                || txtSelectedFile == null)
            {
                return;
            }

            if (dgSuCo.SelectedItem is not DataRowView row)
            {
                detailPanel.DataContext = null;
                detailPanel.Visibility = Visibility.Collapsed;

                return;
            }

            detailPanel.DataContext = row;
            detailPanel.Visibility = Visibility.Visible;

            string? path = row["TepDinhKem"] == DBNull.Value
                ? null
                : row["TepDinhKem"].ToString();

            txtSelectedFile.Text =
                string.IsNullOrWhiteSpace(path)
                    ? "Không có tệp đính kèm."
                    : Path.GetFileName(path);
        }

        private async void Send_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy
                || cboHopDong.SelectedItem
                    is not DataRowView contract)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(txtNoiDung.Text))
            {
                txtStatus.Text =
                    "Vui lòng nhập nội dung yêu cầu sửa chữa.";

                txtNoiDung.Focus();

                return;
            }

            string time = GetComboText(cboThoiGian);

            if (!string.IsNullOrWhiteSpace(txtThoiGian.Text))
            {
                time += "; " + txtThoiGian.Text.Trim();
            }

            SetBusy(true);

            txtStatus.Text = "Đang gửi yêu cầu...";

            try
            {
                string id =
                    await SuCoKhachThueService.GuiAsync(
                        (string)contract["MaHopDong"],
                        GetComboText(cboNhom),
                        txtNoiDung.Text,
                        GetComboText(cboUuTien),
                        time,
                        _selectedFile);

                if (!IsLoaded)
                    return;

                txtNoiDung.Clear();
                txtThoiGian.Clear();

                _selectedFile = null;
                txtFileName.Text = "Chưa chọn tệp.";

                cboNhom.SelectedIndex = 0;
                cboUuTien.SelectedIndex = 0;
                cboThoiGian.SelectedIndex = 0;

                await ReloadAsync(
                    "Gửi yêu cầu sửa chữa thành công. " +
                    "Ban quản lý sẽ tiếp nhận trong thời gian sớm nhất." +
                    "\nMã sự cố: " + id);
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không xác nhận được kết quả gửi yêu cầu. " +
                    "Vui lòng bấm Làm mới để kiểm tra danh sách " +
                    "trước khi gửi lại.";
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
}