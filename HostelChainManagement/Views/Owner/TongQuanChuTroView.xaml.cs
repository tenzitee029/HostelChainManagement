
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Owner
{
    public partial class TongQuanChuTroView : UserControl
    {
        private readonly CultureInfo _vanHoa =
            CultureInfo.GetCultureInfo("vi-VN");

        private bool _dangTai;

        public event Action<string>? NavigateRequested;

        public TongQuanChuTroView()
        {
            InitializeComponent();

            Loaded += TongQuanChuTroView_Loaded;
        }

        private async void TongQuanChuTroView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= TongQuanChuTroView_Loaded;

            await TaiDuLieuAsync();
        }

        private async void TaiLai_Click(
            object sender,
            RoutedEventArgs e)
        {
            await TaiDuLieuAsync();
        }

        private async Task TaiDuLieuAsync()
        {
            if (_dangTai)
                return;

            _dangTai = true;
            btnTaiLai.IsEnabled = false;

            txtTrangThaiTai.Text =
                "Đang cập nhật số liệu từ SQL Server...";

            txtTrangThaiTai.Foreground =
                new SolidColorBrush(
                    Color.FromRgb(107, 114, 128));

            try
            {
                var soLieu =
                    await TongQuanChuTroService.LaySoLieuAsync();

                txtTongDayTro.Text =
                    soLieu.TongDayTro.ToString(_vanHoa);

                txtNhanVienDangLam.Text =
                    soLieu.NhanVienDangLam.ToString(_vanHoa);

                txtHopDongHieuLuc.Text =
                    soLieu.HopDongHieuLuc.ToString(_vanHoa);

                txtHoaDonConNo.Text =
                    soLieu.HoaDonConNo.ToString(_vanHoa);

                txtDoanhThuDaThu.Text =
                    DinhDangTien(soLieu.DoanhThuDaThu);

                txtTongCongNo.Text =
                    DinhDangTien(soLieu.TongCongNo);

                txtPhongChuaThanhToan.Text =
                    soLieu.PhongChuaThanhToan.ToString(_vanHoa);

                txtTrangThaiTai.Text =
                    "Dữ liệu SQL được cập nhật lúc "
                    + DateTime.Now.ToString(
                        "HH:mm dd/MM/yyyy",
                        _vanHoa)
                    + ". Hóa đơn còn nợ bao gồm cả khoản chưa quá hạn.";
            }
            catch (InvalidOperationException ex)
            {
                HienThiLoi(ex.Message);
            }
            catch (SqlException)
            {
                HienThiLoi(
                    "Không thể tải số liệu Tổng quan từ SQL Server. "
                    + "Vui lòng kiểm tra kết nối cơ sở dữ liệu.");
            }
            catch (Exception)
            {
                HienThiLoi(
                    "Đã xảy ra lỗi khi tải số liệu Tổng quan.");
            }
            finally
            {
                _dangTai = false;
                btnTaiLai.IsEnabled = true;
            }
        }

        private string DinhDangTien(decimal soTien)
        {
            return soTien.ToString("N0", _vanHoa) + " VNĐ";
        }

        private void HienThiLoi(string noiDung)
        {
            txtTongDayTro.Text = "—";
            txtNhanVienDangLam.Text = "—";
            txtHopDongHieuLuc.Text = "—";
            txtHoaDonConNo.Text = "—";

            txtDoanhThuDaThu.Text = "— VNĐ";
            txtTongCongNo.Text = "— VNĐ";
            txtPhongChuaThanhToan.Text = "—";

            txtTrangThaiTai.Text = noiDung;

            txtTrangThaiTai.Foreground =
                new SolidColorBrush(
                    Color.FromRgb(220, 38, 38));
        }

        private void Navigate_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button
                || button.Tag is not string destination)
            {
                return;
            }

            if (destination != "DayTro"
                && destination != "NhanVien"
                && destination != "PhanCong"
                && destination != "HopDong"
                && destination != "BaoCao")
            {
                return;
            }

            NavigateRequested?.Invoke(destination);
        }
    }
}
