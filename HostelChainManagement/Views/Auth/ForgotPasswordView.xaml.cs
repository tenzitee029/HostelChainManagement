using System;
using System.Windows;
using System.Windows.Controls;

namespace QuanLyChuoiNhaTro.Views.Auth
{
    public partial class ForgotPasswordView : UserControl
    {
        public event Action<string>? SendCodeRequested;
        public event Action? BackToLoginRequested;

        public ForgotPasswordView(string email = "")
        {
            InitializeComponent();

            txtEmail.Text = email;

            Loaded += (_, _) => txtEmail.Focus();
        }

        private void SendCode_Click(object sender, RoutedEventArgs e)
        {
            string email = txtEmail.Text.Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                txtError.Text = "Vui lòng nhập email đã đăng ký.";
                txtError.Visibility = Visibility.Visible;
                txtEmail.Focus();
                return;
            }

            if (!email.Contains('@') ||
                email.StartsWith('@') ||
                email.EndsWith('@'))
            {
                txtError.Text = "Email chưa đúng định dạng.";
                txtError.Visibility = Visibility.Visible;
                txtEmail.Focus();
                return;
            }

            txtError.Text = string.Empty;
            txtError.Visibility = Visibility.Collapsed;

            SendCodeRequested?.Invoke(email);
        }

        private void BackToLogin_Click(object sender, RoutedEventArgs e)
        {
            BackToLoginRequested?.Invoke();
        }
    }
}