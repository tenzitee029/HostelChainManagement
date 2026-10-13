using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Auth
{
    public partial class ForgotPasswordView : UserControl
    {
        public event Action<PasswordRecoveryService.RecoveryTicket>?
            IdentityVerified;

        public event Action? BackToLoginRequested;

        public ForgotPasswordView()
        {
            InitializeComponent();
            Loaded += (_, _) => txtCccd.Focus();
        }

        private async void Verify_Click(
            object sender,
            RoutedEventArgs e)
        {
            string cccd = txtCccd.Text.Trim();
            string phone = txtPhone.Text.Trim();

            txtError.Visibility = Visibility.Collapsed;

            if (cccd.Length != 12 ||
                cccd.Any(c => c < '0' || c > '9'))
            {
                ShowError("CCCD phải gồm đúng 12 chữ số.");
                txtCccd.Focus();
                return;
            }

            if ((phone.Length != 10 && phone.Length != 11) ||
                phone.Any(c => c < '0' || c > '9'))
            {
                ShowError("Số điện thoại phải gồm 10 hoặc 11 chữ số.");
                txtPhone.Focus();
                return;
            }

            btnVerify.IsEnabled = false;

            try
            {
                var ticket =
                    await PasswordRecoveryService.VerifyIdentityAsync(
                        cccd, phone);

                IdentityVerified?.Invoke(ticket);
            }
            catch (InvalidOperationException ex)
            {
                ShowError(ex.Message);
            }
            catch (SqlException)
            {
                ShowError(
                    "Không thể kiểm tra thông tin trên SQL Server.");
            }
            catch (Exception)
            {
                ShowError(
                    "Có lỗi xảy ra. Vui lòng thử lại.");
            }
            finally
            {
                btnVerify.IsEnabled = true;
            }
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