using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Views.Auth;
using QuanLyChuoiNhaTro.Views.Account;

namespace QuanLyChuoiNhaTro.Views.Tenant
{
    public partial class KhachThueWindow : Window
    {
        public KhachThueWindow()
        {
            InitializeComponent();

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Khách thuê")
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền mở màn hình Khách thuê.");
            }

            txtUser.Text = user.HoTen;

            ShowRoom();
        }

        private void ShowRoom()
        {
            txtBreadcrumb.Text = "Hệ thống / Phòng của tôi";
            SetSelectedMenu(btnRoom);

            contentArea.Content = new PhongCuaToiView();
        }

        private void Room_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowRoom();
        }

        private void Contract_Click(
            object sender,
            RoutedEventArgs e)
        {
            txtBreadcrumb.Text = "Hệ thống / Hợp đồng thuê";
            SetSelectedMenu(btnContract);

            contentArea.Content = new HopDongThueView();
        }

        private void SetSelectedMenu(Button selected)
        {
            Button[] buttons = { btnRoom, btnContract, btnProfile };

            foreach (var button in buttons)
            {
                button.Background = Brushes.Transparent;
                button.Foreground = new SolidColorBrush(
                    Color.FromRgb(203, 213, 225));
            }

            selected.Background = new SolidColorBrush(
                Color.FromRgb(37, 99, 235));
            selected.Foreground = Brushes.White;
        }

        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            txtBreadcrumb.Text = "Hệ thống / Hồ sơ cá nhân";
            SetSelectedMenu(btnProfile);

            var view = new HoSoView();

            view.ProfileUpdated += (_, _) =>
            {
                txtUser.Text = UserSession.CurrentUser?.HoTen ?? "";
            };

            contentArea.Content = view;
        }

        private void Logout_Click(
            object sender,
            RoutedEventArgs e)
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