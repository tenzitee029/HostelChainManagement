using System;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Management;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class ThemPhongControl : UserControl
    {
        public event EventHandler? LuuThanhCong;
        public event EventHandler? YeuCauHuy;

        private bool _dangLuu;
        private bool _sanSang;
        private bool _phongDangThue;

        private readonly bool _laCheDoSua;
        private string _maPhong;
        private string _maDayTroCu = "";

        // Thêm phòng mới.
        public ThemPhongControl()
        {
            InitializeComponent();

            _maPhong = TaoMaPhong();

            txtTieuDe.Text = "Thêm phòng";
            pnlThongTin.IsEnabled = false;

            Loaded += Control_Loaded;
        }

        // Sửa phòng được chọn trong danh sách.
        public ThemPhongControl(DataRowView phong) : this()
        {
            _laCheDoSua = true;
            _maPhong = Convert.ToString(phong["MaPhong"]) ?? "";

            txtTieuDe.Text = "Cập nhật phòng";
            txtMoTa.Text = "Chỉnh sửa thông tin phòng đã chọn.";

            // Giữ nguyên số phòng và dãy trọ khi sửa.
            txtSoPhong.IsReadOnly = true;
            cboDayTro.IsEnabled = false;

            txtGhiChu.Text =
                "Khi cập nhật, số phòng và dãy trọ được giữ nguyên. "
                + "Phòng đang thuê giữ nguyên trạng thái. "
                + "Giá thuê trong hợp đồng đã ký không tự thay đổi.";
        }

        private static string TaoMaPhong()
        {
            // Tổng cộng 20 ký tự, phù hợp VARCHAR(20).
            return "P" + Guid.NewGuid().ToString("N")
                .Substring(0, 19).ToUpperInvariant();
        }

        private async void Control_Loaded(
            object sender, RoutedEventArgs e)
        {
            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtStatus.Text = "Bạn không có quyền quản lý phòng.";
                return;
            }

            txtStatus.Text = "Đang tải thông tin...";

            try
            {
                using var service = new ThemPhongService();
                await service.MoKetNoiAsync();

                var danhSachDay =
                    await service.LayDanhSachDayAsync(
                        user.MaNguoiDung);

                if (!IsVisible)
                    return;

                cboDayTro.ItemsSource = danhSachDay.DefaultView;

                if (danhSachDay.Rows.Count == 0)
                {
                    txtStatus.Text =
                        "Tài khoản chưa được phân công quản lý dãy trọ.";
                    return;
                }

                if (_laCheDoSua)
                {
                    var thongTin = await service.LayPhongAsync(
                        _maPhong,
                        user.MaNguoiDung);

                    if (thongTin == null)
                    {
                        txtStatus.Text =
                            "Phòng không còn tồn tại hoặc bạn không có quyền sửa.";
                        return;
                    }

                    if (!IsVisible)
                        return;

                    _maDayTroCu =
                        Convert.ToString(thongTin.MaDayTro) ?? "";

                    cboDayTro.SelectedValue = _maDayTroCu;

                    txtSoPhong.Text =
                        Convert.ToString(thongTin.SoPhong) ?? "";

                    cboLoaiPhong.Text =
                        Convert.ToString(thongTin.LoaiPhong) ?? "";

                    txtDienTich.Text =
                        Convert.ToDecimal(thongTin.DienTich)
                        .ToString("0.##", CultureInfo.InvariantCulture);

                    txtGiaThue.Text =
                        Convert.ToDecimal(thongTin.GiaThue)
                        .ToString("0.##", CultureInfo.InvariantCulture);

                    string trangThai =
                        Convert.ToString(thongTin.TrangThai) ?? "";

                    _phongDangThue = trangThai == "Đang thuê";

                    if (_phongDangThue)
                    {
                        cboTrangThai.Items.Add(
                            new ComboBoxItem { Content = "Đang thuê" });
                    }

                    cboTrangThai.SelectedIndex = -1;

                    foreach (ComboBoxItem item in cboTrangThai.Items)
                    {
                        if (item.Content?.ToString() == trangThai)
                        {
                            cboTrangThai.SelectedItem = item;
                            break;
                        }
                    }

                    cboTrangThai.IsEnabled = !_phongDangThue;
                }
                else
                {
                    cboDayTro.SelectedIndex = 0;
                }

                _sanSang = cboDayTro.SelectedValue != null;
                DatTrangThaiLuu(false);

                txtStatus.Text = _sanSang
                    ? ""
                    : "Không xác định được dãy trọ của phòng.";

                if (_laCheDoSua)
                    txtDienTich.Focus();
                else
                    txtSoPhong.Focus();
            }
            catch (Exception)
            {
                _sanSang = false;
                DatTrangThaiLuu(false);

                if (IsVisible)
                {
                    txtStatus.Text =
                        "Không tải được thông tin. Kiểm tra SQL Server.";
                }
            }
        }

        private static bool DocSo(
            string noiDung, decimal toiDa, out decimal giaTri)
        {
            string chuoi = noiDung.Trim().Replace(',', '.');

            return decimal.TryParse(
                       chuoi,
                       NumberStyles.AllowDecimalPoint,
                       CultureInfo.InvariantCulture,
                       out giaTri)
                   && giaTri > 0
                   && giaTri <= toiDa
                   && decimal.Round(giaTri, 2) == giaTri;
        }

        private async void Save_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangLuu || !_sanSang)
                return;

            txtStatus.Text = "";

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtStatus.Text = "Bạn không có quyền quản lý phòng.";
                return;
            }

            string maDayTro = _laCheDoSua
                ? _maDayTroCu
                : cboDayTro.SelectedValue?.ToString() ?? "";

            string soPhong = txtSoPhong.Text.Trim();
            string loaiPhong = cboLoaiPhong.Text.Trim();

            string trangThai =
                (cboTrangThai.SelectedItem as ComboBoxItem)
                ?.Content?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(maDayTro))
            {
                txtStatus.Text = "Vui lòng chọn dãy trọ.";
                return;
            }

            if (string.IsNullOrWhiteSpace(soPhong) ||
                soPhong.Length > 20)
            {
                txtStatus.Text = "Số phòng phải có từ 1 đến 20 ký tự.";
                txtSoPhong.Focus();
                return;
            }

            if (loaiPhong.Length > 50)
            {
                txtStatus.Text = "Loại phòng không được quá 50 ký tự.";
                return;
            }

            if (!DocSo(
                txtDienTich.Text, 99999999.99m, out decimal dienTich))
            {
                txtStatus.Text =
                    "Diện tích phải lớn hơn 0, tối đa 2 số thập phân. "
                    + "Ví dụ: 25 hoặc 25,5.";

                txtDienTich.Focus();
                return;
            }

            if (!DocSo(
                txtGiaThue.Text,
                9999999999999999.99m,
                out decimal giaThue))
            {
                txtStatus.Text =
                    "Giá thuê phải lớn hơn 0, tối đa 2 số thập phân. "
                    + "Không dùng dấu phân cách hàng nghìn. Ví dụ: 3000000.";

                txtGiaThue.Focus();
                return;
            }

            bool trangThaiHopLe =
                trangThai == "Trống"
                || trangThai == "Đang sửa chữa"
                || (_laCheDoSua
                    && _phongDangThue
                    && trangThai == "Đang thuê");

            if (!trangThaiHopLe)
            {
                txtStatus.Text = "Vui lòng chọn trạng thái hợp lệ.";
                return;
            }

            DatTrangThaiLuu(true);
            txtStatus.Text = "Đang lưu thông tin phòng...";

            bool thanhCong = false;

            try
            {
                int soDong = await ThemPhongService.LuuAsync(
                    _laCheDoSua,
                    user.MaNguoiDung,
                    _maPhong,
                    maDayTro,
                    soPhong,
                    loaiPhong,
                    dienTich,
                    giaThue,
                    trangThai);

                thanhCong = soDong == 1;

                if (!thanhCong)
                {
                    txtStatus.Text =
                        "Phòng đã bị xóa, đổi dãy hoặc bạn không còn quyền quản lý.";
                }
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                txtStatus.Text =
                    "Mã phòng đã tồn tại hoặc số phòng bị trùng trong dãy.";
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không lưu được phòng. Kiểm tra kết nối và cấu trúc database.";
            }
            catch (Exception)
            {
                txtStatus.Text = "Có lỗi khi lưu phòng. Vui lòng thử lại.";
            }
            finally
            {
                DatTrangThaiLuu(false);
            }

            if (thanhCong)
            {
                MessageBox.Show(
                    _laCheDoSua
                        ? "Cập nhật phòng trọ thành công!"
                        : "Thêm phòng trọ thành công!",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                LuuThanhCong?.Invoke(this, EventArgs.Empty);
            }
        }

        private void DatTrangThaiLuu(bool dangLuu)
        {
            _dangLuu = dangLuu;

            pnlThongTin.IsEnabled = !dangLuu && _sanSang;
            btnSave.IsEnabled = !dangLuu && _sanSang;
            btnHuy.IsEnabled = !dangLuu;

            btnSave.Content = dangLuu
                ? "Đang lưu..."
                : "Lưu thông tin";
        }

        private void Huy_Click(object sender, RoutedEventArgs e)
        {
            if (!_dangLuu)
                YeuCauHuy?.Invoke(this, EventArgs.Empty);
        }
    }
}