using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Admin;

namespace QuanLyChuoiNhaTro.Views.Admin
{
    public partial class QuanLyTaiKhoanView : UserControl
    {
        private DataTable? _accounts;
        private bool _busy;

        public bool IsBusy => _busy;

        public event Action? CreateRequested;

        private sealed record RoleSelection(
            string Role,
            string? Day);

        public QuanLyTaiKhoanView()
        {
            InitializeComponent();

            cboRoleFilter.ItemsSource =
                new[] { "Tất cả vai trò" }
                    .Concat(QuanLyTaiKhoanService.Roles)
                    .ToArray();

            cboRoleFilter.SelectedIndex = 0;
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
                {
                    txtStatus.Text = success;
                }
                else if (txtStatus.Text == "Đang xử lý...")
                {
                    txtStatus.Text = "";
                }
            }
            catch (SqlException ex)
            {
                txtStatus.Text = ex.Number switch
                {
                    50001 => ex.Message,

                    547 =>
                        "Không thực hiện được vì tài khoản còn "
                        + "liên kết với dữ liệu khác trong hệ thống.",

                    2601 or 2627 =>
                        "Thông tin tài khoản đã tồn tại.",

                    _ =>
                        "Không thực hiện được thao tác. "
                        + "Vui lòng kiểm tra SQL Server."
                };
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
            await RunAsync(LoadAccountsAsync);
        }

        private async Task LoadAccountsAsync()
        {
            var session = UserSession.CurrentUser;

            var accounts =
                await QuanLyTaiKhoanService.GetAccountsAsync();

            if (!IsLoaded
                || !ReferenceEquals(
                    session,
                    UserSession.CurrentUser))
            {
                return;
            }

            _accounts = accounts;

            ApplyAccountFilter();
        }

        private void ApplyAccountFilter()
        {
            if (_accounts == null
                || txtSearch == null
                || cboRoleFilter == null
                || cboStatus == null
                || accountsGrid == null
                || txtNoAccounts == null)
            {
                return;
            }

            string search = txtSearch.Text.Trim();

            string role =
                cboRoleFilter.SelectedItem as string
                ?? "Tất cả vai trò";

            string status =
                (cboStatus.SelectedItem as ComboBoxItem)?
                    .Content.ToString()
                ?? "Tất cả trạng thái";

            var rows = _accounts.DefaultView
                .Cast<DataRowView>()
                .Where(row =>
                    (row["TenDangNhap"].ToString() ?? "")
                        .Contains(
                            search,
                            StringComparison.OrdinalIgnoreCase)

                    && (role == "Tất cả vai trò"
                        || row["VaiTro"].ToString() == role)

                    && (status == "Tất cả trạng thái"
                        || row["TrangThai"].ToString() == status))
                .ToList();

            accountsGrid.ItemsSource = rows;

            txtNoAccounts.Visibility = rows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void AccountFilter_Changed(
            object sender,
            SelectionChangedEventArgs e)
        {
            ApplyAccountFilter();
        }

        private void AccountSearch_Changed(
            object sender,
            TextChangedEventArgs e)
        {
            ApplyAccountFilter();
        }

        private void NewAccount_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_busy)
            {
                CreateRequested?.Invoke();
            }
        }

        private bool Confirm(
            string message,
            string title,
            MessageBoxImage image = MessageBoxImage.Question)
        {
            var owner = Window.GetWindow(this);

            MessageBoxResult result = owner != null
                ? MessageBox.Show(
                    owner,
                    message,
                    title,
                    MessageBoxButton.YesNo,
                    image)
                : MessageBox.Show(
                    message,
                    title,
                    MessageBoxButton.YesNo,
                    image);

            return result == MessageBoxResult.Yes;
        }

        private async void DeleteAccount_Click(
    object sender,
    RoutedEventArgs e)
        {
            if (_busy
                || sender is not Button button
                || button.DataContext is not DataRowView row)
            {
                return;
            }

            string id =
                row["MaNguoiDung"].ToString() ?? "";

            string login =
                row["TenDangNhap"].ToString() ?? "";

            string status =
                row["TrangThai"].ToString() ?? "";

            if (id == UserSession.CurrentUser?.MaNguoiDung)
            {
                txtStatus.Text =
                    "Không thể xóa tài khoản Admin đang đăng nhập.";

                return;
            }

            if (status is not ("Hoạt động" or "Khóa"))
            {
                txtStatus.Text =
                    "Tài khoản đã ngừng sử dụng. "
                    + "Vui lòng tải lại danh sách.";

                return;
            }

            string message =
                $"Bạn có chắc chắn muốn xóa quyền sử dụng "
                + $"của tài khoản {login}?\n\n"
                + "• Tài khoản sẽ chuyển sang Ngừng sử dụng.\n"
                + "• Không thể đăng nhập lại hoặc mở khóa "
                + "bằng chức năng Khóa / Mở khóa.\n"
                + "• Nếu đang đăng nhập, người dùng sẽ được "
                + "đưa về màn hình đăng nhập sau lần kiểm tra phiên.\n"
                + "• Hợp đồng, hóa đơn, công việc và lịch sử "
                + "nghiệp vụ được giữ nguyên.";

            if (!Confirm(
                message,
                "Xác nhận xóa tài khoản",
                MessageBoxImage.Warning))
            {
                return;
            }

            await RunAsync(async () =>
            {
                await QuanLyTaiKhoanService.DeleteAccountAsync(
                    id,
                    status);

                await LoadAccountsAsync();

                txtStatus.Text =
                    $"Đã xóa quyền sử dụng tài khoản {login}. "
                    + "Dữ liệu nghiệp vụ được giữ nguyên.";
            });
        }

        private async void ToggleStatus_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy
                || sender is not Button button
                || button.DataContext is not DataRowView row)
            {
                return;
            }

            string id = row["MaNguoiDung"].ToString()!;
            string login = row["TenDangNhap"].ToString()!;
            string oldStatus = row["TrangThai"].ToString()!;

            if (id == UserSession.CurrentUser?.MaNguoiDung)
            {
                txtStatus.Text =
                    "Không thể tự vô hiệu hóa tài khoản "
                    + "quản trị đang đăng nhập.";

                return;
            }

            if (oldStatus is not ("Hoạt động" or "Khóa"))
            {
                txtStatus.Text =
                    "Tài khoản không ở trạng thái cho phép "
                    + "khóa hoặc mở khóa.";

                return;
            }

            string nextStatus = oldStatus == "Hoạt động"
                ? "Khóa"
                : "Hoạt động";

            string action = nextStatus == "Khóa"
                ? "Khóa"
                : "Mở khóa";

            if (!Confirm(
                $"{action} tài khoản {login}?",
                "Xác nhận"))
            {
                return;
            }

            await RunAsync(
                async () =>
                {
                    await QuanLyTaiKhoanService.SetStatusAsync(
                        id,
                        oldStatus,
                        nextStatus);

                    await LoadAccountsAsync();
                },
                "Cập nhật trạng thái tài khoản thành công.");
        }

        private RoleSelection? PromptRole(
            DataTable days,
            string currentRole)
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            var roles = new ComboBox
            {
                ItemsSource = QuanLyTaiKhoanService.Roles,
                SelectedItem = currentRole,
                MinHeight = 34,
                Margin = new Thickness(0, 6, 0, 12)
            };

            var day = new ComboBox
            {
                ItemsSource = days.DefaultView,
                DisplayMemberPath = "TenDayTro",
                SelectedValuePath = "MaDayTro",
                MinHeight = 34,
                Margin = new Thickness(0, 6, 0, 12)
            };

            panel.Children.Add(new TextBlock
            {
                Text = "Vai trò mới"
            });

            panel.Children.Add(roles);

            panel.Children.Add(new TextBlock
            {
                Text = "Dãy trọ — bắt buộc với Nhân viên quản lý",
                TextWrapping = TextWrapping.Wrap
            });

            panel.Children.Add(day);

            var status = new TextBlock
            {
                Foreground = System.Windows.Media.Brushes.DarkOrange,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };

            panel.Children.Add(status);

            var button = new Button
            {
                Content = "Xác nhận",
                Padding = new Thickness(14, 8, 14, 8),
                HorizontalAlignment = HorizontalAlignment.Right,
                IsDefault = true
            };

            panel.Children.Add(button);

            var dialog = new Window
            {
                Owner = Window.GetWindow(this),
                Title = "Cập nhật vai trò",
                Width = 460,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Content = panel
            };

            button.Click += (_, _) =>
            {
                if (roles.SelectedItem is not string role)
                {
                    status.Text = "Vui lòng chọn vai trò.";
                    return;
                }

                if (role != currentRole
                    && role == "Nhân viên quản lý"
                    && day.SelectedValue == null)
                {
                    status.Text =
                        "Vui lòng chọn dãy trọ cho nhân viên quản lý.";

                    return;
                }

                dialog.DialogResult = true;
            };

            if (dialog.ShowDialog() != true
                || roles.SelectedItem is not string selectedRole)
            {
                return null;
            }

            bool employee =
                selectedRole == "Nhân viên quản lý"
                || selectedRole == "Nhân viên kỹ thuật";

            return new RoleSelection(
                selectedRole,
                employee ? day.SelectedValue as string : null);
        }

        private async void ChangeRole_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy)
                return;

            if (accountsGrid.SelectedItem is not DataRowView row)
            {
                txtStatus.Text =
                    "Vui lòng chọn tài khoản cần cập nhật vai trò.";

                return;
            }

            string id = row["MaNguoiDung"].ToString()!;
            string oldRole = row["VaiTro"].ToString()!;

            await RunAsync(async () =>
            {
                var days =
                    await QuanLyTaiKhoanService.GetDaysAsync();

                var selection = PromptRole(days, oldRole);

                if (selection == null)
                    return;

                if (selection.Role == oldRole)
                {
                    txtStatus.Text = "Vai trò không thay đổi.";
                    return;
                }

                await QuanLyTaiKhoanService.SetRoleAsync(
                    id,
                    oldRole,
                    selection.Role,
                    selection.Day);

                await LoadAccountsAsync();

                txtStatus.Text = "Đã cập nhật vai trò.";
            });
        }
    }
}