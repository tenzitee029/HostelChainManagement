using System;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Tenant;

namespace QuanLyChuoiNhaTro.Views.Tenant
{
    public partial class HopDongThueView : UserControl
    {
        private bool _loading;

        public HopDongThueView()
        {
            InitializeComponent();
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_loading)
                return;

            _loading = true;

            cboHopDong.IsEnabled = false;
            cboHopDong.ItemsSource = null;
            contractDetails.DataContext = null;
            contractDetails.Visibility = Visibility.Collapsed;

            txtStatus.Text = "Đang tải hợp đồng...";

            try
            {
                var table =
                    await HopDongThueService.GetHopDongCuaToiAsync();

                if (!IsLoaded)
                    return;

                if (table.Rows.Count == 0)
                {
                    txtStatus.Text =
                        "Tài khoản chưa có hợp đồng thuê.";
                    return;
                }

                cboHopDong.ItemsSource = table.DefaultView;
                cboHopDong.IsEnabled = true;
                cboHopDong.SelectedIndex = 0;

                contractDetails.Visibility = Visibility.Visible;
                txtStatus.Text = "";
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không tải được hợp đồng. " +
                    "Vui lòng kiểm tra kết nối SQL Server.";
            }
            catch (Exception ex)
            {
                txtStatus.Text = ex.Message;
            }
            finally
            {
                _loading = false;
                UpdateDownloadButton();
            }
        }

        private async void DownloadPdf_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_loading || _downloading)
                return;

            if (cboHopDong.SelectedItem is not DataRowView row)
            {
                txtStatus.Text = "Vui lòng chọn hợp đồng cần tải.";
                return;
            }

            string maHopDong =
                Convert.ToString(row["MaHopDong"]) ?? "";

            var session = UserSession.CurrentUser;

            _downloading = true;

            cboHopDong.IsEnabled = false;
            UpdateDownloadButton();

            try
            {
                // Hỏi vị trí lưu trước khi tạo PDF.
                var dialog = new SaveFileDialog
                {
                    Title = "Lưu bản PDF hợp đồng thuê",

                    FileName = CreateDefaultFileName(maHopDong),

                    Filter = "Tệp PDF (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    AddExtension = true,

                    OverwritePrompt = true,
                    CheckPathExists = true
                };

                Window? owner = Window.GetWindow(this);

                bool? accepted = owner != null
                    ? dialog.ShowDialog(owner)
                    : dialog.ShowDialog();

                if (accepted != true)
                {
                    txtStatus.Text = "Đã hủy tải bản PDF.";
                    return;
                }

                if (!IsLoaded
                    || !ReferenceEquals(
                        session,
                        UserSession.CurrentUser))
                {
                    return;
                }

                txtStatus.Text = "Đang tạo bản PDF...";

                // Tạo từ dữ liệu hợp đồng, không đọc FilePdf.
                byte[] pdf =
                    await HopDongPdfService.TaoPdfAsync(maHopDong);

                if (!IsLoaded
                    || !ReferenceEquals(
                        session,
                        UserSession.CurrentUser))
                {
                    return;
                }

                await File.WriteAllBytesAsync(
                    dialog.FileName,
                    pdf);

                if (!IsLoaded
                    || !ReferenceEquals(
                        session,
                        UserSession.CurrentUser))
                {
                    return;
                }

                txtStatus.Text =
                    "Đã lưu bản PDF tại: " + dialog.FileName;

                MessageBox.Show(
                    Window.GetWindow(this),
                    "Đã lưu bản PDF thành công.\n\n"
                        + dialog.FileName,
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (UnauthorizedAccessException)
            {
                ShowPdfError(
                    "Không có quyền lưu vào vị trí này. "
                    + "Vui lòng chọn thư mục khác.");
            }
            catch (IOException)
            {
                ShowPdfError(
                    "Không lưu được file. File có thể đang được mở "
                    + "hoặc thư mục lưu không còn truy cập được.");
            }
            catch (SqlException)
            {
                ShowPdfError(
                    "Không lấy được thông tin hợp đồng. "
                    + "Vui lòng kiểm tra kết nối SQL Server.");
            }
            catch (Exception ex)
            {
                ShowPdfError(
                    "Không xuất được PDF: " + ex.Message);
            }
            finally
            {
                _downloading = false;

                cboHopDong.IsEnabled =
                    cboHopDong.Items.Count > 0;

                UpdateDownloadButton();
            }
        }

        private void ShowPdfError(string message)
        {
            if (!IsLoaded)
                return;

            txtStatus.Text = message;

            MessageBox.Show(
                Window.GetWindow(this),
                message,
                "Tải PDF",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private bool _downloading;

        private void UpdateDownloadButton()
        {
            btnDownloadPdf.IsEnabled =
                !_loading
                && !_downloading
                && cboHopDong.SelectedItem is DataRowView;
        }

        private static string CreateDefaultFileName(
            string maHopDong)
        {
            string name = "HopDong_" + maHopDong;

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalidChar, '_');
            }

            return name + ".pdf";
        }

        private void Contract_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            contractDetails.DataContext =
                cboHopDong.SelectedItem as DataRowView;

            UpdateDownloadButton();
        }
    }
}