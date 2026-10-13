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

        private bool _isNewPasswordVisible;
        private bool _isConfirmPasswordVisible;

        public event Action? BackToIdentityRequested;
        public event Action? BackToLoginRequested;

        public ResetPasswordView(
            PasswordRecoveryService.RecoveryTicket ticket)
        {
            InitializeComponent();

            _ticket = ticket;

            Loaded += (_, _) => pwdNew.Focus();
        }

        private void ToggleNewPassword_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_isNewPasswordVisible)
            {
                txtNewVisible.Text = pwdNew.Password;

                pwdNew.Visibility = Visibility.Collapsed;
                txtNewVisible.Visibility = Visibility.Visible;

                txtNewVisible.Focus();
                txtNewVisible.CaretIndex = txtNewVisible.Text.Length;

                btnToggleNew.ToolTip = "Ẩn mật khẩu";

                _isNewPasswordVisible = true;
            }
            else
            {
                pwdNew.Password = txtNewVisible.Text;
                txtNewVisible.Clear();

                txtNewVisible.Visibility = Visibility.Collapsed;
                pwdNew.Visibility = Visibility.Visible;

                pwdNew.Focus();

                btnToggleNew.ToolTip = "Hiện mật khẩu";

                _isNewPasswordVisible = false;
            }
        }

        private void ToggleConfirmPassword_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_isConfirmPasswordVisible)
            {
                txtConfirmVisible.Text = pwdConfirm.Password;

                pwdConfirm.Visibility = Visibility.Collapsed;
                txtConfirmVisible.Visibility = Visibility.Visible;

                txtConfirmVisible.Focus();
                txtConfirmVisible.CaretIndex =
                    txtConfirmVisible.Text.Length;

                btnToggleConfirm.ToolTip = "Ẩn mật khẩu";

                _isConfirmPasswordVisible = true;
            }
            else
            {
                pwdConfirm.Password = txtConfirmVisible.Text;
                txtConfirmVisible.Clear();

                txtConfirmVisible.Visibility = Visibility.Collapsed;
                pwdConfirm.Visibility = Visibility.Visible;

                pwdConfirm.Focus();

                btnToggleConfirm.ToolTip = "Hiện mật khẩu";

                _isConfirmPasswordVisible = false;
            }
        }

        private string GetNewPassword()
        {
            return _isNewPasswordVisible
                ? txtNewVisible.Text
                : pwdNew.Password;
        }

        private string GetConfirmPassword()
        {
            return _isConfirmPasswordVisible
                ? txtConfirmVisible.Text
                : pwdConfirm.Password;
        }

        private void ClearPasswords()
        {
            pwdNew.Clear();
            pwdConfirm.Clear();

            txtNewVisible.Clear();
            txtConfirmVisible.Clear();

            txtNewVisible.Visibility = Visibility.Collapsed;
            txtConfirmVisible.Visibility = Visibility.Collapsed;

            pwdNew.Visibility = Visibility.Visible;
            pwdConfirm.Visibility = Visibility.Visible;

            _isNewPasswordVisible = false;
            _isConfirmPasswordVisible = false;

            btnToggleNew.ToolTip = "Hiện mật khẩu";
            btnToggleConfirm.ToolTip = "Hiện mật khẩu";
        }

        private async void Reset_Click(
            object sender,
            RoutedEventArgs e)
        {
            string password = GetNewPassword();
            string confirm = GetConfirmPassword();

            txtError.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Vui lòng nhập mật khẩu mới.");
                return;
            }

            if (string.IsNullOrWhiteSpace(confirm))
            {
                ShowError("Vui lòng xác nhận mật khẩu mới.");
                return;
            }

            if (password != confirm)
            {
                ShowError("Xác nhận mật khẩu không khớp.");
                return;
            }

            btnReset.IsEnabled = false;

            try
            {
                await PasswordRecoveryService.ResetPasswordAsync(
                    _ticket,
                    password,
                    confirm);

                ClearPasswords();

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