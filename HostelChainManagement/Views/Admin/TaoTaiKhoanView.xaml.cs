using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Admin
{
    public partial class TaoTaiKhoanView : UserControl
    {
        private bool _busy;
        private string? _pendingEmailId;

        public bool IsBusy => _busy;

        public TaoTaiKhoanView()
        {
            InitializeComponent();

            cboNewRole.ItemsSource = AdminService.Roles;
            cboNewRole.SelectedIndex = -1;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            editorPanel.IsEnabled = !busy;
        }

        private async Task RunAsync(
            Func<Task> action,
            string? success = null)
        {
            if (_busy)
                return;

            SetBusy(true);
            txtStatus.Text = "Đang xử lý...";

            try
            {
                await action();

                if (success != null)
                    txtStatus.Text = success;
                else if (txtStatus.Text == "Đang xử lý...")
                    txtStatus.Text = "";
            }
            catch (SqlException ex)
            {
                txtStatus.Text = ex.Number is 2601 or 2627
                    ? "Tên đăng nhập, CCCD hoặc số điện thoại đã tồn tại."
                    : ex.Number == 50001
                        ? ex.Message
                        : "Không tạo được tài khoản. "
                            + "Vui lòng kiểm tra SQL Server.";
            }
            catch (Exception ex)
            {
                txtStatus.Text = ex.Message;
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void CreateAccount_Click(
            object sender, RoutedEventArgs e)
        {
            if (_busy)
                return;

            if (_pendingEmailId != null)
            {
                txtStatus.Text =
                    "Tài khoản trước đã được tạo. "
                    + "Hãy gửi lại email trước khi tạo tiếp.";
                return;
            }

            if (cboNewRole.SelectedItem is not string role)
            {
                txtStatus.Text = "Vui lòng chọn vai trò.";
                return;
            }

            await RunAsync(async () =>
            {
                var info = AdminProfileDialog.Show(
                    Window.GetWindow(this),
                    await AdminService.GetDaysAsync(),
                    role,
                    true);

                if (info == null)
                    return;

                var result =
                    await AdminService.CreateAccountAsync(
                        txtNewLogin.Text,
                        txtNewEmail.Text,
                        txtNewPhone.Text,
                        role,
                        info.Name,
                        info.Cccd,
                        info.Day);

                if (result.EmailSent)
                {
                    txtStatus.Text =
                        "Đã tạo tài khoản và gửi mật khẩu về email.";

                    ClearForm();
                }
                else
                {
                    _pendingEmailId = result.Id;

                    btnRetryEmail.Visibility =
                        Visibility.Visible;

                    txtStatus.Text =
                        "Tài khoản đã được tạo nhưng email chưa gửi được. "
                        + "Kiểm tra SMTP rồi bấm Gửi lại email; "
                        + "không tạo lại tài khoản.";
                }
            });
        }

        private void ClearForm()
        {
            txtNewLogin.Clear();
            txtNewEmail.Clear();
            txtNewPhone.Clear();
        }

        private async void RetryEmail_Click(
            object sender, RoutedEventArgs e)
        {
            if (_pendingEmailId == null || _busy)
                return;

            await RunAsync(async () =>
            {
                await AdminService.ResendEmailAsync(
                    _pendingEmailId);

                _pendingEmailId = null;

                btnRetryEmail.Visibility =
                    Visibility.Collapsed;

                ClearForm();
            }, "Đã gửi lại email cấp mật khẩu.");
        }
    }
}