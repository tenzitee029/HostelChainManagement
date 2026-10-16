using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Management;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class GiaHanHopDongView : UserControl
    {
        private readonly string _maHopDong;
        private string _maPhong = "";
        private string _maKhach = "";
        private DateTime _ngayKetThucCu;

        private bool _dangTai;
        private bool _dangLuu;
        private bool _sanSang;

        public event Action? QuayLai;
        public event Action? DaLuu;

        public GiaHanHopDongView(string maHopDong)
        {
            InitializeComponent();

            _maHopDong = maHopDong;
            txtMaHopDong.Text = maHopDong;
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai || _sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền gia hạn hợp đồng.";
                return;
            }

            _dangTai = true;
            DatTrangThai();
            txtThongBao.Text = "Đang tải hợp đồng...";

            try
            {
                var thongTin =
                    await GiaHanHopDongService.LayThongTinAsync(
                        _maHopDong,
                        user.MaNguoiDung);

                if (thongTin == null)
                {
                    txtThongBao.Text =
                        "Hợp đồng không tồn tại hoặc bạn không có quyền quản lý.";
                    return;
                }

                if (!IsLoaded)
                    return;

                _maPhong = Convert.ToString(thongTin.MaPhong) ?? "";
                _maKhach = Convert.ToString(thongTin.MaKhachThue) ?? "";

                DateTime batDau =
                    Convert.ToDateTime(thongTin.NgayBatDau);

                _ngayKetThucCu =
                    Convert.ToDateTime(thongTin.NgayKetThuc).Date;

                txtTrangThai.Text =
                    Convert.ToString(thongTin.TrangThai);

                txtKhachThue.Text =
                    Convert.ToString(thongTin.HoTen);

                txtPhong.Text =
                    Convert.ToString(thongTin.TenPhong);

                txtNgayBatDau.Text =
                    batDau.ToString("dd/MM/yyyy");

                txtNgayKetThuc.Text =
                    _ngayKetThucCu.ToString("dd/MM/yyyy");

                if (Convert.ToInt32(thongTin.ChoPhepGiaHan) != 1)
                {
                    txtThongBao.Text =
                        "Chỉ gia hạn hợp đồng có trạng thái Hiệu lực "
                        + "và đang trong thời gian thuê.";
                    return;
                }

                if (_ngayKetThucCu == DateTime.MaxValue.Date)
                {
                    txtThongBao.Text =
                        "Ngày kết thúc đã đạt giới hạn, không thể gia hạn thêm.";
                    return;
                }

                dpKetThucMoi.DisplayDateStart =
                    _ngayKetThucCu.AddDays(1);

                _sanSang = true;
                txtThongBao.Text = "";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không tải được hợp đồng: " + ex.Message;
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
                txtThongBao.Text = "Bạn không có quyền gia hạn hợp đồng.";
                return;
            }

            if (!dpKetThucMoi.SelectedDate.HasValue)
            {
                txtThongBao.Text = "Vui lòng chọn ngày kết thúc mới.";
                return;
            }

            DateTime ngayMoi = dpKetThucMoi.SelectedDate.Value.Date;

            if (ngayMoi <= _ngayKetThucCu)
            {
                txtThongBao.Text =
                    "Ngày kết thúc mới phải sau ngày kết thúc hiện tại.";
                return;
            }

            var xacNhan = MessageBox.Show(
                $"Gia hạn hợp đồng {_maHopDong} đến "
                + $"{ngayMoi:dd/MM/yyyy}?",
                "Xác nhận gia hạn",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (xacNhan != MessageBoxResult.Yes)
                return;

            _dangLuu = true;
            DatTrangThai();
            txtThongBao.Text = "Đang gia hạn hợp đồng...";

            bool thanhCong = false;

            try
            {
                await GiaHanHopDongService.LuuAsync(
                    _maHopDong,
                    _maPhong,
                    _maKhach,
                    user.MaNguoiDung,
                    _ngayKetThucCu,
                    ngayMoi);

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
                txtThongBao.Text =
                    "Không gia hạn được hợp đồng: " + ex.Message;
            }
            finally
            {
                _dangLuu = false;
                DatTrangThai();
            }

            if (thanhCong)
            {
                MessageBox.Show(
                    $"Gia hạn thành công đến ngày {ngayMoi:dd/MM/yyyy}.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DaLuu?.Invoke();
            }
        }

        private void DatTrangThai()
        {
            bool choPhep = _sanSang && !_dangTai && !_dangLuu;

            dpKetThucMoi.IsEnabled = choPhep;
            btnLuu.IsEnabled = choPhep;
            btnQuayLai.IsEnabled = !_dangLuu;

            btnLuu.Content = _dangLuu
                ? "Đang lưu..."
                : "Xác nhận gia hạn";
        }

        private void QuayLai_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_dangLuu)
                QuayLai?.Invoke();
        }
    }
}