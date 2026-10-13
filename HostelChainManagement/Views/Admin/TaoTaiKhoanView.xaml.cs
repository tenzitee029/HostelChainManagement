using System;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
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
        private bool _daysLoaded;
        private string? _pendingEmailId;

        public bool IsBusy => _busy;

        public TaoTaiKhoanView()
        {
            InitializeComponent();

            cboNewRole.ItemsSource = AdminService.Roles;
            cboNewRole.SelectedIndex = -1;

            UpdateControls();
        }

        private void UpdateControls()
        {
            editorPanel.IsEnabled = !_busy;

            bool pending = _pendingEmailId != null;

            accountFields.IsEnabled = !pending;
            btnCreate.IsEnabled = !pending;

            btnRetryEmail.Visibility = pending
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            UpdateControls();
        }

        private async Task RunAsync(Func<Task> action)
        {
            if (_busy)
                return;

            SetBusy(true);
            txtStatus.Text = "Đang xử lý...";

            try
            {
                await action();

                if (txtStatus.Text == "Đang xử lý...")
                {
                    txtStatus.Text = "";
                }
            }
            catch (SqlException ex)
            {
                txtStatus.Text = ex.Number switch
                {
                    2601 or 2627 =>
                        "Tên đăng nhập, CCCD hoặc số điện thoại "
                        + "đã tồn tại.",

                    50001 => ex.Message,

                    547 =>
                        "Dữ liệu liên kết không hợp lệ. "
                        + "Vui lòng kiểm tra dãy trọ đã chọn.",

                    _ =>
                        "Không thực hiện được thao tác. "
                        + "Vui lòng kiểm tra SQL Server."
                };
            }
            catch (SmtpException)
            {
                txtStatus.Text =
                    "Không gửi được email. Kiểm tra cấu hình SMTP, "
                    + "tài khoản gửi và kết nối mạng.";
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

        private async void View_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy || _daysLoaded)
                return;

            await RunAsync(async () =>
            {
                var session = UserSession.CurrentUser;

                var days = await AdminService.GetDaysAsync();

                if (!IsLoaded
                    || !ReferenceEquals(
                        session,
                        UserSession.CurrentUser))
                {
                    return;
                }

                cboDay.ItemsSource = days.DefaultView;
                _daysLoaded = true;
            });
        }

        private void Role_Changed(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (dayPanel == null
                || cboDay == null
                || txtDayLabel == null)
            {
                return;
            }

            string role =
                cboNewRole.SelectedItem as string ?? "";

            bool employee =
                role == "Nhân viên quản lý"
                || role == "Nhân viên kỹ thuật";

            dayPanel.Visibility = employee
                ? Visibility.Visible
                : Visibility.Collapsed;

            txtDayLabel.Text = role == "Nhân viên quản lý"
                ? "Dãy trọ (bắt buộc)"
                : "Dãy trọ (không bắt buộc)";

            cboDay.SelectedIndex = -1;
        }

        private static bool IsValidEmail(string email)
        {
            if (email.Length > 100
                || email.Any(character => character > 127))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    new MailAddress(email).Address,
                    email,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private bool ValidateInput(
            string fullName,
            string cccd,
            string login,
            string email,
            string phone,
            string role,
            string? day)
        {
            if (string.IsNullOrWhiteSpace(fullName)
                || fullName.Length > 100)
            {
                txtStatus.Text =
                    "Nhập họ và tên, tối đa 100 ký tự.";

                txtFullName.Focus();
                return false;
            }

            if (!Regex.IsMatch(cccd, @"^[0-9]{12}$"))
            {
                txtStatus.Text = "CCCD phải có đúng 12 chữ số.";
                txtCccd.Focus();
                return false;
            }

            if (!Regex.IsMatch(login, @"^[A-Za-z0-9_.-]{3,50}$"))
            {
                txtStatus.Text =
                    "Tên đăng nhập cần 3–50 ký tự chữ, số, "
                    + "dấu _, . hoặc -.";

                txtNewLogin.Focus();
                return false;
            }

            if (!string.IsNullOrWhiteSpace(email)
                && !IsValidEmail(email))
            {
                txtStatus.Text =
                    "Email không hợp lệ. Bạn có thể để trống.";

                txtNewEmail.Focus();
                return false;
            }

            if (!Regex.IsMatch(phone, @"^[0-9]{9,15}$"))
            {
                txtStatus.Text = "SĐT cần 9–15 chữ số.";
                txtNewPhone.Focus();
                return false;
            }

            if (!AdminService.Roles.Contains(role))
            {
                txtStatus.Text = "Vui lòng chọn vai trò.";
                cboNewRole.Focus();
                return false;
            }

            if (role == "Nhân viên quản lý"
                && string.IsNullOrWhiteSpace(day))
            {
                txtStatus.Text =
                    "Vui lòng chọn dãy trọ cho nhân viên quản lý.";

                cboDay.Focus();
                return false;
            }

            return true;
        }

        private void ClearForm()
        {
            txtFullName.Clear();
            txtCccd.Clear();
            txtNewLogin.Clear();
            txtNewEmail.Clear();
            txtNewPhone.Clear();

            cboNewRole.SelectedIndex = -1;
            cboDay.SelectedIndex = -1;

            dayPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowMessage(
            string message,
            string title,
            MessageBoxImage image)
        {
            var owner = Window.GetWindow(this);

            if (owner != null)
            {
                MessageBox.Show(
                    owner,
                    message,
                    title,
                    MessageBoxButton.OK,
                    image);
            }
            else
            {
                MessageBox.Show(
                    message,
                    title,
                    MessageBoxButton.OK,
                    image);
            }
        }

        private async void CreateAccount_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy || _pendingEmailId != null)
                return;

            string fullName = txtFullName.Text.Trim();
            string cccd = txtCccd.Text.Trim();
            string login = txtNewLogin.Text.Trim();
            string email = txtNewEmail.Text.Trim();
            string phone = txtNewPhone.Text.Trim();

            string role =
                cboNewRole.SelectedItem as string ?? "";

            bool employee =
                role == "Nhân viên quản lý"
                || role == "Nhân viên kỹ thuật";

            string? day = employee
                ? cboDay.SelectedValue as string
                : null;

            if (!ValidateInput(
                fullName,
                cccd,
                login,
                email,
                phone,
                role,
                day))
            {
                return;
            }

            await RunAsync(async () =>
            {
                // Đúng 7 tham số của Service hiện tại.
                var result = await AdminService.CreateAccountAsync(
                    login,
                    email,
                    phone,
                    role,
                    fullName,
                    cccd,
                    day);

                if (result.InitialPassword != null)
                {
                    _pendingEmailId = null;
                    ClearForm();

                    txtStatus.Text =
                        $"Đã tạo tài khoản {login} thành công.";

                    ShowMessage(
                        "Tài khoản đã được tạo thành công.\n\n"
                        + $"Tên đăng nhập: {login}\n"
                        + $"Mật khẩu ban đầu: {result.InitialPassword}\n\n"
                        + "Vui lòng ghi lại và chuyển thông tin cho người dùng. "
                        + "Yêu cầu đổi mật khẩu sau khi đăng nhập.",
                        "Tạo tài khoản thành công",
                        MessageBoxImage.Information);
                }
                else if (result.EmailSent)
                {
                    _pendingEmailId = null;
                    ClearForm();

                    txtStatus.Text =
                        $"Đã tạo tài khoản {login} thành công. "
                        + $"Thông tin đăng nhập đã gửi đến {email}.";

                    ShowMessage(
                        txtStatus.Text,
                        "Tạo tài khoản thành công",
                        MessageBoxImage.Information);
                }
                else
                {
                    _pendingEmailId = result.Id;

                    txtStatus.Text =
                        $"Đã tạo tài khoản {login}, "
                        + "nhưng email chưa gửi được. "
                        + "Kiểm tra SMTP rồi bấm Gửi lại email. "
                        + "Không tạo lại tài khoản.";
                }
            });
        }

        private async void RetryEmail_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy || _pendingEmailId == null)
                return;

            string id = _pendingEmailId;

            await RunAsync(async () =>
            {
                await AdminService.ResendEmailAsync(id);

                _pendingEmailId = null;
                ClearForm();

                txtStatus.Text =
                    "Đã gửi email thành công. "
                    + "Bạn có thể tạo tài khoản tiếp theo.";
            });
        }
    }
}