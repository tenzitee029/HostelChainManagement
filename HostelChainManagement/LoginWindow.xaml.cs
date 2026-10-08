using QuanLyChuoiNhaTro.Services;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace QuanLyChuoiNhaTro
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => txtAccount.Focus();
        }

        private async void Login_Click(
            object sender, RoutedEventArgs e)
        {
            btnLogin.IsEnabled = false;
            btnForgotPassword.IsEnabled = false;
            txtMessage.Text = "Đang đăng nhập...";

            try
            {
                var user = await AuthService.LoginAsync(
                    txtAccount.Text, txtPassword.Password);

                // Người dùng có thể đã đóng cửa sổ khi đang chờ SQL.
                if (!IsVisible)
                    return;

                UserSession.SignIn(user);

                var dashboard = new DashboardWindow();
                Application.Current.MainWindow = dashboard;

                dashboard.Show();

                MessageBox.Show(
                    dashboard,
                    "Đăng nhập thành công.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Close();
            }
            catch (InvalidOperationException ex)
            {
                txtMessage.Text = ex.Message;
                txtPassword.Clear();
            }
            catch (SqlException)
            {
                txtMessage.Text =
                    "Không thể truy cập cơ sở dữ liệu. " +
                    "Vui lòng kiểm tra SQL Server và cấu hình kết nối.";
            }
            catch (Exception)
            {
                txtMessage.Text =
                    "Có lỗi khi đăng nhập. Vui lòng thử lại.";
            }
            finally
            {
                btnLogin.IsEnabled = true;
                btnForgotPassword.IsEnabled = true;
            }
        }

        private bool _isPasswordVisible;
        private void TogglePassword_Click(
            object sender, RoutedEventArgs e)
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

        private void ForgotPassword_Click(
            object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Chức năng khôi phục mật khẩu qua OTP " +
                "sẽ được triển khai ở bước tiếp theo.",
                "Quên mật khẩu",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
