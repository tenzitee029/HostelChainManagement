using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Management;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class XacNhanThanhToanView : UserControl
    {
        private readonly string _maHoaDon;

        private decimal _tongTien;
        private decimal _conNo;

        private bool _dangTai;
        private bool _dangLuu;
        private bool _sanSang;

        public event Action? QuayLai;
        public event Action? DaLuu;

        public XacNhanThanhToanView(string maHoaDon)
        {
            InitializeComponent();

            _maHoaDon = maHoaDon;
            txtMaHoaDon.Text = maHoaDon;

            DatTrangThai();
        }

        private static string Tien(decimal soTien)
        {
            return soTien.ToString(
                "N0", CultureInfo.GetCultureInfo("vi-VN")) + " VNĐ";
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai || _sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text =
                    "Bạn không có quyền xác nhận thanh toán.";
                return;
            }

            _dangTai = true;
            DatTrangThai();
            txtThongBao.Text = "Đang tải hóa đơn...";

            try
            {
                var thongTin =
                    await XacNhanThanhToanService.LayThongTinAsync(
                        _maHoaDon,
                        user.MaNguoiDung);

                if (thongTin == null)
                {
                    txtThongBao.Text =
                        "Hóa đơn không tồn tại hoặc bạn không có quyền quản lý.";
                    return;
                }

                if (!IsLoaded)
                    return;

                _tongTien = Convert.ToDecimal(thongTin.TongTien);
                decimal daTra = Convert.ToDecimal(thongTin.DaTra);

                _conNo = Math.Max(0, _tongTien - daTra);

                txtTenKhach.Text = Convert.ToString(thongTin.HoTen);
                txtTongTien.Text = Tien(_tongTien);
                txtConNo.Text = Tien(_conNo);

                txtTrangThai.Text = _conNo == 0
                    ? "Đã thanh toán"
                    : daTra > 0
                        ? "Còn nợ"
                        : "Chưa thanh toán";

                if (_conNo == 0)
                {
                    txtThongBao.Text =
                        "Hóa đơn đã được thanh toán hết.";
                    return;
                }

                txtSoTien.Text = _conNo.ToString(
                    "0", CultureInfo.InvariantCulture);

                _sanSang = true;
                txtThongBao.Text = "";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không tải được hóa đơn: " + ex.Message;
            }
            finally
            {
                _dangTai = false;
                DatTrangThai();
            }
        }

        private async void Luu_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangLuu || !_sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text =
                    "Bạn không có quyền xác nhận thanh toán.";
                return;
            }

            if (!decimal.TryParse(
                    txtSoTien.Text.Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out decimal soTien)
                || soTien <= 0
                || soTien > _conNo)
            {
                txtThongBao.Text =
                    "Nhập số tiền nguyên dương, không vượt quá "
                    + Tien(_conNo) + ".";
                return;
            }

            string phuongThuc = rbTienMat.IsChecked == true
                ? "Tiền mặt"
                : rbChuyenKhoan.IsChecked == true
                    ? "Chuyển khoản"
                    : "";

            if (phuongThuc.Length == 0)
            {
                txtThongBao.Text =
                    "Vui lòng chọn phương thức thanh toán.";
                return;
            }

            var xacNhan = MessageBox.Show(
                $"Xác nhận đã nhận {Tien(soTien)}?\n"
                + $"Hóa đơn: {_maHoaDon}\n"
                + $"Phương thức: {phuongThuc}",
                "Xác nhận tiền đã nhận",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (xacNhan != MessageBoxResult.Yes)
                return;

            _dangLuu = true;
            DatTrangThai();
            txtThongBao.Text = "Đang ghi nhận thanh toán...";

            bool thanhCong = false;
            decimal noSauThanhToan = _conNo - soTien;

            try
            {
                await XacNhanThanhToanService.LuuAsync(
                    _maHoaDon,
                    user.MaNguoiDung,
                    phuongThuc,
                    soTien,
                    _tongTien,
                    _conNo);

                thanhCong = true;
                _sanSang = false;
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                txtThongBao.Text =
                    "Dữ liệu đang được cập nhật. Vui lòng thử lại.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không ghi nhận được thanh toán: " + ex.Message;
            }
            finally
            {
                _dangLuu = false;
                DatTrangThai();
            }

            if (thanhCong)
            {
                MessageBox.Show(
                    "Ghi nhận thanh toán thành công.\n"
                    + (noSauThanhToan == 0
                        ? "Hóa đơn đã thanh toán hết."
                        : "Còn nợ: " + Tien(noSauThanhToan)),
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DaLuu?.Invoke();
            }
        }

        private void DatTrangThai()
        {
            bool choPhep =
                _sanSang && !_dangTai && !_dangLuu;

            pnlThanhToan.IsEnabled = choPhep;
            btnLuu.IsEnabled = choPhep;
            btnQuayLai.IsEnabled = !_dangLuu;

            btnLuu.Content = _dangLuu
                ? "Đang lưu..."
                : "Xác nhận hoàn tất";
        }

        private void QuayLai_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_dangLuu)
                QuayLai?.Invoke();
        }
    }
}