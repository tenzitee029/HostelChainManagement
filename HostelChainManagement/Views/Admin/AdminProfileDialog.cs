using System.Data;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Admin
{
    internal static class AdminProfileDialog
    {
        internal sealed record ExtraInfo(
            string Name,
            string Cccd,
            string Role,
            string? Day);

        public static ExtraInfo? Show(
            Window? owner,
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
                Field("Vai trò mới", roles);

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
                Owner = owner,
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
                        || !Regex.IsMatch(
                            cccd.Text.Trim(), @"^[0-9]{12}$")))
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
    }
}