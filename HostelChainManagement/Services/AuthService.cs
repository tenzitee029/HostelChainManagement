using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Models;

namespace QuanLyChuoiNhaTro.Services
{
    public static class AuthService
    {
        public static async Task<LoggedInUser> LoginAsync(string account, string password)
        {
            account = account?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(account)
                || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException(
                    "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.");
            }

            const string sql = @"
                SELECT
                    MaNguoiDung,
                    TenDangNhap,
                    MatKhau,
                    HoTen,
                    VaiTro,
                    TrangThai
                FROM dbo.NguoiDung
                WHERE TenDangNhap = @TenDangNhap;";

            LoggedInUser user;
            string storedPassword;
            string status;

            using (var connection = Database.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;

                command.Parameters.Add(
                    "@TenDangNhap",
                    SqlDbType.VarChar,
                    50).Value = account;

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                    {
                        throw new InvalidOperationException(
                            "Tên đăng nhập hoặc mật khẩu không chính xác.");
                    }

                    storedPassword = reader.GetString(
                        reader.GetOrdinal("MatKhau"));

                    status = reader.GetString(
                        reader.GetOrdinal("TrangThai"));

                    user = new LoggedInUser
                    {
                        MaNguoiDung = reader.GetString(
                            reader.GetOrdinal("MaNguoiDung")),

                        TenDangNhap = reader.GetString(
                            reader.GetOrdinal("TenDangNhap")),

                        HoTen = reader.GetString(
                            reader.GetOrdinal("HoTen")),

                        VaiTro = reader.GetString(
                            reader.GetOrdinal("VaiTro"))
                    };
                }
            }

            // So sánh chính xác, phân biệt chữ hoa/chữ thường.
            // Không Trim mật khẩu.
            if (!string.Equals(
                password,
                storedPassword,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Tên đăng nhập hoặc mật khẩu không chính xác.");
            }

            if (status != "Hoạt động")
            {
                throw new InvalidOperationException(
                    "Tài khoản của bạn đã bị khóa hoặc ngừng sử dụng. " +
                    "Vui lòng liên hệ Quản trị viên để được hỗ trợ.");
            }

            bool validRole = user.VaiTro is
                "Admin"
                or "Chủ nhà trọ"
                or "Nhân viên quản lý"
                or "Nhân viên kỹ thuật"
                or "Khách thuê";

            if (!validRole)
            {
                throw new InvalidOperationException(
                    "Vai trò tài khoản không hợp lệ.");
            }

            return user;
        }
    }
}