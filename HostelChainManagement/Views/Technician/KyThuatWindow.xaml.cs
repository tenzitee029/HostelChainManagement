using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Views.Account;
using QuanLyChuoiNhaTro.Views.Auth;

namespace QuanLyChuoiNhaTro.Views.Technician
{
    public partial class KyThuatWindow : Window
    {
        public KyThuatWindow()
        {
            InitializeComponent();

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên kỹ thuật")
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền mở giao diện Nhân viên kỹ thuật.");
            }

            txtUser.Text = user.HoTen;

            ShowJobs();
        }

        private bool CanNavigate => contentArea.Content switch
        {
            DanhSachCongViecView view => !view.IsBusy,
            ChiTietCongViecView view => !view.IsBusy,
            XacNhanHoanThanhView view => !view.IsBusy,
            ThongBaoKyThuatView view => !view.IsBusy,
            _ => true
        };

        private void ShowView(
            UserControl view,
            string title,
            Button? selectedButton = null)
        {
            foreach (var button in new[]
            {
                btnJobs,
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

        private void ShowJobs()
        {
            var view = new DanhSachCongViecView();

            view.WorkSelected += ShowDetail;

            ShowView(
                view,
                "Danh sách công việc",
                btnJobs);
        }

        private void ShowDetail(string id)
        {
            var view = new ChiTietCongViecView(id);

            view.ListRequested += ShowJobs;

            view.CompleteRequested += ShowCompletion;

            ShowView(
                view,
                "Danh sách công việc / Chi tiết công việc",
                btnJobs);
        }

        private void ShowCompletion(string id)
        {
            var view = new XacNhanHoanThanhView(id);

            view.Completed += ShowDetail;

            ShowView(
                view,
                "Danh sách công việc / Xác nhận hoàn thành sửa chữa",
                btnJobs);
        }

        private void Jobs_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!CanNavigate)
                return;

            ShowJobs();
        }

        private void Notifications_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!CanNavigate)
                return;

            ShowView(
                new ThongBaoKyThuatView(),
                "Thông báo");
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

            UserSession.SignOut();

            var loginWindow = new LoginWindow();

            Application.Current.MainWindow = loginWindow;

            loginWindow.Show();

            Close();
        }
    }
}