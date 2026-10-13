using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Views.Admin;
using QuanLyChuoiNhaTro.Views.Owner;
using QuanLyChuoiNhaTro.Views.Management;
using QuanLyChuoiNhaTro.Views.Tenant;
using QuanLyChuoiNhaTro.Views.Technician;

namespace QuanLyChuoiNhaTro.Views.Auth
{
    public partial class LoginWindow : Window
    {
        private bool _isPasswordVisible;

        public LoginWindow()
        {
            InitializeComponent();

            Loaded += (_, _) => txtAccount.Focus();
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            string account = txtAccount.Text.Trim();
            string password = GetPassword();

            if (string.IsNullOrWhiteSpace(account)
                || string.IsNullOrEmpty(password))
            {
                txtMessage.Text =
                    "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.";
                return;
            }

            SetBusy(true);
            txtMessage.Text = "Đang đăng nhập...";

            Window? destination = null;

            try
            {
                var user = await AuthService.LoginAsync(
                    account, password);

                if (!IsVisible)
                    return;

                UserSession.SignIn(user);

                destination = CreateRoleWindow(user.VaiTro);

                Application.Current.MainWindow = destination;
                destination.Show();

                ClearPassword();
                Close();
            }
            catch (InvalidOperationException ex)
            {
                ResetFailedNavigation(destination);
                txtMessage.Text = ex.Message;
                ClearPassword();
            }
            catch (SqlException)
            {
                ResetFailedNavigation(destination);

                txtMessage.Text =
                    "Không thể truy cập cơ sở dữ liệu. " +
                    "Vui lòng kiểm tra SQL Server và cấu hình kết nối.";
            }
            catch (Exception ex)
            {
                ResetFailedNavigation(destination);

                txtMessage.Text =
                    "Không thể mở giao diện tài khoản.";

                MessageBox.Show(
                    this,
                    ex.Message,
                    "Lỗi mở giao diện",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private static Window CreateRoleWindow(string role)
        {
            return role switch
            {
                "Admin" =>
                    new AdminWindow(),

                "Chủ nhà trọ" =>
                    new ChuTroWindow(),

                "Nhân viên quản lý" =>
                    new QuanLyWindow(),

                "Khách thuê" =>
                    new KhachThueWindow(),

                "Nhân viên kỹ thuật" =>
                    new KyThuatWindow(),

                _ => throw new InvalidOperationException(
                    "Vai trò tài khoản không hợp lệ.")
            };
        }

        private void ResetFailedNavigation(Window? destination)
        {
            UserSession.SignOut();

            Application.Current.MainWindow = this;

            if (destination != null)
                destination.Close();
        }

        private void SetBusy(bool busy)
        {
            btnLogin.IsEnabled = !busy;
            btnForgotPassword.IsEnabled = !busy;
            btnTogglePassword.IsEnabled = !busy;
            txtAccount.IsEnabled = !busy;
            txtPassword.IsEnabled = !busy;
            txtVisiblePassword.IsEnabled = !busy;
        }

        private string GetPassword()
        {
            return _isPasswordVisible
                ? txtVisiblePassword.Text
                : txtPassword.Password;
        }

        private void ClearPassword()
        {
            txtPassword.Clear();
            txtVisiblePassword.Clear();
        }

        private void TogglePassword_Click(object sender, RoutedEventArgs e)
        {
            if (!_isPasswordVisible)
            {
                txtVisiblePassword.Text = txtPassword.Password;

                txtPassword.Visibility = Visibility.Collapsed;
                txtVisiblePassword.Visibility = Visibility.Visible;

                txtVisiblePassword.Focus();
                txtVisiblePassword.CaretIndex =
                    txtVisiblePassword.Text.Length;

                btnTogglePassword.ToolTip = "Ẩn mật khẩu";
            }
            else
            {
                txtPassword.Password = txtVisiblePassword.Text;
                txtVisiblePassword.Clear();

                txtVisiblePassword.Visibility = Visibility.Collapsed;
                txtPassword.Visibility = Visibility.Visible;

                txtPassword.Focus();
                btnTogglePassword.ToolTip = "Hiện mật khẩu";
            }

            _isPasswordVisible = !_isPasswordVisible;
        }

        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            ShowForgotPassword();
        }

        private void ShowForgotPassword(string email = "")
        {
            var view = new ForgotPasswordView(email);

            view.SendCodeRequested += submittedEmail =>
            {
                ShowVerifyOtp(submittedEmail);
            };

            view.BackToLoginRequested += ShowLoginForm;

            ShowAuthView(view);
        }

        private void ShowVerifyOtp(string email)
        {
            var view = new VerifyOtpView(email);

            view.BackToEmailRequested += () =>
            {
                ShowForgotPassword(email);
            };

            view.BackToLoginRequested += ShowLoginForm;

            ShowAuthView(view);
        }

        private void ShowAuthView(UserControl view)
        {
            LoginFormBorder.Visibility = Visibility.Collapsed;

            AuthContentHost.Content = view;
            AuthContentHost.Visibility = Visibility.Visible;
        }

        private void ShowLoginForm()
        {
            AuthContentHost.Content = null;
            AuthContentHost.Visibility = Visibility.Collapsed;

            LoginFormBorder.Visibility = Visibility.Visible;

            txtMessage.Text = string.Empty;
            txtAccount.Focus();
        }
    }
}