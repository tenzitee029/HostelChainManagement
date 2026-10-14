using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Views.Account;
using QuanLyChuoiNhaTro.Views.Auth;

namespace QuanLyChuoiNhaTro.Views.Admin
{
    public partial class AdminWindow : Window
    {
        // Giữ trạng thái gửi lại email khi chuyển giao diện.
        private TaoTaiKhoanView? _createAccountView;

        public AdminWindow()
        {
            InitializeComponent();

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Admin")
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền mở giao diện Admin.");
            }

            txtUser.Text = user.HoTen;

            ShowMaintenance();
        }

        private bool CanNavigate => contentArea.Content switch
        {
            BaoTriHeThongView view => !view.IsBusy,
            NhatKyHeThongView view => !view.IsBusy,
            QuanLyTaiKhoanView view => !view.IsBusy,
            TaoTaiKhoanView view => !view.IsBusy,
            _ => true
        };

        private void ShowView(
            UserControl view,
            string title,
            Button? selectedButton = null)
        {
            foreach (var button in new[]
            {
                btnMaintenance,
                btnAccounts,
                btnProfile,
                btnPassword
            })
            {
                button.Background = Brushes.Transparent;

                button.Foreground = new SolidColorBrush(
                    Color.FromRgb(203, 213, 225));
            }

            if (selectedButton != null)
            {
                selectedButton.Background = new SolidColorBrush(
                    Color.FromRgb(37, 99, 235));

                selectedButton.Foreground = Brushes.White;
            }

            txtBreadcrumb.Text = "Hệ thống / " + title;

            contentArea.Content = view;
        }

        private void ShowMaintenance()
        {
            var view = new BaoTriHeThongView();

            view.LogsRequested += ShowLogs;

            view.SessionResetRequested += OpenLogin;

            ShowView(
                view,
                "Bảo trì hệ thống",
                btnMaintenance);
        }

        private void ShowLogs()
        {
            ShowView(
                new NhatKyHeThongView(),
                "Bảo trì hệ thống / Xem nhật ký hệ thống",
                btnMaintenance);
        }

        private void ShowAccounts()
        {
            var view = new QuanLyTaiKhoanView();

            view.CreateRequested += ShowCreateAccount;

            ShowView(
                view,
                "Quản lý tài khoản",
                btnAccounts);
        }

        private void ShowCreateAccount()
        {
            _createAccountView ??= new TaoTaiKhoanView();

            ShowView(
                _createAccountView,
                "Quản lý tài khoản / Tạo tài khoản mới",
                btnAccounts);
        }

        private void Maintenance_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!CanNavigate)
                return;

            ShowMaintenance();
        }

        private void Accounts_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!CanNavigate)
                return;

            ShowAccounts();
        }

        private void Profile_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!CanNavigate)
                return;

            var view = new HoSoView();

            view.ProfileUpdated += (_, _) =>
            {
                txtUser.Text =
                    UserSession.CurrentUser?.HoTen ?? "";
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
            if (!CanNavigate)
                return;

            ShowView(
                new DoiMatKhauView(),
                "Đổi mật khẩu",
                btnPassword);
        }

        private void OpenLogin()
        {
            UserSession.SignOut();

            var loginWindow = new LoginWindow();

            Application.Current.MainWindow = loginWindow;

            loginWindow.Show();

            Close();
        }

        private void Logout_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!CanNavigate)
                return;

            var answer = MessageBox.Show(
                this,
                "Bạn có chắc chắn muốn đăng xuất?",
                "Xác nhận",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return;

            OpenLogin();
        }
    }
}