using System;
using System.Data;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Models;

namespace QuanLyChuoiNhaTro.Services.Account
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
                    n.TenDangNhap,
                    n.HoTen,
                    n.CCCD,
                    n.Email,
                    n.SoDienThoai,
                    n.DiaChi,
                    n.AnhDaiDien
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
                TenDangNhap = ReadText("TenDangNhap"),
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
            profile.TenDangNhap = profile.TenDangNhap?.Trim() ?? "";

            if (profile.TenDangNhap.Length == 0)
            {
                throw new InvalidOperationException(
                    "Vui lòng nhập tên đăng nhập.");
            }

            if (profile.TenDangNhap.Length > 50)
            {
                throw new InvalidOperationException(
                    "Tên đăng nhập không được quá 50 ký tự.");
            }

            // TenDangNhap trong SQL hiện tại là VARCHAR(50).
            if (!Regex.IsMatch(
                profile.TenDangNhap,
                @"^[\x21-\x7E]+$"))
            {
                throw new InvalidOperationException(
                    "Tên đăng nhập phải dùng ký tự không dấu " +
                    "và không chứa khoảng trắng.");
            }
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
                IF EXISTS
                (
                    SELECT 1
                    FROM dbo.NguoiDung
                    WHERE TenDangNhap = @TenDangNhap
                      AND MaNguoiDung <> @MaNguoiDung
                )
                BEGIN
                    THROW 50004, N'Tên đăng nhập đã được sử dụng.', 1;
                END;

                UPDATE dbo.NguoiDung
                SET
                    TenDangNhap = @TenDangNhap,
                    HoTen = @HoTen,
                    Email = @Email,
                    SoDienThoai = @SoDienThoai,
                    DiaChi = @DiaChi,
                    AnhDaiDien = @AnhDaiDien
                WHERE MaNguoiDung = @MaNguoiDung;";

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;

            command.Parameters.Add(
                "@TenDangNhap",
                SqlDbType.VarChar,
                50).Value = profile.TenDangNhap;

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
            catch (SqlException ex) when (ex.Number == 50004)
            {
                throw new InvalidOperationException(
                    "Tên đăng nhập đã được sử dụng. Vui lòng chọn tên khác.",
                    ex);
            }
            catch (SqlException ex) when (
                ex.Number == 2601 || ex.Number == 2627)
            {
                // Ràng buộc UNIQUE trong SQL vẫn bảo vệ khi
                // nhiều người cập nhật cùng lúc.
                throw new InvalidOperationException(
                    "Tên đăng nhập hoặc số điện thoại đã được sử dụng " +
                    "bởi tài khoản khác.",
                    ex);
            }

            // Cập nhật tên trong phiên đăng nhập.
            if (ReferenceEquals(user, UserSession.CurrentUser))
            {
                UserSession.SignIn(new LoggedInUser
                {
                    MaNguoiDung = user.MaNguoiDung,
                    TenDangNhap = profile.TenDangNhap,
                    VaiTro = user.VaiTro,
                    HoTen = profile.HoTen
                });
            }
        }

        private static object Optional(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? DBNull.Value
                : value;
        }
    }
}