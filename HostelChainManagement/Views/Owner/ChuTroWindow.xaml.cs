
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Views.Account;
using QuanLyChuoiNhaTro.Views.Auth;

namespace QuanLyChuoiNhaTro.Views.Owner
{
    public partial class ChuTroWindow : Window
    {
        public ChuTroWindow()
        {
            InitializeComponent();

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Chủ nhà trọ")
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền mở giao diện Chủ nhà trọ.");
            }

            txtUser.Text = user.HoTen;

            ShowOverview();
        }

        private void ShowView(
            UserControl view,
            string title,
            Button selectedButton)
        {
            Button[] buttons =
            {
                btnOverview,
                btnDayTro,
                btnNhanVien,
                btnPhanCong,
                btnHopDong,
                btnBaoCao,
                btnProfile,
                btnPassword
            };

            foreach (var button in buttons)
            {
                button.Background = Brushes.Transparent;

                button.Foreground = new SolidColorBrush(
                    Color.FromRgb(203, 213, 225));
            }

            selectedButton.Background = new SolidColorBrush(
                Color.FromRgb(37, 99, 235));

            selectedButton.Foreground = Brushes.White;

            txtBreadcrumb.Text = "Hệ thống / " + title;

            contentArea.Content = view;
        }

        private void ShowOverview()
        {
            var view = new TongQuanChuTroView();

            view.NavigateRequested += OnOverviewNavigate;

            ShowView(
                view,
                "Tổng quan",
                btnOverview);
        }

        private void Overview_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowOverview();
        }

        private void OnOverviewNavigate(string destination)
        {
            switch (destination)
            {
                case "DayTro":
                    OpenPendingScreen("Quản lý các dãy trọ");
                    break;

                case "NhanVien":
                    OpenPendingScreen("Quản lý nhân viên");
                    break;

                case "PhanCong":
                    OpenPendingScreen(
                        "Phân công nhân viên quản lý");
                    break;

                case "HopDong":
                    OpenPendingScreen("Hợp đồng & hóa đơn");
                    break;
            }
        }

        private void OpenPendingScreen(string screenName)
        {
            MessageBox.Show(
                this,
                $"Màn hình \"{screenName}\" chưa được triển khai.\n"
                + "Chức năng này sẽ được thực hiện ở bước tiếp theo.",
                "Thông báo",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void DayTro_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenPendingScreen("Quản lý các dãy trọ");
        }

        private void NhanVien_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenPendingScreen("Quản lý nhân viên");
        }

        private void PhanCong_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenPendingScreen("Phân công nhân viên quản lý");
        }

        private void HopDong_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenPendingScreen("Hợp đồng & hóa đơn");
        }

        private void BaoCao_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenPendingScreen("Xem báo cáo");
        }

        private void Profile_Click(
            object sender,
            RoutedEventArgs e)
        {
            var view = new HoSoView();

            view.ProfileUpdated += (_, _) =>
            {
                txtUser.Text =
                    UserSession.CurrentUser?.HoTen ?? "—";
            };

            ShowView(
                view,
                "Hồ sơ cá nhân",
                btnProfile);
        }

        private void Password_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowView(
                new DoiMatKhauView(),
                "Đổi mật khẩu",
                btnPassword);
        }

        private void Logout_Click(
            object sender,
            RoutedEventArgs e)
        {
            var answer = MessageBox.Show(
                this,
                "Bạn có chắc chắn muốn đăng xuất?",
                "Xác nhận đăng xuất",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return;

            UserSession.SignOut();

            var loginWindow = new LoginWindow();

            Application.Current.MainWindow = loginWindow;

            loginWindow.Show();

            Close();
        }
    }
}
