using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services
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

            // Không Trim mật khẩu.
            if (string.IsNullOrEmpty(currentPassword)
                || string.IsNullOrEmpty(newPassword)
                || string.IsNullOrEmpty(confirmPassword))
            {
                throw new InvalidOperationException(
                    "Vui lòng nhập đầy đủ ba trường mật khẩu.");
            }

            if (currentPassword.Length > 255)
            {
                throw new InvalidOperationException(
                    "Mật khẩu hiện tại không chính xác.");
            }

            if (!string.Equals(
                newPassword,
                confirmPassword,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Xác nhận mật khẩu mới không khớp.");
            }

            if (string.Equals(
                newPassword,
                currentPassword,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Mật khẩu mới không được trùng mật khẩu cũ.");
            }

            if (newPassword.Length < 8
                || newPassword.Length > 255
                || !newPassword.Any(c => c >= 'A' && c <= 'Z')
                || !newPassword.Any(c => c >= 'a' && c <= 'z')
                || !newPassword.Any(c => c >= '0' && c <= '9'))
            {
                throw new InvalidOperationException(
                    "Mật khẩu mới phải từ 8 đến 255 ký tự, " +
                    "gồm chữ hoa, chữ thường và số.");
            }

            // MatKhau đang là VARCHAR(255).
            if (newPassword.Any(
                c => c > 127 || char.IsControl(c)))
            {
                throw new InvalidOperationException(
                    "Mật khẩu mới chỉ dùng ký tự ASCII, " +
                    "không dùng chữ có dấu hoặc ký tự điều khiển.");
            }

            using var connection = Database.CreateConnection();

            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();

            command.Transaction = transaction;

            // Khóa bản ghi trong lúc kiểm tra và đổi mật khẩu.
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
                    "Tài khoản không còn hoạt động hoặc quyền " +
                    "đã thay đổi. Vui lòng đăng nhập lại.");
            }

            if (!string.Equals(
                stored,
                currentPassword,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Mật khẩu hiện tại không chính xác.");
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

            int count = await command.ExecuteNonQueryAsync();

            if (count != 1)
            {
                throw new InvalidOperationException(
                    "Không cập nhật được mật khẩu tài khoản.");
            }

            transaction.Commit();
        }
    }
}