
using System;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Owner
{
    public partial class ChiTietHoaDonView : UserControl
    {
        private readonly string _maHoaDon;

        public event Action? QuayLaiRequested;

        public ChiTietHoaDonView(string maHoaDon)
        {
            InitializeComponent();

            _maHoaDon = maHoaDon;

            Loaded += ChiTietHoaDonView_Loaded;
        }

        private async void ChiTietHoaDonView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= ChiTietHoaDonView_Loaded;

            await TaiChiTietAsync();
        }

        private async Task TaiChiTietAsync()
        {
            txtThongBao.Text = "Đang tải chi tiết hóa đơn...";
            txtThongBao.Visibility = Visibility.Visible;

            dgvKhoanThu.ItemsSource = null;

            txtEmptyKhoanThu.Text = "Đang tải các khoản thu...";
            txtEmptyKhoanThu.Visibility = Visibility.Visible;

            try
            {
                var ketQua =
                    await ChiTietHoaDonChuTroService
                        .LayChiTietAsync(_maHoaDon);

                if (ketQua.ThongTin.Rows.Count == 0)
                {
                    HienThiLoi(
                        "Không tìm thấy hóa đơn hoặc hóa đơn "
                        + "không thuộc quyền quản lý của bạn.");

                    txtEmptyKhoanThu.Text =
                        "Không có dữ liệu hóa đơn.";

                    return;
                }

                DataRow hoaDon = ketQua.ThongTin.Rows[0];

                txtMaHoaDon.Text =
                    LayChuoi(hoaDon, "MaHoaDon");

                txtMaHopDong.Text =
                    LayChuoi(hoaDon, "MaHopDong");

                txtKhachThue.Text =
                    LayChuoi(hoaDon, "HoTenKhach");

                txtPhong.Text =
                    LayChuoi(hoaDon, "SoPhong");

                txtDayTro.Text =
                    LayChuoi(hoaDon, "TenDayTro");

                txtTrangThai.Text =
                    LayChuoi(hoaDon, "TrangThai");

                txtKyThanhToan.Text =
                    LayChuoi(hoaDon, "Thang")
                    + "/"
                    + LayChuoi(hoaDon, "Nam");

                txtNgayLap.Text =
                    DinhDangNgay(hoaDon, "NgayLap");

                txtHanThanhToan.Text =
                    DinhDangNgay(hoaDon, "HanThanhToan");

                txtPhuongThuc.Text =
                    LayChuoi(hoaDon, "PhuongThucThanhToan");

                txtTongTien.Text =
                    DinhDangTien(hoaDon, "TongTien");

                if (txtTrangThai.Text == "Đã thanh toán")
                {
                    txtTrangThai.Foreground =
                        TaoMau(21, 128, 61);
                }
                else if (txtTrangThai.Text == "Còn nợ")
                {
                    txtTrangThai.Foreground =
                        TaoMau(220, 38, 38);
                }
                else
                {
                    txtTrangThai.Foreground =
                        TaoMau(180, 83, 9);
                }

                dgvKhoanThu.ItemsSource =
                    ketQua.CacKhoanThu.DefaultView;

                if (ketQua.CacKhoanThu.Rows.Count == 0)
                {
                    txtEmptyKhoanThu.Text =
                        "Hóa đơn chưa có chi tiết khoản thu.";

                    txtEmptyKhoanThu.Visibility =
                        Visibility.Visible;
                }
                else
                {
                    txtEmptyKhoanThu.Visibility =
                        Visibility.Collapsed;
                }

                txtThongBao.Visibility = Visibility.Collapsed;
            }
            catch (InvalidOperationException ex)
            {
                HienThiLoi(ex.Message);
            }
            catch (SqlException)
            {
                HienThiLoi(
                    "Không thể tải chi tiết hóa đơn từ SQL Server. "
                    + "Vui lòng kiểm tra kết nối.");
            }
            catch (Exception)
            {
                HienThiLoi(
                    "Đã xảy ra lỗi khi tải chi tiết hóa đơn.");
            }
        }

        private static string LayChuoi(
            DataRow row,
            string tenCot)
        {
            if (!row.Table.Columns.Contains(tenCot))
                return "—";

            if (row[tenCot] == DBNull.Value)
                return "—";

            string giaTri =
                Convert.ToString(row[tenCot]) ?? "";

            return string.IsNullOrWhiteSpace(giaTri)
                ? "—"
                : giaTri;
        }

        private static string DinhDangNgay(
            DataRow row,
            string tenCot)
        {
            if (row[tenCot] == DBNull.Value)
                return "—";

            DateTime ngay =
                Convert.ToDateTime(row[tenCot]);

            return ngay.ToString("dd/MM/yyyy");
        }

        private static string DinhDangTien(
            DataRow row,
            string tenCot)
        {
            if (row[tenCot] == DBNull.Value)
                return "—";

            decimal soTien =
                Convert.ToDecimal(row[tenCot]);

            return soTien.ToString(
                "N0",
                CultureInfo.GetCultureInfo("vi-VN")) + " đ";
        }

        private static SolidColorBrush TaoMau(
            byte r,
            byte g,
            byte b)
        {
            return new SolidColorBrush(
                Color.FromRgb(r, g, b));
        }

        private void HienThiLoi(string noiDung)
        {
            txtThongBao.Text = noiDung;
            txtThongBao.Foreground =
                TaoMau(220, 38, 38);

            txtThongBao.Visibility = Visibility.Visible;
        }

        private void QuayLai_Click(
            object sender,
            RoutedEventArgs e)
        {
            QuayLaiRequested?.Invoke();
        }
    }
}
