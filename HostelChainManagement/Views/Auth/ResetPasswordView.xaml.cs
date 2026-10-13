using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Auth
{
    public partial class ResetPasswordView : UserControl
    {
        private readonly PasswordRecoveryService.RecoveryTicket _ticket;

        public event Action? BackToIdentityRequested;
        public event Action? BackToLoginRequested;

        public ResetPasswordView(
            PasswordRecoveryService.RecoveryTicket ticket)
        {
            InitializeComponent();

            _ticket = ticket;

            Loaded += (_, _) => pwdNew.Focus();
        }

        private async void Reset_Click(
            object sender,
            RoutedEventArgs e)
        {
            string password = pwdNew.Password;
            string confirm = pwdConfirm.Password;

            txtError.Visibility = Visibility.Collapsed;

            if (password != confirm)
            {
                ShowError("Xác nhận mật khẩu không khớp.");
                return;
            }

            btnReset.IsEnabled = false;

            try
            {
                await PasswordRecoveryService.ResetPasswordAsync(
                    _ticket, password, confirm);

                pwdNew.Clear();
                pwdConfirm.Clear();

                MessageBox.Show(
                    "Đặt lại mật khẩu thành công. " +
                    "Hãy đăng nhập bằng mật khẩu mới.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                BackToLoginRequested?.Invoke();
            }
            catch (InvalidOperationException ex)
            {
                ShowError(ex.Message);
            }
            catch (SqlException)
            {
                ShowError(
                    "Không thể cập nhật mật khẩu trên SQL Server.");
            }
            catch (Exception)
            {
                ShowError(
                    "Đã xảy ra lỗi. Vui lòng thử lại.");
            }
            finally
            {
                btnReset.IsEnabled = true;
            }
        }

        private void BackToIdentity_Click(
            object sender,
            RoutedEventArgs e)
        {
            BackToIdentityRequested?.Invoke();
        }

        private void BackToLogin_Click(
            object sender,
            RoutedEventArgs e)
        {
            BackToLoginRequested?.Invoke();
        }

        private void ShowError(string message)
        {
            txtError.Text = message;
            txtError.Visibility = Visibility.Visible;
        }
    }
}