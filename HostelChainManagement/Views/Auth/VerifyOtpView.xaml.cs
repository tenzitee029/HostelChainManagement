using System;
using System.Windows;
using System.Windows.Controls;

namespace QuanLyChuoiNhaTro.Views.Auth
{
    public partial class VerifyOtpView : UserControl
    {
        public event Action? BackToEmailRequested;
        public event Action? BackToLoginRequested;

        public VerifyOtpView(string email)
        {
            InitializeComponent();

            txtEmailInfo.Text = "Email: " + email;

            Loaded += (_, _) => txtOtp.Focus();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Đây là bản xem trước giao diện. " +
                "Chức năng xác thực OTP và đặt lại mật khẩu chưa được triển khai.",
                "Quên mật khẩu",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void BackToEmail_Click(object sender, RoutedEventArgs e)
        {
            BackToEmailRequested?.Invoke();
        }

        private void BackToLogin_Click(object sender, RoutedEventArgs e)
        {
            BackToLoginRequested?.Invoke();
        }
    }
}