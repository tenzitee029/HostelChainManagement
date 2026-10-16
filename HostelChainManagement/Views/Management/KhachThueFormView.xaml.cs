using System;
using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Management;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class KhachThueFormView : UserControl
    {
        private const string VaiTroChu = "Người đứng tên hợp đồng";
        private const string VaiTroThanhVien = "Thành viên ở cùng";

        private readonly bool _dangSua;
        private readonly bool _suaThanhVien;
        private readonly string _maKhach;
        private readonly string _maThanhVienMoi;
        private readonly string _tenDangNhapMoi;
        private readonly string _tenDangNhapThanhVienMoi;
        private readonly string _matKhauMoi;

        private bool _dangTai;
        private bool _dangTaiPhong;
        private bool _dangLuu;
        private bool _sanSang;
        private int _lanTaiPhong;

        public event Action? QuayLai;
        public event Action? DaLuu;

        private bool ThemThanhVien =>
            !_dangSua &&
            Convert.ToString(cboVaiTro.SelectedItem) == VaiTroThanhVien;

        public KhachThueFormView(DataRowView? khach = null)
        {
            InitializeComponent();

            _dangSua = khach != null;
            _suaThanhVien = _dangSua &&
                Convert.ToString(khach!["LoaiHoSo"]) == VaiTroThanhVien;

            _maKhach = khach == null
                ? TaoMa("KT")
                : Convert.ToString(khach["MaKhachThue"]) ?? "";

            _maThanhVienMoi = TaoMa("TV");
            _tenDangNhapMoi = _maKhach.ToLowerInvariant();

            _tenDangNhapThanhVienMoi =
                _maThanhVienMoi.ToLowerInvariant();

            _matKhauMoi = _dangSua
                ? ""
                : "Aa1!" + Convert.ToBase64String(
                    RandomNumberGenerator.GetBytes(12));

            txtMaKhach.Text = _maKhach;

            txtTieuDe.Text = _dangSua
                ? (_suaThanhVien
                    ? "Cập nhật thành viên ở cùng"
                    : "Cập nhật khách thuê")
                : "Thêm khách thuê";

            if (!_suaThanhVien)
                cboVaiTro.Items.Add(VaiTroChu);

            if (!_dangSua || _suaThanhVien)
                cboVaiTro.Items.Add(VaiTroThanhVien);

            cboVaiTro.SelectedIndex = 0;

            HienThiPhongChu(
                "Chưa có phòng đang thuê",
                "Chưa có hợp đồng");

            txtHuongDan.Text =
                "Người đứng tên được tạo tài khoản tự động. "
                + "Phòng ở được cập nhật khi lập hợp đồng.";

            DatTrangThai();
        }

        private static string TaoMa(string tienTo)
        {
            return KhachThueFormService.TaoMa(tienTo);
        }

        private void HienThiPhongChu(
            string tenPhong,
            string trangThai)
        {
            var table = new DataTable();

            table.Columns.Add("MaHopDong", typeof(string));
            table.Columns.Add("TenHienThi", typeof(string));

            table.Rows.Add("", tenPhong);

            cboPhong.ItemsSource = table.DefaultView;
            cboPhong.SelectedIndex = 0;

            txtTrangThai.Text = trangThai;
        }

        private void DatTrangThai()
        {
            if (pnlThongTin == null || btnLuu == null)
                return;

            bool choPhep =
                _sanSang && !_dangTai &&
                !_dangTaiPhong && !_dangLuu;

            pnlThongTin.IsEnabled = choPhep;

            cboDayTro.IsEnabled = !_dangSua;
            cboVaiTro.IsEnabled = !_dangSua;
            cboPhong.IsEnabled = ThemThanhVien;

            btnLuu.IsEnabled =
                choPhep &&
                (!ThemThanhVien ||
                 cboPhong.SelectedItem is DataRowView);

            btnQuayLai.IsEnabled = !_dangLuu;
            btnLuu.Content = _dangLuu ? "Đang lưu..." : "Lưu hồ sơ";
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai || _sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền quản lý khách thuê.";
                return;
            }

            _dangTai = true;
            DatTrangThai();

            txtThongBao.Text = "Đang tải thông tin...";

            try
            {
                using var service = new KhachThueFormService();
                await service.MoKetNoiAsync();

                var dayTro =
                    await service.LayDayTroAsync(user.MaNguoiDung);

                if (!IsLoaded)
                    return;

                cboDayTro.ItemsSource = dayTro.DefaultView;

                if (dayTro.Rows.Count == 0)
                {
                    txtThongBao.Text = "Bạn chưa được phân công dãy trọ.";
                    return;
                }

                if (_dangSua && _suaThanhVien)
                {
                    var thongTin = await service.LayThanhVienAsync(
                        _maKhach,
                        user.MaNguoiDung);

                    if (thongTin == null)
                    {
                        txtThongBao.Text =
                            "Không tìm thấy thành viên hoặc không có quyền.";
                        return;
                    }

                    if (!IsLoaded)
                        return;

                    txtHoTen.Text =
                        Convert.ToString(thongTin["HoTen"]) ?? "";

                    txtCCCD.Text =
                        Convert.ToString(thongTin["CCCD"]) ?? "";

                    txtSoDienThoai.Text =
                        Convert.ToString(thongTin["SoDienThoai"]) ?? "";

                    cboDayTro.SelectedValue =
                        Convert.ToString(thongTin["MaDayTro"]);

                    pnlDiaChi.Visibility = Visibility.Collapsed;

                    HienThiPhongChu(
                        Convert.ToString(thongTin["TenPhong"]) ?? "",
                        Convert.ToString(thongTin["TrangThai"]) ?? "");

                    txtHuongDan.Text =
                        "Cập nhật thông tin thành viên ở cùng. Phòng và hợp đồng được giữ nguyên.";
                }
                else if (_dangSua)
                {
                    var thongTin = await service.LayKhachAsync(
                        _maKhach,
                        user.MaNguoiDung);

                    if (thongTin == null)
                    {
                        txtThongBao.Text =
                            "Hồ sơ không tồn tại hoặc bạn không có quyền.";
                        return;
                    }

                    if (!IsLoaded)
                        return;

                    txtHoTen.Text =
                        Convert.ToString(thongTin["HoTen"]) ?? "";

                    txtCCCD.Text =
                        Convert.ToString(thongTin["CCCD"]) ?? "";

                    txtSoDienThoai.Text =
                        Convert.ToString(thongTin["SoDienThoai"]) ?? "";

                    txtDiaChi.Text =
                        Convert.ToString(thongTin["DiaChi"]) ?? "";

                    cboDayTro.SelectedValue =
                        Convert.ToString(thongTin["MaDayTro"]);

                    bool dangThue =
                        thongTin["DangHieuLuc"] != DBNull.Value &&
                        Convert.ToInt32(thongTin["DangHieuLuc"]) == 1;

                    bool coHopDong =
                        thongTin["MaHopDong"] != DBNull.Value;

                    HienThiPhongChu(
                        dangThue
                            ? Convert.ToString(thongTin["TenPhong"]) ?? ""
                            : "Chưa có phòng đang thuê",
                        dangThue
                            ? "Đang thuê"
                            : coHopDong
                                ? "Không có hợp đồng hiệu lực"
                                : "Chưa có hợp đồng");
                }
                else
                {
                    cboDayTro.SelectedIndex = 0;
                }

                _sanSang = true;
                txtThongBao.Text = "";
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

        private async void VaiTro_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            if (!_sanSang || _dangSua)
                return;

            txtThongBao.Text = "";

            pnlDiaChi.Visibility = ThemThanhVien
                ? Visibility.Collapsed
                : Visibility.Visible;

            txtMaKhach.Text = ThemThanhVien
                ? _maThanhVienMoi
                : _maKhach;

            if (ThemThanhVien)
            {
                txtHuongDan.Text =
                    "Chọn phòng đôi đang có người đứng tên hợp đồng. "
                    + "Mỗi phòng tối đa 2 người, gồm người đứng tên "
                    + "và 1 thành viên ở cùng. "
                    + "Thành viên được lưu vào hợp đồng đang có "
                    + "và được cấp tài khoản đăng nhập riêng.";

                await TaiPhongOChungAsync();
            }
            else
            {
                _lanTaiPhong++;
                _dangTaiPhong = false;

                HienThiPhongChu(
                    "Phòng được chọn khi lập hợp đồng",
                    "Chưa có hợp đồng");

                txtHuongDan.Text =
                    "Người đứng tên được tạo tài khoản tự động. "
                    + "Phòng ở được cập nhật khi lập hợp đồng.";
            }

            DatTrangThai();
        }

        private async void DayTro_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            if (_sanSang && ThemThanhVien)
                await TaiPhongOChungAsync();
        }

        private void Phong_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            if (!_sanSang || !ThemThanhVien)
                return;

            txtTrangThai.Text =
                cboPhong.SelectedItem is DataRowView
                    ? "Đang ở cùng"
                    : "Chưa chọn phòng";

            DatTrangThai();
        }

        private async Task TaiPhongOChungAsync()
        {
            int lanTai = ++_lanTaiPhong;

            _dangTaiPhong = true;
            cboPhong.ItemsSource = null;

            txtTrangThai.Text = "Chưa chọn phòng";
            txtThongBao.Text = "Đang tải phòng đôi còn chỗ...";

            DatTrangThai();

            try
            {
                var user = UserSession.CurrentUser;

                if (user == null || user.VaiTro != "Nhân viên quản lý")
                {
                    throw new InvalidOperationException(
                        "Bạn không có quyền quản lý.");
                }

                string maDay =
                    cboDayTro.SelectedValue?.ToString() ?? "";

                var table =
                    await KhachThueFormService.LayPhongOChungAsync(
                        maDay,
                        user.MaNguoiDung);

                if (lanTai != _lanTaiPhong || !ThemThanhVien || !IsLoaded)
                    return;

                cboPhong.ItemsSource = table.DefaultView;

                if (table.Rows.Count > 0)
                {
                    cboPhong.SelectedIndex = 0;
                    txtThongBao.Text = "";
                }
                else
                {
                    txtThongBao.Text =
                        "Không có phòng đôi còn chỗ trong dãy này. "
                        + "Phòng phải có hợp đồng đang hiệu lực "
                        + "và chưa có thành viên ở cùng.";
                }
            }
            catch (Exception ex)
            {
                if (lanTai == _lanTaiPhong)
                {
                    txtThongBao.Text =
                        "Không tải được phòng: " + ex.Message;
                }
            }
            finally
            {
                if (lanTai == _lanTaiPhong)
                {
                    _dangTaiPhong = false;
                    DatTrangThai();
                }
            }
        }

        private bool KiemTraThongTin()
        {
            string hoTen = txtHoTen.Text.Trim();
            string cccd = txtCCCD.Text.Trim();
            string sdt = txtSoDienThoai.Text.Trim();

            if (hoTen.Length == 0 || hoTen.Length > 100)
            {
                txtThongBao.Text = "Họ tên phải có từ 1 đến 100 ký tự.";
                return false;
            }

            if (!Regex.IsMatch(cccd, @"^[0-9]{12}$"))
            {
                txtThongBao.Text = "CCCD phải gồm đúng 12 chữ số.";
                return false;
            }

            if (!Regex.IsMatch(sdt, @"^0[0-9]{9}$"))
            {
                txtThongBao.Text =
                    "Số điện thoại gồm 10 chữ số, bắt đầu bằng 0.";
                return false;
            }

            if (!ThemThanhVien)
            {
                string diaChi = txtDiaChi.Text.Trim();

                if (diaChi.Length == 0 || diaChi.Length > 255)
                {
                    txtThongBao.Text =
                        "Địa chỉ phải có từ 1 đến 255 ký tự.";
                    return false;
                }
            }

            if (!_dangSua && cboDayTro.SelectedValue == null)
            {
                txtThongBao.Text = "Vui lòng chọn dãy trọ.";
                return false;
            }

            if (ThemThanhVien &&
                cboPhong.SelectedItem is not DataRowView)
            {
                txtThongBao.Text = "Vui lòng chọn phòng đôi còn chỗ.";
                return false;
            }

            return true;
        }

        private async void Luu_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_sanSang || _dangTai || _dangTaiPhong || _dangLuu)
                return;

            if (!KiemTraThongTin())
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền lưu hồ sơ.";
                return;
            }

            bool laThanhVien = ThemThanhVien;
            bool thanhCong = false;

            _dangLuu = true;
            DatTrangThai();

            txtThongBao.Text = "Đang lưu hồ sơ...";

            try
            {
                if (_suaThanhVien)
                    await CapNhatThanhVienAsync(user.MaNguoiDung);
                else if (laThanhVien)
                    await LuuThanhVienAsync(user.MaNguoiDung);
                else
                    await LuuKhachAsync(user.MaNguoiDung);

                thanhCong = true;
                _sanSang = false;
            }
            catch (SqlException ex)
                when (ex.Number == 2601 || ex.Number == 2627)
            {
                txtThongBao.Text =
                    "CCCD, số điện thoại hoặc tài khoản bị trùng.";
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                txtThongBao.Text =
                    "Dữ liệu đang được người khác cập nhật. "
                    + "Vui lòng thử lưu lại.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không lưu được hồ sơ: " + ex.Message;
            }
            finally
            {
                _dangLuu = false;
                DatTrangThai();
            }

            if (!thanhCong)
                return;

            string thongBao;

            if (_suaThanhVien)
            {
                thongBao = "Cập nhật thành viên ở cùng thành công.";
            }
            else if (laThanhVien)
            {
                thongBao =
                    "Đã thêm thành viên vào phòng đôi thành công.\n\n"
                    + "Tên đăng nhập: " + _tenDangNhapThanhVienMoi + "\n"
                    + "Mật khẩu: " + _matKhauMoi + "\n\n"
                    + "Hãy ghi lại thông tin tài khoản.";
            }
            else if (_dangSua)
            {
                thongBao = "Cập nhật khách thuê thành công.";
            }
            else
            {
                thongBao =
                    "Thêm khách thuê thành công.\n\n"
                    + "Tên đăng nhập: " + _tenDangNhapMoi + "\n"
                    + "Mật khẩu: " + _matKhauMoi + "\n\n"
                    + "Hãy ghi lại thông tin tài khoản.";
            }

            MessageBox.Show(
                thongBao,
                "Thông báo",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DaLuu?.Invoke();
        }

        private async Task LuuKhachAsync(string maQuanLy)
        {
            await KhachThueFormService.LuuKhachAsync(
                maQuanLy,
                _dangSua,
                _maKhach,
                _tenDangNhapMoi,
                _matKhauMoi,
                () => new DuLieuNhapKhachThue
                {
                    HoTen = txtHoTen.Text.Trim(),
                    CCCD = txtCCCD.Text.Trim(),
                    SoDienThoai = txtSoDienThoai.Text.Trim(),
                    DiaChi = txtDiaChi.Text.Trim(),

                    MaDayTro = !_dangSua
                        ? cboDayTro.SelectedValue!.ToString()!
                        : ""
                });
        }

        private async Task CapNhatThanhVienAsync(string maQuanLy)
        {
            await KhachThueFormService.CapNhatThanhVienAsync(
                maQuanLy,
                _maKhach,
                () => new DuLieuNhapKhachThue
                {
                    HoTen = txtHoTen.Text.Trim(),
                    CCCD = txtCCCD.Text.Trim(),
                    SoDienThoai = txtSoDienThoai.Text.Trim()
                });
        }

        private async Task LuuThanhVienAsync(string maQuanLy)
        {
            if (cboPhong.SelectedItem is not DataRowView phong)
            {
                throw new InvalidOperationException(
                    "Chưa chọn phòng.");
            }

            string maPhong =
                Convert.ToString(phong["MaPhong"]) ?? "";

            string maHopDong =
                Convert.ToString(phong["MaHopDong"]) ?? "";

            await KhachThueFormService.LuuThanhVienAsync(
                maQuanLy,
                maPhong,
                maHopDong,
                _maThanhVienMoi,
                _tenDangNhapThanhVienMoi,
                _matKhauMoi,
                () => new DuLieuNhapKhachThue
                {
                    HoTen = txtHoTen.Text.Trim(),
                    CCCD = txtCCCD.Text.Trim(),
                    SoDienThoai = txtSoDienThoai.Text.Trim(),
                    DiaChi = txtDiaChi.Text.Trim()
                });
        }

        private void QuayLai_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangLuu)
                return;

            _lanTaiPhong++;
            QuayLai?.Invoke();
        }
    }
}