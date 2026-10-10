using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Views.Auth;

namespace QuanLyChuoiNhaTro.Views.Tenant
{
    public partial class PhongCuaToiWindow : Window
    {
        private bool _loading;

        public PhongCuaToiWindow()
        {
            InitializeComponent();

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Khách thuê")
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền mở màn hình Khách thuê.");
            }

            txtUser.Text = user.HoTen;
        }

        private async void Window_Loaded(
            object sender, RoutedEventArgs e)
        {
            await LoadRoomAsync();
        }

        private async void Refresh_Click(
            object sender, RoutedEventArgs e)
        {
            await LoadRoomAsync();
        }

        private async Task LoadRoomAsync()
        {
            if (_loading)
                return;

            _loading = true;
            ClearRoom();
            txtStatus.Text = "Đang tải thông tin phòng...";

            try
            {
                var room =
                    await KhachThueService.GetPhongCuaToiAsync();

                if (!IsVisible)
                    return;

                if (room == null)
                {
                    txtStatus.Text =
                        "Tài khoản chưa có hợp đồng thuê đang hiệu lực.";
                    return;
                }

                txtSoPhong.Text = room.SoPhong;
                txtTenDay.Text = room.TenDayTro;
                txtDiaChi.Text = room.DiaChi;
                txtDienTich.Text = $"{room.DienTich:N2} m²";
                txtGiaThue.Text = $"{room.GiaThue:N0} VNĐ";

                dgThietBi.ItemsSource = room.ThietBi.DefaultView;
                dgThanhVien.ItemsSource = room.ThanhVien.DefaultView;

                txtEmptyEquipment.Visibility =
                    room.ThietBi.Rows.Count == 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                txtEmptyMembers.Visibility =
                    room.ThanhVien.Rows.Count == 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                txtStatus.Text = "";
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không tải được dữ liệu. Kiểm tra kết nối SQL Server.";
            }
            catch (Exception ex)
            {
                txtStatus.Text = ex.Message;
            }
            finally
            {
                _loading = false;
            }
        }

        private void ClearRoom()
        {
            txtSoPhong.Text = "—";
            txtTenDay.Text = "—";
            txtDiaChi.Text = "—";
            txtDienTich.Text = "—";
            txtGiaThue.Text = "—";

            dgThietBi.ItemsSource = null;
            dgThanhVien.ItemsSource = null;

            txtEmptyEquipment.Visibility = Visibility.Visible;
            txtEmptyMembers.Visibility = Visibility.Visible;
        }

        private void PendingFeature_Click(
            object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                MessageBox.Show(
                    this,
                    $"Chức năng “{button.Content}” sẽ được triển khai tiếp.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void Logout_Click(
            object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show(
                this,
                "Bạn có chắc chắn muốn đăng xuất?",
                "Xác nhận",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return;

            UserSession.SignOut();

            var login = new LoginWindow();
            Application.Current.MainWindow = login;

            login.Show();
            Close();
        }
    }
}