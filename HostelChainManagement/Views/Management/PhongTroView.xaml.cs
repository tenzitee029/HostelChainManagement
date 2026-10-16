using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Management;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class PhongTroView : UserControl
    {
        private DataTable? _rooms;
        private bool _loading;
        private bool _dangXoa;

        public PhongTroView()
        {
            InitializeComponent();
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            await TaiDanhSachPhongAsync();
        }

        // Tải danh sách phòng từ Service.
        private async Task TaiDanhSachPhongAsync()
        {
            if (_loading)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                _rooms = null;
                dgPhong.ItemsSource = null;
                CapNhatNutThaoTac();

                txtStatus.Text =
                    "Bạn không có quyền xem màn hình này.";
                return;
            }

            _loading = true;
            CapNhatNutThaoTac();
            txtStatus.Text = "Đang tải danh sách phòng...";

            try
            {
                var table =
                    await PhongTroService.LayDanhSachAsync(
                        user.MaNguoiDung);

                _rooms = table;
                ApplySearch();
            }
            catch (Exception)
            {
                _rooms = null;
                dgPhong.ItemsSource = null;

                txtStatus.Text =
                    "Không tải được phòng. Kiểm tra kết nối SQL Server.";
            }
            finally
            {
                _loading = false;
                CapNhatNutThaoTac();
            }
        }

        // Hiển thị form Thêm/Sửa trong trang.
        private void HienThiFormPhong(ThemPhongControl control)
        {
            control.YeuCauHuy += (s, e) =>
            {
                DongFormPhong();
            };

            control.LuuThanhCong += async (s, e) =>
            {
                DongFormPhong();
                txtSearch.Clear();
                await TaiDanhSachPhongAsync();
            };

            DanhSachPhong.Visibility = Visibility.Collapsed;

            NoiDungPhong.Content = control;
            NoiDungPhong.Visibility = Visibility.Visible;
        }

        // Quay lại danh sách phòng.
        private void DongFormPhong()
        {
            NoiDungPhong.Content = null;
            NoiDungPhong.Visibility = Visibility.Collapsed;

            DanhSachPhong.Visibility = Visibility.Visible;
        }

        // Nút thêm phòng.
        private void AddRoom_Click(
            object sender, RoutedEventArgs e)
        {
            if (_loading || _dangXoa)
                return;

            HienThiFormPhong(new ThemPhongControl());
        }

        // Nút tìm kiếm.
        private void Search_Click(
            object sender, RoutedEventArgs e)
        {
            if (_loading || _dangXoa)
                return;

            ApplySearch();
        }

        // Lọc danh sách phòng.
        private void ApplySearch()
        {
            if (_rooms == null)
                return;

            string keyword = txtSearch.Text.Trim();
            var results = _rooms.Clone();

            foreach (DataRow row in _rooms.Rows)
            {
                bool matches = keyword.Length == 0;

                foreach (string column in new[]
                {
                    "MaPhong",
                    "SoPhong",
                    "TenDayTro",
                    "LoaiPhong",
                    "TrangThai"
                })
                {
                    string value =
                        Convert.ToString(row[column]) ?? "";

                    if (value.IndexOf(
                        keyword,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matches = true;
                        break;
                    }
                }

                if (matches)
                    results.ImportRow(row);
            }

            dgPhong.ItemsSource = results.DefaultView;
            CapNhatNutThaoTac();

            txtStatus.Text = results.Rows.Count == 0
                ? "Không có phòng phù hợp để hiển thị."
                : $"Hiển thị {results.Rows.Count} phòng.";
        }

        // Khi chọn một phòng.
        private void dgPhong_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            CapNhatNutThaoTac();
        }

        // Cập nhật trạng thái nút Sửa/Xóa.
        private void CapNhatNutThaoTac()
        {
            bool choPhep =
                !_loading
                && !_dangXoa
                && dgPhong.SelectedItem is DataRowView;

            btnSua.IsEnabled = choPhep;
            btnXoa.IsEnabled = choPhep;
        }

        // Nút sửa phòng.
        private void SuaPhong_Click(
            object sender, RoutedEventArgs e)
        {
            if (_loading || _dangXoa)
                return;

            if (dgPhong.SelectedItem is not DataRowView phong)
                return;

            HienThiFormPhong(new ThemPhongControl(phong));
        }

        // Nút xóa phòng.
        private async void XoaPhong_Click(
            object sender, RoutedEventArgs e)
        {
            if (_loading || _dangXoa)
                return;

            if (dgPhong.SelectedItem is not DataRowView phong)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                MessageBox.Show(
                    "Bạn không có quyền xóa phòng.",
                    "Thông báo");
                return;
            }

            string maPhong =
                Convert.ToString(phong["MaPhong"]) ?? "";

            string soPhong =
                Convert.ToString(phong["SoPhong"]) ?? "";

            string trangThai =
                Convert.ToString(phong["TrangThai"]) ?? "";

            if (trangThai == "Đang thuê")
            {
                MessageBox.Show(
                    "Không thể xóa phòng đang có người thuê.",
                    "Không thể xóa",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var xacNhan = MessageBox.Show(
                $"Bạn có chắc muốn xóa phòng {soPhong} ({maPhong})?",
                "Xác nhận xóa phòng",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (xacNhan != MessageBoxResult.Yes)
                return;

            _dangXoa = true;
            IsEnabled = false;
            CapNhatNutThaoTac();

            bool daXoa = false;

            try
            {
                int soDong = await PhongTroService.XoaAsync(
                    maPhong,
                    user.MaNguoiDung);

                daXoa = soDong == 1;

                if (daXoa)
                {
                    MessageBox.Show(
                        "Xóa phòng thành công.",
                        "Thông báo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Không thể xóa: phòng đang thuê, đã bị xóa "
                        + "hoặc bạn không còn quyền quản lý.",
                        "Thông báo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                MessageBox.Show(
                    "Không thể xóa phòng vì đã có hợp đồng, hóa đơn, "
                    + "thiết bị hoặc dữ liệu liên quan.",
                    "Không thể xóa",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "Không xóa được phòng. Kiểm tra kết nối SQL Server.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _dangXoa = false;
                IsEnabled = true;
                CapNhatNutThaoTac();
            }

            if (daXoa)
            {
                await TaiDanhSachPhongAsync();
            }
        }
    }
}