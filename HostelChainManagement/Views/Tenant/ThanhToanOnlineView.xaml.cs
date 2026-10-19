using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Tenant;

namespace QuanLyChuoiNhaTro.Views.Tenant
{
    public partial class ThanhToanOnlineView : UserControl
    {
        private readonly string _maHoaDon;

        private bool _loading;

        public event Action? BackRequested;

        public ThanhToanOnlineView(string maHoaDon)
        {
            InitializeComponent();

            _maHoaDon = maHoaDon;
        }

        private async void View_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_loading)
                return;

            _loading = true;

            var session = UserSession.CurrentUser;

            ResetDisplay();

            try
            {
                // Service kiểm tra hóa đơn thuộc
                // khách thuê đang đăng nhập.
                var data =
                    await HoaDonKhachThueService.GetChiTietAsync(
                        _maHoaDon);

                if (!IsLoaded)
                    return;

                if (!ReferenceEquals(
                    session,
                    UserSession.CurrentUser))
                {
                    ShowError(
                        "Phiên đăng nhập đã thay đổi.",
                        "Vui lòng quay lại và đăng nhập lại.");

                    return;
                }

                var row = data.ThongTin;

                decimal conPhaiTra =
                    Convert.ToDecimal(row["ConPhaiTra"]);

                string trangThai =
                    Convert.ToString(row["TrangThai"]) ?? "";

                txtInvoice.Text =
                    $"Hóa đơn {_maHoaDon} • " +
                    $"Phòng {row["SoPhong"]} • " +
                    $"{row["Thang"]}/{row["Nam"]}";

                txtAmount.Text =
                    conPhaiTra.ToString(
                        "N0",
                        CultureInfo.GetCultureInfo("vi-VN"))
                    + " VNĐ";

                if (conPhaiTra <= 0
                    || trangThai != "Chưa thanh toán")
                {
                    txtQrMissing.Text =
                        "Hóa đơn không còn chờ thanh toán.";

                    txtStatus.Text =
                        "Không cần chuyển tiền cho hóa đơn này.";

                    return;
                }

                // Thông tin nhận tiền chỉ hiển thị.
                txtAccountName.Text =
                    ThanhToanOnlineConfig.TenTaiKhoan;

                if (string.IsNullOrWhiteSpace(ThanhToanOnlineConfig.TenTaiKhoan))
                {
                    txtQrMissing.Text =
                        "Chưa cấu hình tài khoản MoMo.";
                    return;
                }

                string qrPath = Path.GetFullPath(
                    Path.Combine(
                        AppContext.BaseDirectory,
                        ThanhToanOnlineConfig.AnhQr));

                if (!File.Exists(qrPath))
                {
                    txtQrMissing.Text =
                        "Chưa có ảnh QR MoMo.";

                    txtStatus.Text =
                        "Không tìm thấy mã QR thanh toán. " +
                        "Vui lòng liên hệ quản lý.";

                    return;
                }

                var bitmap = new BitmapImage();

                bitmap.BeginInit();

                bitmap.CacheOption =
                    BitmapCacheOption.OnLoad;

                bitmap.UriSource = new Uri(
                    qrPath,
                    UriKind.Absolute);

                bitmap.EndInit();

                bitmap.Freeze();

                imgQr.Source = bitmap;
                imgQr.Visibility = Visibility.Visible;

                txtQrMissing.Visibility =
                    Visibility.Collapsed;

                txtStatus.Text = "";
            }
            catch (SqlException)
            {
                ShowError(
                    "Không tải được hóa đơn.",
                    "Không kết nối được SQL Server. " +
                    "Vui lòng kiểm tra kết nối và mở lại màn hình.");
            }
            catch (InvalidOperationException ex)
            {
                ShowError(
                    "Không thể thanh toán hóa đơn.",
                    ex.Message);
            }
            catch (Exception)
            {
                ShowError(
                    "Không mở được thông tin thanh toán.",
                    "Vui lòng kiểm tra cấu hình tài khoản " +
                    "và ảnh QR MoMo.");
            }
            finally
            {
                _loading = false;
            }
        }

        private void ResetDisplay()
        {
            imgQr.Source = null;
            imgQr.Visibility = Visibility.Collapsed;

            txtQrMissing.Visibility = Visibility.Visible;
            txtQrMissing.Text = "Đang tải mã QR MoMo...";

            txtAccountName.Text = "";
            txtAmount.Text = "Đang tải...";
            txtInvoice.Text = "";

            txtStatus.Text =
                "Đang tải thông tin thanh toán...";
        }

        private void ShowError(
            string qrMessage,
            string statusMessage)
        {
            imgQr.Source = null;
            imgQr.Visibility = Visibility.Collapsed;

            txtQrMissing.Visibility = Visibility.Visible;
            txtQrMissing.Text = qrMessage;

            if (txtAmount.Text == "Đang tải...")
            {
                txtAmount.Text = "Không tải được";
            }

            txtStatus.Text = statusMessage;
        }

        private void Back_Click(
            object sender,
            RoutedEventArgs e)
        {
            BackRequested?.Invoke();
        }
    }
}