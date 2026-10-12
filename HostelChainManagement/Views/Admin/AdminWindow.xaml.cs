using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Views.Account;
using QuanLyChuoiNhaTro.Views.Auth;

namespace QuanLyChuoiNhaTro.Views.Admin;

public partial class AdminWindow : Window
{
    private DataTable? _accounts;
    private DataTable? _logs;

    private bool _busy;
    private string? _task;
    private string? _pendingEmailId;

    public AdminWindow()
    {
        InitializeComponent();

        var user = UserSession.CurrentUser;

        if (user == null || user.VaiTro != "Admin")
        {
            throw new InvalidOperationException(
                "Bạn không có quyền mở màn hình Admin.");
        }

        txtUser.Text = user.HoTen;

        cboRoleFilter.ItemsSource =
            new[] { "Tất cả vai trò" }
                .Concat(AdminService.Roles)
                .ToArray();

        cboRoleFilter.SelectedIndex = 0;

        cboNewRole.ItemsSource = AdminService.Roles;
        cboNewRole.SelectedIndex = -1;
    }

    private async Task RunAsync(
        Func<Task> action,
        string? success = null)
    {
        if (_busy)
            return;

        _busy = true;

        contentPanel.IsEnabled =
            navPanel.IsEnabled =
            btnBell.IsEnabled = false;

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
            txtStatus.Text =
                ex.Number is 2601 or 2627
                    ? "Tên đăng nhập, CCCD hoặc số điện thoại đã tồn tại."
                    : ex.Number == 50001
                        ? ex.Message
                        : "Không thực hiện được thao tác SQL. "
                            + "Kiểm tra kết nối, quyền SQL Server "
                            + "và đường dẫn trên máy chủ.";

            await SystemLogService.TryWriteAsync(
                UserSession.CurrentUser?.MaNguoiDung,
                "Log lỗi runtime",
                $"Lỗi SQL {ex.Number} tại phân hệ Admin.");
        }
        catch (Exception ex)
        {
            txtStatus.Text = ex.Message;

            await SystemLogService.TryWriteAsync(
                UserSession.CurrentUser?.MaNguoiDung,
                "Log lỗi runtime",
                "Lỗi phân hệ Admin: "
                    + ex.GetType().Name);
        }
        finally
        {
            _busy = false;

            contentPanel.IsEnabled =
                navPanel.IsEnabled =
                btnBell.IsEnabled = true;
        }
    }

    private void ShowPage(
        UIElement page,
        string title,
        Button menu)
    {
        foreach (var panel in new UIElement[]
        {
            maintenancePanel,
            logsPanel,
            accountsPanel,
            createPanel,
            profilePanel,
            passwordPanel
        })
        {
            panel.Visibility = Visibility.Collapsed;
        }

        page.Visibility = Visibility.Visible;

        txtBreadcrumb.Text =
            "Hệ thống / " + title;

        txtStatus.Text = "";

        foreach (var button in new[]
        {
            btnMaintenance,
            btnAccounts,
            btnProfile,
            btnPassword
        })
        {
            button.Background = Brushes.Transparent;

            button.Foreground = new SolidColorBrush(
                Color.FromRgb(203, 213, 225));
        }

        menu.Background = new SolidColorBrush(
            Color.FromRgb(37, 99, 235));

        menu.Foreground = Brushes.White;
    }

    private async void Window_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            // Kiểm tra quyền Admin trong CSDL.
            await AdminService.GetAccountsAsync();
        });
    }

    private void Maintenance_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!_busy)
        {
            ShowPage(
                maintenancePanel,
                "Bảo trì hệ thống",
                btnMaintenance);
        }
    }

    private void Task_Checked(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is RadioButton radio)
        {
            _task = radio.Tag.ToString();

            if (txtTask != null)
            {
                txtTask.Text =
                    radio.Content.ToString();
            }
        }
    }

    private string? PromptPath(
        string title,
        string initial)
    {
        var input = new TextBox
        {
            Text = initial,
            Padding = new Thickness(8),
            Margin = new Thickness(0, 12, 0, 12)
        };

        var stack = new StackPanel
        {
            Margin = new Thickness(20)
        };

        stack.Children.Add(new TextBlock
        {
            Text = "Nhập đường dẫn .bak trên máy chủ SQL Server. "
                + "Thư mục phải tồn tại và dịch vụ SQL Server "
                + "phải có quyền truy cập.",
            TextWrapping = TextWrapping.Wrap
        });

        stack.Children.Add(input);

        var ok = new Button
        {
            Content = "Xác nhận",
            Padding = Padding = new Thickness(14, 8, 14, 8),
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true
        };

        stack.Children.Add(ok);

        var dialog = new Window
        {
            Owner = this,
            Title = title,
            Width = 610,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Content = stack
        };

        ok.Click += (_, _) =>
            dialog.DialogResult = true;

        return dialog.ShowDialog() == true
            ? input.Text.Trim()
            : null;
    }

    private async void ExecuteTask_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_busy)
            return;

        if (_task == "Logs")
        {
            ShowPage(
                logsPanel,
                "Bảo trì hệ thống / Xem nhật ký hệ thống",
                btnMaintenance);

            await RunAsync(async () =>
            {
                _logs =
                    await AdminService.GetLogsAsync();

                ApplyLogFilter();
            });

            return;
        }

        if (_task == "Backup")
        {
            string initial =
                @"C:\SQLBackup\QuanLyNhaTro_"
                + DateTime.Now.ToString("yyyyMMdd_HHmmss")
                + "_"
                + Guid.NewGuid().ToString("N")[..8]
                + ".bak";

            string? path = PromptPath(
                "Sao lưu CSDL",
                initial);

            if (path == null)
                return;

            await RunAsync(
                () => AdminService.BackupAsync(path),
                "Sao lưu dữ liệu thành công: " + path);

            return;
        }

        if (_task == "Restore")
        {
            string? path = PromptPath(
                "Khôi phục CSDL",
                @"C:\SQLBackup\");

            if (path == null)
                return;

            var answer = MessageBox.Show(
                this,
                "Thao tác sẽ thay dữ liệu hiện tại "
                    + "bằng bản sao lưu và ngắt các phiên SQL "
                    + "đang sử dụng CSDL. "
                    + "Hệ thống sẽ sao lưu dữ liệu hiện tại trước. "
                    + "Bạn có chắc chắn tiếp tục?",
                "Xác nhận khôi phục",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
                return;

            await RunAsync(async () =>
            {
                string safety =
                    await AdminService.RestoreAsync(path);

                MessageBox.Show(
                    this,
                    "Khôi phục thành công. "
                        + "Bản sao an toàn: " + safety
                        + "\nVui lòng đăng nhập lại.",
                    "Khôi phục CSDL");

                OpenLogin();
            });

            return;
        }

        if (_task == "Cache")
        {
            var answer = MessageBox.Show(
                this,
                "Xác nhận dọn tệp cache của ứng dụng?",
                "Xác nhận",
                MessageBoxButton.YesNo);

            if (answer != MessageBoxResult.Yes)
                return;

            await RunAsync(async () =>
            {
                int count =
                    await AdminService.CleanCacheAsync();

                txtStatus.Text =
                    $"Đã dọn {count} tệp tạm.";
            });

            return;
        }

        txtStatus.Text =
            "Vui lòng chọn tác vụ bảo trì.";
    }

    private void ApplyLogFilter()
    {
        if (_logs == null || cboLogType == null)
            return;

        string type =
            (cboLogType.SelectedItem as ComboBoxItem)?
                .Content.ToString()
            ?? "Tất cả";

        var rows = _logs.DefaultView
            .Cast<DataRowView>()
            .Where(row =>
                type == "Tất cả"
                || row["LoaiNhatKy"].ToString() == type)
            .ToList();

        logsGrid.ItemsSource = rows;

        txtNoLogs.Visibility =
            rows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void LogFilter_Changed(
        object sender,
        SelectionChangedEventArgs e)
    {
        ApplyLogFilter();
    }

    private async Task ExportAsync(bool excel)
    {
        var dialog = new SaveFileDialog
        {
            Filter = excel
                ? "Excel Workbook|*.xlsx"
                : "Text file|*.txt",
            FileName =
                "NhatKy_"
                + DateTime.Now.ToString("yyyyMMdd_HHmmss"),
            AddExtension = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        await RunAsync(
            async () =>
            {
                _logs =
                    await AdminService.GetLogsAsync();

                ApplyLogFilter();

                var rows =
                    ((IEnumerable<DataRowView>)
                        logsGrid.ItemsSource)
                    .Select(row => row.Row)
                    .ToArray();

                var filtered = rows.Length > 0
                    ? rows.CopyToDataTable()
                    : _logs.Clone();

                string temporary = Path.Combine(
                    Path.GetDirectoryName(dialog.FileName)!,
                    Guid.NewGuid().ToString("N") + ".tmp");

                try
                {
                    AdminService.ExportLogs(
                        filtered,
                        temporary,
                        excel);

                    File.Move(
                        temporary,
                        dialog.FileName,
                        true);
                }
                finally
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
            },
            "Đã xuất nhật ký: " + dialog.FileName);
    }

    private async void ExportText_Click(
        object sender,
        RoutedEventArgs e)
    {
        await ExportAsync(false);
    }

    private async void ExportExcel_Click(
        object sender,
        RoutedEventArgs e)
    {
        await ExportAsync(true);
    }

    private async void Accounts_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_busy)
            return;

        ShowPage(
            accountsPanel,
            "Quản lý tài khoản",
            btnAccounts);

        await RunAsync(LoadAccountsAsync);
    }

    private async Task LoadAccountsAsync()
    {
        _accounts =
            await AdminService.GetAccountsAsync();

        ApplyAccountFilter();
    }

    private void ApplyAccountFilter()
    {
        if (_accounts == null
            || txtSearch == null
            || cboStatus == null)
        {
            return;
        }

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
                        txtSearch.Text.Trim(),
                        StringComparison.OrdinalIgnoreCase)
                && (role == "Tất cả vai trò"
                    || row["VaiTro"].ToString() == role)
                && (status == "Tất cả trạng thái"
                    || row["TrangThai"].ToString() == status))
            .ToList();

        accountsGrid.ItemsSource = rows;

        txtNoAccounts.Visibility =
            rows.Count == 0
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

    private async void ToggleStatus_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_busy
            || (sender as Button)?.DataContext
                is not DataRowView row)
        {
            return;
        }

        string id =
            row["MaNguoiDung"].ToString()!;

        string old =
            row["TrangThai"].ToString()!;

        string next = old == "Hoạt động"
            ? "Khóa"
            : "Hoạt động";

        string action =
            next == "Khóa" ? "Khóa" : "Mở khóa";

        var answer = MessageBox.Show(
            this,
            $"{action} tài khoản {row["TenDangNhap"]}?",
            "Xác nhận",
            MessageBoxButton.YesNo);

        if (answer != MessageBoxResult.Yes)
            return;

        await RunAsync(
            async () =>
            {
                await AdminService.SetStatusAsync(
                    id,
                    old,
                    next);

                await LoadAccountsAsync();
            },
            "Cập nhật trạng thái tài khoản thành công.");
    }

    private sealed record ExtraInfo(
        string Name,
        string Cccd,
        string Role,
        string? Day);

    private ExtraInfo? PromptExtra(
        DataTable days,
        string role,
        bool identity)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(20)
        };

        var name = new TextBox
        {
            Padding = new Thickness(8),
            MaxLength = 100
        };

        var cccd = new TextBox
        {
            Padding = new Thickness(8),
            MaxLength = 12
        };

        void Field(string label, Control input)
        {
            stack.Children.Add(new TextBlock
            {
                Text = label,
                Margin = new Thickness(0, 8, 0, 5)
            });

            stack.Children.Add(input);
        }

        if (identity)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "CSDL yêu cầu họ tên và CCCD "
                    + "cho tài khoản mới.",
                TextWrapping = TextWrapping.Wrap
            });

            Field("Họ tên", name);
            Field("CCCD (12 chữ số)", cccd);
        }

        var roles = new ComboBox
        {
            ItemsSource = AdminService.Roles,
            SelectedItem = role,
            Padding = new Thickness(8),
            IsEnabled = !identity
        };

        if (!identity)
        {
            Field("Vai trò mới", roles);
        }

        var day = new ComboBox
        {
            ItemsSource = days.DefaultView,
            DisplayMemberPath = "TenDayTro",
            SelectedValuePath = "MaDayTro",
            Padding = new Thickness(8)
        };

        Field(
            "Dãy trọ (bắt buộc đối với Nhân viên quản lý)",
            day);

        var message = new TextBlock
        {
            Foreground = Brushes.Red,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 8)
        };

        stack.Children.Add(message);

        var ok = new Button
        {
            Content = "Xác nhận",
            Padding = new Thickness(14, 8, 14, 8),
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true
        };

        stack.Children.Add(ok);

        var window = new Window
        {
            Owner = this,
            Title = identity
                ? "Thông tin hồ sơ bắt buộc"
                : "Cập nhật vai trò",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Content = stack
        };

        ok.Click += (_, _) =>
        {
            if (identity
                && (string.IsNullOrWhiteSpace(name.Text)
                    || !System.Text.RegularExpressions.Regex
                        .IsMatch(
                            cccd.Text.Trim(),
                            @"^[0-9]{12}$")))
            {
                message.Text =
                    "Nhập họ tên và CCCD gồm 12 chữ số.";
                return;
            }

            if ((string?)roles.SelectedItem
                    == "Nhân viên quản lý"
                && day.SelectedValue == null)
            {
                message.Text =
                    "Chọn dãy trọ cho nhân viên quản lý.";
                return;
            }

            window.DialogResult = true;
        };

        return window.ShowDialog() == true
            ? new ExtraInfo(
                name.Text.Trim(),
                cccd.Text.Trim(),
                (string)roles.SelectedItem,
                day.SelectedValue as string)
            : null;
    }

    private async void ChangeRole_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_busy
            || accountsGrid.SelectedItem
                is not DataRowView row)
        {
            return;
        }

        string id =
            row["MaNguoiDung"].ToString()!;

        string old =
            row["VaiTro"].ToString()!;

        await RunAsync(async () =>
        {
            var info = PromptExtra(
                await AdminService.GetDaysAsync(),
                old,
                false);

            if (info == null)
                return;

            await AdminService.SetRoleAsync(
                id,
                old,
                info.Role,
                info.Day);

            await LoadAccountsAsync();

            txtStatus.Text =
                "Đã cập nhật vai trò.";
        });
    }

    private void NewAccount_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!_busy)
        {
            ShowPage(
                createPanel,
                "Quản lý tài khoản / Tạo tài khoản mới",
                btnAccounts);
        }
    }

    private async void CreateAccount_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_pendingEmailId != null)
        {
            txtStatus.Text =
                "Tài khoản trước đã được tạo. "
                + "Hãy gửi lại email trước khi tạo tiếp.";
            return;
        }

        if (cboNewRole.SelectedItem is not string role)
        {
            txtStatus.Text =
                "Vui lòng chọn vai trò.";
            return;
        }

        await RunAsync(async () =>
        {
            var info = PromptExtra(
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

                txtNewLogin.Clear();
                txtNewEmail.Clear();
                txtNewPhone.Clear();
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

    private async void RetryEmail_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_pendingEmailId == null)
            return;

        await RunAsync(
            async () =>
            {
                await AdminService.ResendEmailAsync(
                    _pendingEmailId);

                _pendingEmailId = null;

                btnRetryEmail.Visibility =
                    Visibility.Collapsed;

                txtNewLogin.Clear();
                txtNewEmail.Clear();
                txtNewPhone.Clear();
            },
            "Đã gửi lại email cấp mật khẩu.");
    }

    private void Profile_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_busy)
            return;

        var view = new HoSoView();

        view.ProfileUpdated += (_, _) =>
        {
            txtUser.Text =
                UserSession.CurrentUser?.HoTen ?? "";
        };

        profilePanel.Content = view;

        ShowPage(
            profilePanel,
            "Hồ sơ cá nhân",
            btnProfile);
    }

    private void Password_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!_busy)
        {
            ShowPage(
                passwordPanel,
                "Đổi mật khẩu",
                btnPassword);
        }
    }

    private async void ChangePassword_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (newPassword.Password
            != confirmPassword.Password)
        {
            txtStatus.Text =
                "Hai mật khẩu mới không trùng khớp.";
            return;
        }

        string old = oldPassword.Password;
        string next = newPassword.Password;

        await RunAsync(
            async () =>
            {
                await AdminService.ChangePasswordAsync(
                    old,
                    next);

                oldPassword.Clear();
                newPassword.Clear();
                confirmPassword.Clear();
            },
            "Đổi mật khẩu thành công.");
    }

    private async void Bell_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_busy)
            return;

        await RunAsync(async () =>
        {
            var table =
                await AdminService.GetNotificationsAsync();

            string text = table.Rows.Count == 0
                ? "Bạn chưa có thông báo."
                : string.Join(
                    "\n\n",
                    table.AsEnumerable().Select(
                        row =>
                            row["TieuDe"]
                            + "\n"
                            + row["NoiDung"]));

            var box = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto,
                Margin = new Thickness(16)
            };

            new Window
            {
                Owner = this,
                Title = "Thông báo",
                Width = 600,
                Height = 440,
                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner,
                Content = box
            }.ShowDialog();
        });
    }

    private void OpenLogin()
    {
        UserSession.SignOut();

        var login = new LoginWindow();

        Application.Current.MainWindow = login;

        login.Show();
        Close();
    }

    private void Logout_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_busy)
            return;

        if (MessageBox.Show(
            this,
            "Bạn có chắc chắn muốn đăng xuất?",
            "Xác nhận",
            MessageBoxButton.YesNo)
            == MessageBoxResult.Yes)
        {
            OpenLogin();
        }
    }
}