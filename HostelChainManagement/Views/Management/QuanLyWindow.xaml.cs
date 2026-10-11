using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Views.Account;
using QuanLyChuoiNhaTro.Views.Auth;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class QuanLyWindow : Window
    {
        public QuanLyWindow()
        {
            InitializeComponent();

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền mở màn hình quản lý.");
            }

            txtUser.Text = user.HoTen;
            ShowOverview();
        }

        private void ShowOverview()
        {
            ShowView(
                new TongQuanQuanLyView(),
                "Tổng quan",
                btnOverview);
        }

        private void ShowView(
            UserControl view,
            string title,
            Button selectedButton)
        {
            Button[] buttons =
            {
                btnOverview,
                btnRooms,
                btnTenants,
                btnContracts,
                btnInvoices,
                btnRepairs,
                btnNotifications,
                btnReports,
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

            txtBreadcrumb.Text = $"Hệ thống / {title}";
            contentArea.Content = view;
        }

        private void Overview_Click(
            object sender, RoutedEventArgs e)
        {
            ShowOverview();
        }

        private void Rooms_Click(
            object sender, RoutedEventArgs e)
        {
            ShowView(
                new PhongTroView(),
                "Quản lý phòng",
                btnRooms);
        }

        private void Profile_Click(
            object sender, RoutedEventArgs e)
        {
            var view = new HoSoView();

            view.ProfileUpdated += (_, _) =>
            {
                txtUser.Text =
                    UserSession.CurrentUser?.HoTen ?? "";
            };

            ShowView(view, "Hồ sơ cá nhân", btnProfile);
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