using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Account
{
    public static class DoiMatKhauService
    {
        public static async Task ChangeAsync(
            string currentPassword,
            string newPassword,
            string confirmPassword)
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException(
                    "Bạn chưa đăng nhập.");

            if (user.VaiTro is not (
                "Admin"
                or "Chủ nhà trọ"
                or "Nhân viên quản lý"
                or "Nhân viên kỹ thuật"
                or "Khách thuê"))
            {
                throw new InvalidOperationException(
                    "Vai trò tài khoản không hợp lệ.");
            }

            if (string.IsNullOrEmpty(currentPassword) ||
                string.IsNullOrEmpty(newPassword) ||
                string.IsNullOrEmpty(confirmPassword))
            {
                throw new InvalidOperationException(
                    "Vui lòng nhập đầy đủ ba trường mật khẩu.");
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

            using var connection = Database.CreateConnection();

            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText = @"
                SELECT MatKhau
                FROM dbo.NguoiDung WITH (UPDLOCK, HOLDLOCK)
                WHERE MaNguoiDung = @User
                  AND VaiTro = @Role
                  AND TrangThai = N'Hoạt động';";

            command.Parameters.Add(
                "@User",
                SqlDbType.VarChar,
                20).Value = user.MaNguoiDung;

            command.Parameters.Add(
                "@Role",
                SqlDbType.NVarChar,
                50).Value = user.VaiTro;

            string? stored =
                await command.ExecuteScalarAsync() as string;

            if (stored == null)
            {
                throw new InvalidOperationException(
                    "Tài khoản không còn hoạt động. " +
                    "Vui lòng đăng nhập lại.");
            }

            if (!PasswordSecurity.VerifyPassword(
                currentPassword,
                stored))
            {
                throw new InvalidOperationException(
                    "Mật khẩu hiện tại không chính xác.");
            }

            if (PasswordSecurity.VerifyPassword(
                newPassword,
                stored))
            {
                throw new InvalidOperationException(
                    "Mật khẩu mới không được trùng mật khẩu cũ.");
            }

            command.CommandText = @"
                UPDATE dbo.NguoiDung
                SET MatKhau = @Password
                WHERE MaNguoiDung = @User
                  AND VaiTro = @Role
                  AND TrangThai = N'Hoạt động';";

            command.Parameters.Add(
                "@Password",
                SqlDbType.VarChar,
                255).Value = newPassword;

            int changed = await command.ExecuteNonQueryAsync();

            if (changed != 1)
            {
                throw new InvalidOperationException(
                    "Không cập nhật được mật khẩu tài khoản.");
            }

            transaction.Commit();
        }
    }
}