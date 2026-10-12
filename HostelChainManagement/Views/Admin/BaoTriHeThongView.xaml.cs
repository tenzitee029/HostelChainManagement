using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Admin
{
    public partial class BaoTriHeThongView : UserControl
    {
        private bool _busy;
        private string? _task;

        public bool IsBusy => _busy;

        public event Action? LogsRequested;
        public event Action? SessionResetRequested;

        public BaoTriHeThongView()
        {
            InitializeComponent();
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
                txtStatus.Text = ex.Number == 50001
                    ? ex.Message
                    : "Không thực hiện được thao tác SQL. "
                        + "Kiểm tra quyền và đường dẫn trên máy chủ.";

                await SystemLogService.TryWriteAsync(
                    UserSession.CurrentUser?.MaNguoiDung,
                    "Log lỗi runtime",
                    $"Lỗi SQL {ex.Number} khi bảo trì.");
            }
            catch (Exception ex)
            {
                txtStatus.Text = ex.Message;

                await SystemLogService.TryWriteAsync(
                    UserSession.CurrentUser?.MaNguoiDung,
                    "Log lỗi runtime",
                    "Lỗi bảo trì: " + ex.GetType().Name);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void Task_Checked(
            object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio)
            {
                _task = radio.Tag.ToString();

                if (txtTask != null)
                    txtTask.Text = radio.Content.ToString();
            }
        }

        private string? PromptPath(string title, string initial)
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
                Padding = new Thickness(14, 8, 14, 8),
                HorizontalAlignment = HorizontalAlignment.Right,
                IsDefault = true
            };

            stack.Children.Add(ok);

            var dialog = new Window
            {
                Owner = Window.GetWindow(this),
                Title = title,
                Width = 610,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Content = stack
            };

            ok.Click += (_, _) => dialog.DialogResult = true;

            return dialog.ShowDialog() == true
                ? input.Text.Trim()
                : null;
        }

        private async void ExecuteTask_Click(
            object sender, RoutedEventArgs e)
        {
            if (_busy)
                return;

            if (_task == "Logs")
            {
                LogsRequested?.Invoke();
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

                string? path =
                    PromptPath("Sao lưu CSDL", initial);

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

                if (MessageBox.Show(
                    Window.GetWindow(this),
                    "Thao tác sẽ thay dữ liệu hiện tại bằng bản sao lưu "
                        + "và ngắt các phiên SQL đang sử dụng CSDL. "
                        + "Hệ thống sẽ sao lưu dữ liệu hiện tại trước. "
                        + "Bạn có chắc chắn tiếp tục?",
                    "Xác nhận khôi phục",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning)
                    != MessageBoxResult.Yes)
                {
                    return;
                }

                await RunAsync(async () =>
                {
                    string safety =
                        await AdminService.RestoreAsync(path);

                    MessageBox.Show(
                        Window.GetWindow(this),
                        "Khôi phục thành công. Bản sao an toàn: "
                            + safety + "\nVui lòng đăng nhập lại.",
                        "Khôi phục CSDL");

                    if (IsLoaded)
                        SessionResetRequested?.Invoke();
                });

                return;
            }

            if (_task == "Cache")
            {
                if (MessageBox.Show(
                    Window.GetWindow(this),
                    "Xác nhận dọn tệp cache của ứng dụng?",
                    "Xác nhận",
                    MessageBoxButton.YesNo)
                    != MessageBoxResult.Yes)
                {
                    return;
                }

                await RunAsync(async () =>
                {
                    int count =
                        await AdminService.CleanCacheAsync();

                    txtStatus.Text = $"Đã dọn {count} tệp tạm.";
                });

                return;
            }

            txtStatus.Text = "Vui lòng chọn tác vụ bảo trì.";
        }
    }
}