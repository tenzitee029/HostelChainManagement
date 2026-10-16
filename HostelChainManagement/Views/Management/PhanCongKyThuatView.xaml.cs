using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Management;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class PhanCongKyThuatView : UserControl
    {
        private readonly string _maSuCo;
        private string _maDayDaTai = "";

        private bool _dangTai;
        private bool _dangLuu;
        private bool _sanSang;
        private bool _daLuu;

        public event Action? QuayLai;
        public event Action? DaLuu;

        public PhanCongKyThuatView(string maSuCo)
        {
            InitializeComponent();

            _maSuCo = maSuCo;
            txtMaYeuCau.Text = maSuCo;
            dpHan.DisplayDateStart = DateTime.Today;

            // Nối sự kiện, tránh đăng ký cùng hàm hai lần.
            Loaded -= View_Loaded;
            Loaded += View_Loaded;

            dgNhanVien.SelectionChanged -= NhanVien_Changed;
            dgNhanVien.SelectionChanged += NhanVien_Changed;

            dpHan.SelectedDateChanged -= Han_Changed;
            dpHan.SelectedDateChanged += Han_Changed;

            btnLuu.Click -= Luu_Click;
            btnLuu.Click += Luu_Click;

            btnQuayLai.Click -= QuayLai_Click;
            btnQuayLai.Click += QuayLai_Click;

            CapNhatNut();
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai || _sanSang || _daLuu)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền phân công.";
                CapNhatNut();
                return;
            }

            _dangTai = true;
            _sanSang = false;
            dgNhanVien.ItemsSource = null;
            txtNhanVien.Clear();

            CapNhatNut();
            txtThongBao.Text = "Đang tải yêu cầu và kỹ thuật viên...";

            try
            {
                using var service = new PhanCongKyThuatService();
                await service.MoKetNoiAsync();

                var yeuCau = await service.LayYeuCauAsync(
                    _maSuCo,
                    user.MaNguoiDung);

                if (yeuCau == null)
                {
                    txtThongBao.Text =
                        "Yêu cầu không tồn tại hoặc bạn không có quyền.";
                    return;
                }

                if (!IsLoaded)
                    return;

                string trangThai =
                    Convert.ToString(yeuCau["TrangThai"]) ?? "";

                txtTrangThai.Text = trangThai;
                txtPhong.Text = Convert.ToString(yeuCau["TenPhong"]);
                txtNoiDung.Text =
                    Convert.ToString(yeuCau["NoiDungSuCo"]);

                _maDayDaTai =
                    Convert.ToString(yeuCau["MaDayTro"]) ?? "";

                bool khanCap =
                    Convert.ToString(yeuCau["MucDoUuTien"]) == "Khẩn cấp";

                rbKhanCap.IsChecked = khanCap;
                rbBinhThuong.IsChecked = !khanCap;

                if (trangThai != "Đã tiếp nhận")
                {
                    txtThongBao.Text =
                        "Chỉ phân công yêu cầu có trạng thái Đã tiếp nhận.";
                    return;
                }

                if (Convert.ToInt32(yeuCau["DaPhanCong"]) == 1)
                {
                    txtThongBao.Text =
                        "Yêu cầu đã có phân công. Hãy quay lại danh sách.";
                    return;
                }

                var table =
                    await service.LayNhanVienAsync(_maDayDaTai);

                if (!IsLoaded)
                    return;

                dgNhanVien.ItemsSource = table.DefaultView;
                _sanSang = table.Rows.Count > 0;

                txtThongBao.Text = _sanSang
                    ? "Chọn kỹ thuật viên và thời hạn hoàn thành dự kiến."
                    : "Dãy trọ chưa có kỹ thuật viên phù hợp.";
            }
            catch (Exception ex)
            {
                _sanSang = false;
                txtThongBao.Text =
                    "Không tải được dữ liệu: " + ex.Message;
            }
            finally
            {
                _dangTai = false;
                CapNhatNut();
            }
        }

        private void NhanVien_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            if (txtNhanVien == null)
                return;

            txtNhanVien.Text =
                dgNhanVien.SelectedItem is DataRowView nv
                    ? Convert.ToString(nv["HoTen"])
                    : "";

            CapNhatNut();
        }

        private void Han_Changed(
            object? sender, SelectionChangedEventArgs e)
        {
            CapNhatNut();
        }

        private async void Luu_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai || _dangLuu || !_sanSang || _daLuu ||
                dgNhanVien.SelectedItem is not DataRowView nv)
            {
                return;
            }

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền phân công.";
                return;
            }

            if (!dpHan.SelectedDate.HasValue ||
                dpHan.SelectedDate.Value.Date < DateTime.Today)
            {
                txtThongBao.Text =
                    "Chọn hạn dự kiến từ hôm nay trở đi.";
                return;
            }

            DateTime han = dpHan.SelectedDate.Value.Date;

            string maNhanVien =
                Convert.ToString(nv["MaNhanVien"]) ?? "";

            string uuTien = rbKhanCap.IsChecked == true
                ? "Khẩn cấp"
                : rbBinhThuong.IsChecked == true
                    ? "Bình thường"
                    : "";

            if (maNhanVien.Length == 0 || uuTien.Length == 0)
            {
                txtThongBao.Text =
                    "Vui lòng chọn kỹ thuật viên và mức độ ưu tiên.";
                return;
            }

            var xacNhan = MessageBox.Show(
                $"Phân công yêu cầu {_maSuCo} cho {nv["HoTen"]}?\n"
                + $"Ưu tiên: {uuTien}\n"
                + $"Hạn dự kiến: {han:dd/MM/yyyy}",
                "Xác nhận phân công",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (xacNhan != MessageBoxResult.Yes)
                return;

            _dangLuu = true;
            CapNhatNut();
            txtThongBao.Text = "Đang lưu phân công...";

            bool thanhCong = false;

            try
            {
                await PhanCongKyThuatService.LuuAsync(
                    _maSuCo,
                    _maDayDaTai,
                    user.MaNguoiDung,
                    maNhanVien,
                    uuTien,
                    han);

                thanhCong = true;
                _daLuu = true;
                _sanSang = false;

                txtTrangThai.Text = "Đã phân công";
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                txtThongBao.Text =
                    "Dữ liệu đang được cập nhật. Vui lòng thử lại.";
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                txtThongBao.Text =
                    "Dữ liệu phân công bị trùng. Quay lại và tải lại yêu cầu.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không lưu được phân công: " + ex.Message;
            }
            finally
            {
                _dangLuu = false;
                CapNhatNut();
            }

            if (thanhCong)
            {
                MessageBox.Show(
                    "Đã lưu phân công và thông báo cho kỹ thuật viên.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DaLuu?.Invoke();
            }
        }

        private void CapNhatNut()
        {
            if (btnLuu == null || btnQuayLai == null ||
                dgNhanVien == null || dpHan == null ||
                rbBinhThuong == null || rbKhanCap == null)
            {
                return;
            }

            bool coQuyen =
                UserSession.CurrentUser?.VaiTro == "Nhân viên quản lý";

            bool choPhep =
                coQuyen && _sanSang &&
                !_dangTai && !_dangLuu && !_daLuu;

            dgNhanVien.IsEnabled = choPhep;
            rbBinhThuong.IsEnabled = choPhep;
            rbKhanCap.IsEnabled = choPhep;
            dpHan.IsEnabled = choPhep;

            btnQuayLai.IsEnabled = !_dangLuu;

            btnLuu.IsEnabled =
                choPhep &&
                dgNhanVien.SelectedItem is DataRowView &&
                dpHan.SelectedDate.HasValue &&
                dpHan.SelectedDate.Value.Date >= DateTime.Today;

            btnLuu.Content = _dangLuu
                ? "Đang lưu..."
                : "Lưu phân công";
        }

        private void QuayLai_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_dangLuu)
                QuayLai?.Invoke();
        }
    }
}