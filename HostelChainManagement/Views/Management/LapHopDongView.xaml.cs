using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Management;
using System;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class LapHopDongView : UserControl
    {
        private readonly string _maHopDong;
        private bool _dangTai;
        private bool _dangLuu;
        private bool _sanSang;

        public event Action? QuayLai;
        public event Action? DaLuu;

        public LapHopDongView()
        {
            InitializeComponent();

            _maHopDong = "HD" + Guid.NewGuid().ToString("N")
                .Substring(0, 18).ToUpperInvariant();

            txtMaHopDong.Text = _maHopDong;
            dpBatDau.SelectedDate = DateTime.Today;
            dpKetThuc.SelectedDate = DateTime.Today.AddYears(1);
            txtDaiDien.Text = UserSession.CurrentUser?.HoTen ?? "";

            DatTrangThai();
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai || _sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền lập hợp đồng.";
                return;
            }

            _dangTai = true;
            DatTrangThai();
            txtThongBao.Text = "Đang tải phòng và khách thuê...";

            try
            {
                var (phong, khach) =
                    await LapHopDongService.TaiDuLieuAsync(user.MaNguoiDung);

                if (!IsLoaded)
                    return;

                cboPhong.ItemsSource = phong.DefaultView;
                cboKhach.ItemsSource = khach.DefaultView;

                _sanSang = phong.Rows.Count > 0 && khach.Rows.Count > 0;

                txtThongBao.Text = _sanSang
                    ? ""
                    : "Cần có phòng trống và khách thuê thuộc phạm vi quản lý.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text = "Không tải được dữ liệu: " + ex.Message;
            }
            finally
            {
                _dangTai = false;
                DatTrangThai();
            }
        }

        private void Phong_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            if (txtDayTro == null || txtGiaThue == null)
                return;

            if (cboPhong.SelectedItem is DataRowView phong)
            {
                txtDayTro.Text = Convert.ToString(phong["TenDayTro"]);
                txtGiaThue.Text = Convert.ToDecimal(phong["GiaThue"])
                    .ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
            }
            else
            {
                txtDayTro.Clear();
                txtGiaThue.Clear();
            }
        }

        private async void Luu_Click(object sender, RoutedEventArgs e)
        {
            if (_dangLuu || !_sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền lập hợp đồng.";
                return;
            }

            string maPhong = cboPhong.SelectedValue?.ToString() ?? "";
            string maKhach = cboKhach.SelectedValue?.ToString() ?? "";
            string daiDien = txtDaiDien.Text.Trim();

            if (maPhong.Length == 0 || maKhach.Length == 0)
            {
                txtThongBao.Text = "Vui lòng chọn phòng và khách thuê.";
                return;
            }

            if (!dpBatDau.SelectedDate.HasValue ||
                !dpKetThuc.SelectedDate.HasValue)
            {
                txtThongBao.Text = "Vui lòng chọn đầy đủ ngày thuê.";
                return;
            }

            DateTime batDau = dpBatDau.SelectedDate.Value.Date;
            DateTime ketThuc = dpKetThuc.SelectedDate.Value.Date;

            if (ketThuc <= batDau || ketThuc < DateTime.Today)
            {
                txtThongBao.Text =
                    "Ngày kết thúc phải sau ngày bắt đầu và không trước hôm nay.";
                return;
            }

            if (!decimal.TryParse(
                    txtTienCoc.Text.Trim().Replace(',', '.'),
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out decimal tienCoc)
                || tienCoc < 0
                || tienCoc > 9999999999999999.99m
                || decimal.Round(tienCoc, 2) != tienCoc)
            {
                txtThongBao.Text =
                    "Tiền cọc phải từ 0 trở lên, tối đa 2 số thập phân. "
                    + "Không nhập dấu phân cách hàng nghìn.";
                return;
            }

            if (!int.TryParse(txtKyThanhToan.Text.Trim(), out int ky)
                || ky < 1 || ky > 31)
            {
                txtThongBao.Text = "Ngày thanh toán phải từ 1 đến 31.";
                return;
            }

            if (daiDien.Length == 0 || daiDien.Length > 100)
            {
                txtThongBao.Text =
                    "Đại diện bên cho thuê phải có từ 1 đến 100 ký tự.";
                return;
            }

            _dangLuu = true;
            DatTrangThai();
            txtThongBao.Text = "Đang tạo hợp đồng...";

            bool thanhCong = false;

            try
            {
                await LapHopDongService.LuuAsync(
                    user.MaNguoiDung, _maHopDong, maPhong, maKhach,
                    daiDien, batDau, ketThuc, tienCoc, ky,
                    () => txtDieuKhoan.Text);

                thanhCong = true;
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                txtThongBao.Text =
                    "Dữ liệu đang được người khác cập nhật. Vui lòng thử lại.";
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                txtThongBao.Text =
                    "Mã hợp đồng hoặc dữ liệu bị trùng. "
                    + "Quay lại và mở form mới.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text = "Không tạo được hợp đồng: " + ex.Message;
            }
            finally
            {
                _dangLuu = false;
                DatTrangThai();
            }

            if (thanhCong)
            {
                MessageBox.Show(
                    "Tạo hợp đồng thuê thành công.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DaLuu?.Invoke();
            }
        }

        private void DatTrangThai()
        {
            bool choPhep = _sanSang && !_dangTai && !_dangLuu;

            pnlThongTin.IsEnabled = choPhep;
            btnLuu.IsEnabled = choPhep;
            btnQuayLai.IsEnabled = !_dangLuu;
            btnLuu.Content = _dangLuu
                ? "Đang lưu..."
                : "Xác nhận tạo hợp đồng";
        }

        private void QuayLai_Click(object sender, RoutedEventArgs e)
        {
            if (!_dangLuu)
                QuayLai?.Invoke();
        }
    }
}