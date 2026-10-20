using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Models;
using QuanLyChuoiNhaTro.Views.Auth;

namespace QuanLyChuoiNhaTro.Services
{
    public static class UserSession
    {
        private static DispatcherTimer? _timer;
        private static CancellationTokenSource? _checkCancellation;

        private static bool _checking;
        private static bool _redirecting;
        private static bool _exitRegistered;

        private static long _version;

        public static LoggedInUser? CurrentUser
        {
            get;
            private set;
        }

        public static void SignIn(LoggedInUser user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            CurrentUser = user;
            _version++;

            CancelCurrentCheck();
            StartMonitoring();
        }

        public static void SignOut()
        {
            CurrentUser = null;
            _version++;

            _timer?.Stop();
            CancelCurrentCheck();
        }

        private static void StartMonitoring()
        {
            var application = Application.Current;

            if (application == null
                || application.Dispatcher.HasShutdownStarted)
            {
                return;
            }

            if (!application.Dispatcher.CheckAccess())
            {
                application.Dispatcher.Invoke(StartMonitoring);
                return;
            }

            if (_timer == null)
            {
                _timer = new DispatcherTimer(
                    DispatcherPriority.Background,
                    application.Dispatcher)
                {
                    Interval = TimeSpan.FromSeconds(3)
                };

                _timer.Tick += Monitor_Tick;
            }

            if (!_exitRegistered)
            {
                application.Exit += (_, _) =>
                {
                    _timer?.Stop();
                    CancelCurrentCheck();
                };

                _exitRegistered = true;
            }

            _timer.Stop();
            _timer.Start();
        }

        private static void CancelCurrentCheck()
        {
            // CancellationTokenSource được Dispose trong finally
            // của lần kiểm tra đang chạy.
            _checkCancellation?.Cancel();
        }

        public static async Task<bool> IsAccountActiveAsync(
            string userId,
            string role,
            CancellationToken cancellationToken = default)
        {
            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandTimeout = 5;

            command.CommandText = @"
                SELECT COUNT(*)
                FROM dbo.NguoiDung
                WHERE MaNguoiDung = @UserId
                  AND VaiTro = @Role
                  AND TrangThai = N'Hoạt động';";

            command.Parameters.Add(
                "@UserId",
                SqlDbType.VarChar,
                20).Value = userId;

            command.Parameters.Add(
                "@Role",
                SqlDbType.NVarChar,
                50).Value = role;

            await connection.OpenAsync(cancellationToken);

            object? result = await command.ExecuteScalarAsync(
                cancellationToken);

            return Convert.ToInt32(result) == 1;
        }

        private static async void Monitor_Tick(
            object? sender,
            EventArgs e)
        {
            if (_checking || _redirecting)
                return;

            var user = CurrentUser;

            if (user == null)
            {
                _timer?.Stop();
                return;
            }

            long version = _version;

            _checking = true;

            var cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(TimeSpan.FromSeconds(5));

            _checkCancellation = cancellation;

            try
            {
                bool active = await IsAccountActiveAsync(
                    user.MaNguoiDung,
                    user.VaiTro,
                    cancellation.Token);

                // Không xử lý kết quả của phiên đăng nhập cũ.
                if (cancellation.IsCancellationRequested
                    || version != _version
                    || !ReferenceEquals(user, CurrentUser))
                {
                    return;
                }

                if (!active)
                {
                    ReturnToLogin();
                }
            }
            catch (OperationCanceledException)
            {
                // Đã đăng xuất, đổi phiên hoặc hết thời gian kiểm tra.
            }
            catch (SqlException ex)
            {
                // Lỗi kết nối không đồng nghĩa tài khoản bị xóa.
                // Tiếp tục kiểm tra ở lần kế tiếp.
                Debug.WriteLine(
                    "Không kiểm tra được phiên đăng nhập: "
                    + ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "Lỗi theo dõi phiên đăng nhập: "
                    + ex.Message);
            }
            finally
            {
                if (ReferenceEquals(
                    _checkCancellation,
                    cancellation))
                {
                    _checkCancellation = null;
                }

                cancellation.Dispose();
                _checking = false;
            }
        }

        private static void ReturnToLogin()
        {
            if (_redirecting)
                return;

            var application = Application.Current;

            if (application == null
                || application.Dispatcher.HasShutdownStarted)
            {
                return;
            }

            _redirecting = true;

            var oldWindows = application.Windows
                .Cast<Window>()
                .ToArray();

            SignOut();

            try
            {
                // Chặn tương tác với các cửa sổ của phiên cũ.
                foreach (var window in oldWindows)
                {
                    window.IsEnabled = false;
                }

                var login = new LoginWindow();

                // Đặt MainWindow mới trước khi đóng cửa sổ cũ.
                application.MainWindow = login;
                login.Show();

                foreach (var window in oldWindows)
                {
                    try
                    {
                        // Nếu Closing bị hủy thì cửa sổ cũ vẫn
                        // bị ẩn và không thể tương tác.
                        window.Hide();
                        window.Close();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            "Không đóng được cửa sổ phiên cũ: "
                            + ex.Message);
                    }
                }

                login.Activate();

                MessageBox.Show(
                    login,
                    "Tài khoản đã bị khóa, ngừng sử dụng "
                    + "hoặc thay đổi vai trò.\n"
                    + "Phiên đăng nhập đã kết thúc. "
                    + "Vui lòng đăng nhập lại.",
                    "Phiên đăng nhập kết thúc",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                // Nếu không mở được màn hình đăng nhập,
                // các cửa sổ cũ vẫn bị vô hiệu hóa.
                MessageBox.Show(
                    "Phiên đăng nhập đã kết thúc nhưng không "
                    + "mở được giao diện đăng nhập.\n"
                    + "Vui lòng đóng và mở lại ứng dụng.\n\n"
                    + ex.Message,
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _redirecting = false;
            }
        }
    }
}