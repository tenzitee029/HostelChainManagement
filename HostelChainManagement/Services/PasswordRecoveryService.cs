using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services
{
    public static class PasswordRecoveryService
    {
        public sealed class RecoveryTicket
        {
            internal RecoveryTicket(
                string userId,
                string cccd,
                string phone)
            {
                UserId = userId;
                Cccd = cccd;
                Phone = phone;
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5);
            }

            internal string UserId { get; }
            internal string Cccd { get; }
            internal string Phone { get; }
            internal bool Used { get; set; }
            internal SemaphoreSlim Gate { get; } = new(1, 1);

            public DateTime ExpiresAtUtc { get; }
        }

        private static readonly object AttemptLock = new();

        private static int _failedAttempts;
        private static DateTime _blockedUntilUtc = DateTime.MinValue;

        public static async Task<RecoveryTicket> VerifyIdentityAsync(
            string cccd,
            string phone)
        {
            cccd = cccd?.Trim() ?? "";
            phone = phone?.Trim() ?? "";

            CheckAttemptLimit();

            if (cccd.Length != 12 ||
                cccd.Any(c => c < '0' || c > '9') ||
                (phone.Length != 10 && phone.Length != 11) ||
                phone.Any(c => c < '0' || c > '9'))
            {
                RegisterFailedAttempt();

                throw new InvalidOperationException(
                    "CCCD hoặc số điện thoại không hợp lệ.");
            }

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT MaNguoiDung
                FROM dbo.NguoiDung
                WHERE CCCD = @CCCD
                  AND SoDienThoai = @Phone
                  AND TrangThai = N'Hoạt động';";

            command.Parameters.Add(
                "@CCCD",
                SqlDbType.VarChar,
                12).Value = cccd;

            command.Parameters.Add(
                "@Phone",
                SqlDbType.VarChar,
                15).Value = phone;

            await connection.OpenAsync();

            object? result = await command.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
            {
                RegisterFailedAttempt();

                throw new InvalidOperationException(
                    "Thông tin xác minh không chính xác " +
                    "hoặc tài khoản không hoạt động.");
            }

            ResetAttemptLimit();

            return new RecoveryTicket(
                Convert.ToString(result)!,
                cccd,
                phone);
        }

        public static async Task ResetPasswordAsync(
            RecoveryTicket ticket,
            string newPassword,
            string confirmPassword)
        {
            if (ticket == null)
            {
                throw new InvalidOperationException(
                    "Phiên xác minh không hợp lệ.");
            }

            if (!string.Equals(
                    newPassword,
                    confirmPassword,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Xác nhận mật khẩu mới không khớp.");
            }

            PasswordSecurity.ValidateNewPassword(newPassword);

            await ticket.Gate.WaitAsync();

            try
            {
                if (ticket.Used ||
                    DateTime.UtcNow >= ticket.ExpiresAtUtc)
                {
                    throw new InvalidOperationException(
                        "Phiên xác minh đã hết hạn hoặc đã sử dụng. " +
                        "Vui lòng xác minh lại.");
                }

                using var connection = Database.CreateConnection();

                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();
                using var command = connection.CreateCommand();

                command.Transaction = transaction;

                command.CommandText = @"
                    SELECT MatKhau
                    FROM dbo.NguoiDung WITH (UPDLOCK, HOLDLOCK)
                    WHERE MaNguoiDung = @UserId
                      AND CCCD = @CCCD
                      AND SoDienThoai = @Phone
                      AND TrangThai = N'Hoạt động';";

                command.Parameters.Add(
                    "@UserId",
                    SqlDbType.VarChar,
                    20).Value = ticket.UserId;

                command.Parameters.Add(
                    "@CCCD",
                    SqlDbType.VarChar,
                    12).Value = ticket.Cccd;

                command.Parameters.Add(
                    "@Phone",
                    SqlDbType.VarChar,
                    15).Value = ticket.Phone;

                string? currentPassword =
                    await command.ExecuteScalarAsync() as string;

                if (currentPassword == null)
                {
                    throw new InvalidOperationException(
                        "Không thể xác minh tài khoản. " +
                        "Vui lòng bắt đầu lại.");
                }

                if (PasswordSecurity.VerifyPassword(
                    newPassword,
                    currentPassword))
                {
                    throw new InvalidOperationException(
                        "Mật khẩu mới phải khác mật khẩu hiện tại.");
                }

                command.CommandText = @"
                    UPDATE dbo.NguoiDung
                    SET MatKhau = @Password
                    WHERE MaNguoiDung = @UserId
                      AND CCCD = @CCCD
                      AND SoDienThoai = @Phone
                      AND TrangThai = N'Hoạt động';";

                command.Parameters.Add(
                    "@Password",
                    SqlDbType.VarChar,
                    255).Value = newPassword;

                int changed = await command.ExecuteNonQueryAsync();

                if (changed != 1)
                {
                    throw new InvalidOperationException(
                        "Không thể đặt lại mật khẩu.");
                }

                transaction.Commit();

                ticket.Used = true;
            }
            finally
            {
                ticket.Gate.Release();
            }

            await SystemLogService.TryWriteAsync(
                ticket.UserId,
                "Khôi phục mật khẩu",
                "Đặt lại mật khẩu qua xác minh CCCD và số điện thoại.");
        }

        private static void CheckAttemptLimit()
        {
            lock (AttemptLock)
            {
                if (DateTime.UtcNow < _blockedUntilUtc)
                {
                    throw new InvalidOperationException(
                        "Đã thử xác minh quá nhiều lần. " +
                        "Vui lòng đợi 15 phút.");
                }

                if (_failedAttempts >= 5)
                {
                    _failedAttempts = 0;
                    _blockedUntilUtc = DateTime.MinValue;
                }
            }
        }

        private static void RegisterFailedAttempt()
        {
            lock (AttemptLock)
            {
                _failedAttempts++;

                if (_failedAttempts >= 5)
                {
                    _blockedUntilUtc =
                        DateTime.UtcNow.AddMinutes(15);
                }
            }
        }

        private static void ResetAttemptLimit()
        {
            lock (AttemptLock)
            {
                _failedAttempts = 0;
                _blockedUntilUtc = DateTime.MinValue;
            }
        }
    }
}