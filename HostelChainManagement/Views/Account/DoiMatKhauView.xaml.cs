using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Account;

namespace QuanLyChuoiNhaTro.Views.Account
{
    public partial class DoiMatKhauView : UserControl
    {
        private bool _busy;
        private bool _visible;

        public DoiMatKhauView()
        {
            InitializeComponent();

            Loaded += (_, _) => pwdCurrent.Focus();

            // Rời trang thì xóa thông tin đang nhập.
            Unloaded += (_, _) => ClearPasswords();
        }

        private string ReadPassword(
            PasswordBox hidden,
            TextBox visible)
        {
            return _visible
                ? visible.Text
                : hidden.Password;
        }

        private void TogglePassword(
            object sender,
            RoutedEventArgs e)
        {
            // Sự kiện có thể xảy ra khi đang tạo giao diện.
            if (pwdCurrent == null
                || pwdNew == null
                || pwdConfirm == null
                || txtCurrent == null
                || txtNew == null
                || txtConfirm == null)
            {
                return;
            }

            bool show = chkShow.IsChecked == true;

            if (show == _visible)
                return;

            ToggleField(pwdCurrent, txtCurrent, show);
            ToggleField(pwdNew, txtNew, show);
            ToggleField(pwdConfirm, txtConfirm, show);

            _visible = show;
        }

        private static void ToggleField(
            PasswordBox hidden,
            TextBox visible,
            bool show)
        {
            if (show)
            {
                visible.Text = hidden.Password;
                hidden.Clear();
            }
            else
            {
                hidden.Password = visible.Text;
                visible.Clear();
            }

            hidden.Visibility = show
                ? Visibility.Collapsed
                : Visibility.Visible;

            visible.Visibility = show
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ClearPasswords()
        {
            pwdCurrent.Clear();
            pwdNew.Clear();
            pwdConfirm.Clear();

            txtCurrent.Clear();
            txtNew.Clear();
            txtConfirm.Clear();

            chkShow.IsChecked = false;
        }

        private void Clear_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy)
                return;

            ClearPasswords();

            txtStatus.Text = "";

            pwdCurrent.Focus();
        }

        private async void Save_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy)
                return;

            string? userId =
                UserSession.CurrentUser?.MaNguoiDung;

            string current =
                ReadPassword(pwdCurrent, txtCurrent);

            string next =
                ReadPassword(pwdNew, txtNew);

            string confirm =
                ReadPassword(pwdConfirm, txtConfirm);

            _busy = true;
            editorPanel.IsEnabled = false;

            txtStatus.Text = "Đang đổi mật khẩu...";

            try
            {
                await DoiMatKhauService.ChangeAsync(
                    current,
                    next,
                    confirm);

                if (!IsLoaded
                    || UserSession.CurrentUser?.MaNguoiDung
                        != userId)
                {
                    return;
                }

                ClearPasswords();

                txtStatus.Text =
                    "Đổi mật khẩu thành công. " +
                    "Vui lòng sử dụng mật khẩu mới " +
                    "cho lần đăng nhập tiếp theo.";
            }
            catch (InvalidOperationException ex)
            {
                txtStatus.Text = ex.Message;
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không xác nhận được kết quả đổi mật khẩu. " +
                    "Vui lòng kiểm tra kết nối SQL Server.";
            }
            catch (Exception)
            {
                txtStatus.Text =
                    "Có lỗi khi đổi mật khẩu. Vui lòng thử lại.";
            }
            finally
            {
                _busy = false;
                editorPanel.IsEnabled = true;
            }
        }
    }
}