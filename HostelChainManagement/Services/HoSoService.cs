using System;
using System.Data;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Models;

namespace QuanLyChuoiNhaTro.Services
{
    public static class HoSoService
    {
        public static async Task<HoSoCaNhan> GetAsync()
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException(
                    "Bạn chưa đăng nhập.");

            const string sql = @"
                SELECT
                    n.HoTen,
                    n.Email,
                    n.SoDienThoai,
                    n.DiaChi,
                    n.AnhDaiDien,
                    CASE
                        WHEN n.VaiTro = N'Khách thuê' THEN
                            (SELECT TOP (1) k.CCCD
                             FROM dbo.KhachThue AS k
                             WHERE k.MaNguoiDung = n.MaNguoiDung
                             ORDER BY k.MaKhachThue)
                        WHEN n.VaiTro IN
                            (N'Nhân viên quản lý', N'Nhân viên kỹ thuật')
                        THEN
                            (SELECT TOP (1) nv.CCCD
                             FROM dbo.NhanVien AS nv
                             WHERE nv.MaNguoiDung = n.MaNguoiDung
                             ORDER BY nv.MaNhanVien)
                        ELSE NULL
                    END AS CCCD
                FROM dbo.NguoiDung AS n
                WHERE n.MaNguoiDung = @MaNguoiDung;";

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;
            command.Parameters.Add(
                "@MaNguoiDung", SqlDbType.VarChar, 20)
                .Value = user.MaNguoiDung;

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                throw new InvalidOperationException(
                    "Không tìm thấy hồ sơ tài khoản.");

            string ReadText(string column)
            {
                int index = reader.GetOrdinal(column);
                return reader.IsDBNull(index)
                    ? ""
                    : reader.GetString(index);
            }

            return new HoSoCaNhan
            {
                HoTen = ReadText("HoTen"),
                CCCD = ReadText("CCCD"),
                Email = ReadText("Email"),
                SoDienThoai = ReadText("SoDienThoai"),
                DiaChi = ReadText("DiaChi"),

                AnhDaiDien = reader.IsDBNull(
                    reader.GetOrdinal("AnhDaiDien"))
                    ? null
                    : (byte[])reader["AnhDaiDien"]
            };
        }

        public static async Task UpdateAsync(HoSoCaNhan profile)
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException(
                    "Bạn chưa đăng nhập.");

            profile.HoTen = profile.HoTen.Trim();
            profile.Email = profile.Email.Trim();
            profile.SoDienThoai = profile.SoDienThoai.Trim();
            profile.DiaChi = profile.DiaChi.Trim();

            if (profile.HoTen.Length == 0)
                throw new InvalidOperationException(
                    "Vui lòng nhập họ tên.");

            if (profile.HoTen.Length > 100)
                throw new InvalidOperationException(
                    "Họ tên không được quá 100 ký tự.");

            if (profile.Email.Length > 100)
                throw new InvalidOperationException(
                    "Email không được quá 100 ký tự.");

            if (profile.Email.Length > 0)
            {
                if (!MailAddress.TryCreate(
                    profile.Email, out var address)
                    || address.Address != profile.Email
                    || !Regex.IsMatch(profile.Email, @"^[\x00-\x7F]+$"))
                {
                    throw new InvalidOperationException(
                        "Email không hợp lệ.");
                }
            }

            // Quy ước bản thử: SĐT Việt Nam dạng 10 chữ số.
            if (profile.SoDienThoai.Length > 0
                && !Regex.IsMatch(
                    profile.SoDienThoai, @"^0[0-9]{9}$"))
            {
                throw new InvalidOperationException(
                    "Số điện thoại phải có 10 chữ số, bắt đầu bằng 0.");
            }

            if (profile.DiaChi.Length > 255)
                throw new InvalidOperationException(
                    "Địa chỉ không được quá 255 ký tự.");

            if (profile.AnhDaiDien?.Length > 2 * 1024 * 1024)
                throw new InvalidOperationException(
                    "Ảnh đại diện không được vượt quá 2 MB.");

            const string sql = @"
                UPDATE dbo.NguoiDung
                SET HoTen = @HoTen,
                    Email = @Email,
                    SoDienThoai = @SoDienThoai,
                    DiaChi = @DiaChi,
                    AnhDaiDien = @AnhDaiDien
                WHERE MaNguoiDung = @MaNguoiDung;";

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;

            command.Parameters.Add(
                "@MaNguoiDung", SqlDbType.VarChar, 20)
                .Value = user.MaNguoiDung;

            command.Parameters.Add(
                "@HoTen", SqlDbType.NVarChar, 100)
                .Value = profile.HoTen;

            command.Parameters.Add(
                "@Email", SqlDbType.VarChar, 100)
                .Value = Optional(profile.Email);

            command.Parameters.Add(
                "@SoDienThoai", SqlDbType.VarChar, 15)
                .Value = Optional(profile.SoDienThoai);

            command.Parameters.Add(
                "@DiaChi", SqlDbType.NVarChar, 255)
                .Value = Optional(profile.DiaChi);

            command.Parameters.Add(
                "@AnhDaiDien", SqlDbType.VarBinary, -1)
                .Value = (object?)profile.AnhDaiDien ?? DBNull.Value;

            await connection.OpenAsync();

            try
            {
                int count = await command.ExecuteNonQueryAsync();

                if (count != 1)
                    throw new InvalidOperationException(
                        "Không tìm thấy tài khoản để cập nhật.");
            }
            catch (SqlException ex)
                when (ex.Number is 2601 or 2627)
            {
                throw new InvalidOperationException(
                    "Số điện thoại này đã được liên kết " +
                    "với một tài khoản khác.");
            }

            // Cập nhật tên trong phiên đăng nhập.
            UserSession.SignIn(new LoggedInUser
            {
                MaNguoiDung = user.MaNguoiDung,
                TenDangNhap = user.TenDangNhap,
                VaiTro = user.VaiTro,
                HoTen = profile.HoTen
            });
        }

        private static object Optional(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? DBNull.Value
                : value;
        }
    }
}