using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Management;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class ThanhLyHopDongView : UserControl
    {
        private readonly string _maHopDong;
        private string _maPhong = "";
        private string _maKhach = "";

        private decimal _tienCoc;
        private decimal _tienNo;
        private bool _dangTai;
        private bool _dangLuu;
        private bool _sanSang;

        public event Action? QuayLai;
        public event Action? DaLuu;

        public ThanhLyHopDongView(string maHopDong)
        {
            InitializeComponent();

            _maHopDong = maHopDong;
            txtMaHopDong.Text = maHopDong;
        }

        private static string DinhDangTien(decimal tien)
        {
            return tien.ToString(
                "N0", CultureInfo.GetCultureInfo("vi-VN")) + " VNĐ";
        }

        private bool DocKhauTru(out decimal soTien)
        {
            return decimal.TryParse(
                       txtKhauTru.Text.Trim(),
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out soTien)
                   && soTien >= 0
                   && soTien <= _tienCoc;
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai || _sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền thanh lý hợp đồng.";
                return;
            }

            _dangTai = true;
            DatTrangThai();
            txtThongBao.Text = "Đang tải thông tin thanh lý...";

            try
            {
                using var service = new ThanhLyHopDongService();
                await service.MoKetNoiAsync();

                var thongTin = await service.LayThongTinAsync(
                    _maHopDong,
                    user.MaNguoiDung);

                if (thongTin == null)
                {
                    txtThongBao.Text =
                        "Hợp đồng không tồn tại hoặc bạn không có quyền.";
                    return;
                }

                if (!IsLoaded)
                    return;

                _maPhong = Convert.ToString(thongTin.MaPhong) ?? "";
                _maKhach = Convert.ToString(thongTin.MaKhachThue) ?? "";
                _tienCoc = Convert.ToDecimal(thongTin.TienCoc);

                string trangThai =
                    Convert.ToString(thongTin.TrangThai) ?? "";

                txtKhach.Text = Convert.ToString(thongTin.HoTen);
                txtPhong.Text = Convert.ToString(thongTin.TenPhong);
                txtTrangThai.Text = trangThai;
                txtTienCoc.Text = DinhDangTien(_tienCoc);

                if (trangThai != "Hiệu lực" && trangThai != "Hết hạn")
                {
                    txtThongBao.Text = "Hợp đồng đã thanh lý.";
                    return;
                }

                if (Convert.ToDateTime(thongTin.NgayBatDau).Date >
                    Convert.ToDateTime(thongTin.HomNay).Date)
                {
                    txtThongBao.Text =
                        "Hợp đồng chưa bắt đầu. Cần xử lý hủy hợp đồng riêng.";
                    return;
                }

                _tienNo = await service.LayTienNoAsync(_maHopDong);
                txtNo.Text = DinhDangTien(_tienNo);

                var table = await service.LayThietBiAsync(_maPhong);
                dgThietBi.ItemsSource = table.DefaultView;

                _sanSang = true;
                TinhHoanCoc();

                txtThongBao.Text = _tienNo > 0
                    ? "Hợp đồng còn nợ hóa đơn. Thanh toán xong rồi mở lại form."
                    : "";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không tải được thông tin: " + ex.Message;
            }
            finally
            {
                _dangTai = false;
                DatTrangThai();
            }
        }

        private void KhauTru_Changed(
            object sender, TextChangedEventArgs e)
        {
            if (txtHoanCoc == null || !_sanSang)
                return;

            TinhHoanCoc();
            DatTrangThai();
        }

        private void TinhHoanCoc()
        {
            txtHoanCoc.Text = DocKhauTru(out decimal khauTru)
                ? DinhDangTien(_tienCoc - khauTru)
                : "Khấu trừ phải từ 0 đến tiền cọc";
        }

        private async void Luu_Click(object sender, RoutedEventArgs e)
        {
            if (_dangLuu || !_sanSang || _tienNo > 0)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền thanh lý.";
                return;
            }

            if (!DocKhauTru(out decimal khauTru))
            {
                txtThongBao.Text =
                    "Khấu trừ phải là số nguyên từ 0 đến tiền cọc.";
                return;
            }

            string ghiChu = txtGhiChu.Text.Trim();

            if (ghiChu.Length > 500)
            {
                txtThongBao.Text = "Ghi chú tối đa 500 ký tự.";
                return;
            }

            if (khauTru > 0 && ghiChu.Length == 0)
            {
                txtThongBao.Text = "Vui lòng ghi rõ lý do khấu trừ tiền cọc.";
                return;
            }

            var xacNhan = MessageBox.Show(
                $"Thanh lý hợp đồng {_maHopDong}?\n"
                + $"Khấu trừ: {DinhDangTien(khauTru)}\n"
                + $"Tiền cần hoàn: {DinhDangTien(_tienCoc - khauTru)}",
                "Xác nhận thanh lý",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (xacNhan != MessageBoxResult.Yes)
                return;

            _dangLuu = true;
            DatTrangThai();
            txtThongBao.Text = "Đang thanh lý hợp đồng...";

            bool thanhCong = false;

            try
            {
                using var service = new ThanhLyHopDongService();
                await service.MoKetNoiAsync();

                await service.LuuAsync(
                    _maHopDong,
                    _maPhong,
                    _maKhach,
                    user.MaNguoiDung,
                    _tienCoc,
                    khauTru,
                    ghiChu);

                thanhCong = true;
                _sanSang = false;
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                txtThongBao.Text =
                    "Dữ liệu đang được người khác cập nhật. Vui lòng thử lại.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text = "Không thanh lý được: " + ex.Message;
            }
            finally
            {
                _dangLuu = false;
                DatTrangThai();
            }

            if (thanhCong)
            {
                MessageBox.Show(
                    "Thanh lý hợp đồng thành công.\n"
                    + "Tiền cọc cần hoàn: "
                    + DinhDangTien(_tienCoc - khauTru),
                    "Thông báo");

                DaLuu?.Invoke();
            }
        }

        private void DatTrangThai()
        {
            bool choPhep = _sanSang && !_dangTai && !_dangLuu;

            txtKhauTru.IsEnabled = choPhep;
            txtGhiChu.IsEnabled = choPhep;
            btnQuayLai.IsEnabled = !_dangLuu;

            btnLuu.IsEnabled =
                choPhep && _tienNo == 0 && DocKhauTru(out _);

            btnLuu.Content = _dangLuu
                ? "Đang lưu..."
                : "Xác nhận thanh lý";
        }

        private void QuayLai_Click(object sender, RoutedEventArgs e)
        {
            if (!_dangLuu)
                QuayLai?.Invoke();
        }
    }
}