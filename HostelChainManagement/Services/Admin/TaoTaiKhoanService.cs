using System;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Admin
{
    public static class TaoTaiKhoanService
    {
        public static readonly string[] Roles = AdminService.Roles;

        public static Task<DataTable> GetDaysAsync()
        {
            return AdminService.GetDaysAsync();
        }

        private static SmtpClient CreateSmtp(out string from)
        {
            string Get(string key)
            {
                return Environment.GetEnvironmentVariable(key)
                    ?? throw new InvalidOperationException(
                        $"Chưa cấu hình biến môi trường {key}.");
            }

            from = new MailAddress(
                Get("NHATRO_SMTP_FROM")).Address;

            string host = Get("NHATRO_SMTP_HOST");

            int port = int.TryParse(
                Environment.GetEnvironmentVariable(
                    "NHATRO_SMTP_PORT"),
                out int value)
                ? value
                : 587;

            return new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(
                    Get("NHATRO_SMTP_USER"),
                    Get("NHATRO_SMTP_PASSWORD"))
            };
        }

        public static async Task<CreatedAccount> CreateAccountAsync(
                    string login,
                    string email,
                    string phone,
                    string role,
                    string fullName,
                    string cccd,
                    string? day)
        {
            login = login.Trim();
            email = email.Trim();
            phone = phone.Trim();
            fullName = fullName.Trim();
            cccd = cccd.Trim();

            bool hasEmail = !string.IsNullOrWhiteSpace(email);

            if (!Regex.IsMatch(login, @"^[A-Za-z0-9_.-]{3,50}$"))
            {
                throw new InvalidOperationException(
                    "Tên đăng nhập cần 3–50 ký tự chữ, số, "
                    + "dấu _, . hoặc -.");
            }

            if (string.IsNullOrWhiteSpace(fullName)
                || fullName.Length > 100)
            {
                throw new InvalidOperationException(
                    "Họ tên không được để trống, tối đa 100 ký tự.");
            }

            if (!Regex.IsMatch(cccd, @"^[0-9]{12}$"))
            {
                throw new InvalidOperationException(
                    "CCCD phải có đúng 12 chữ số.");
            }

            if (hasEmail)
            {
                bool validEmail;

                try
                {
                    validEmail =
                        email.Length <= 100
                        && !email.Any(character => character > 127)
                        && string.Equals(
                            new MailAddress(email).Address,
                            email,
                            StringComparison.OrdinalIgnoreCase);
                }
                catch (FormatException)
                {
                    validEmail = false;
                }

                if (!validEmail)
                {
                    throw new InvalidOperationException(
                        "Email không hợp lệ. Bạn có thể để trống.");
                }
            }

            if (!Regex.IsMatch(phone, @"^[0-9]{9,15}$"))
            {
                throw new InvalidOperationException(
                    "SĐT cần 9–15 chữ số.");
            }

            if (!Roles.Contains(role))
            {
                throw new InvalidOperationException(
                    "Vai trò không hợp lệ.");
            }

            bool employee =
                role == "Nhân viên quản lý"
                || role == "Nhân viên kỹ thuật";

            day = employee && !string.IsNullOrWhiteSpace(day)
                ? day.Trim()
                : null;

            if (role == "Nhân viên quản lý" && day == null)
            {
                throw new InvalidOperationException(
                    "Chọn dãy trọ cho nhân viên quản lý.");
            }

            string adminId = AdminService.UserId();

            string password =
                Convert.ToBase64String(
                    RandomNumberGenerator.GetBytes(12))
                + "@1aA";

            string id = AdminService.NewId("ND");

            // 1. Lưu thông tin tài khoản vào CSDL trước
            using (var connection = Database.CreateConnection())
            {
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                await AdminService.LockAccountsAsync(
                    connection,
                    transaction);

                if (day != null)
                {
                    using var dayCommand = connection.CreateCommand();
                    dayCommand.Transaction = transaction;

                    dayCommand.CommandText = @"
                        SELECT COUNT(*)
                        FROM dbo.DayTro WITH (HOLDLOCK)
                        WHERE MaDayTro = @Day
                          AND TrangThai = N'Đang hoạt động';";

                    dayCommand.Parameters.Add(
                        "@Day",
                        SqlDbType.VarChar,
                        20).Value = day;

                    if (Convert.ToInt32(
                        await dayCommand.ExecuteScalarAsync()) != 1)
                    {
                        throw new InvalidOperationException(
                            "Dãy trọ không tồn tại hoặc đã ngừng hoạt động.");
                    }
                }

                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                // Kiểm tra trùng lặp thông tin và Email trước khi INSERT
                command.CommandText = @"
                    IF EXISTS (SELECT 1 FROM dbo.NguoiDung WHERE TenDangNhap = @Login)
                        THROW 50001, N'Tên đăng nhập đã tồn tại trong hệ thống.', 1;

                    IF EXISTS (SELECT 1 FROM dbo.NguoiDung WHERE CCCD = @CCCD)
                        THROW 50001, N'Số CCCD đã tồn tại trong hệ thống.', 1;

                    IF EXISTS (SELECT 1 FROM dbo.NguoiDung WHERE SoDienThoai = @Phone)
                        THROW 50001, N'Số điện thoại đã tồn tại trong hệ thống.', 1;

                    IF @Email IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.NguoiDung WHERE Email = @Email)
                        THROW 50001, N'Email này đã được sử dụng bởi một tài khoản khác.', 1;

                    INSERT dbo.NguoiDung
                    (
                        MaNguoiDung,
                        TenDangNhap,
                        MatKhau,
                        HoTen,
                        CCCD,
                        Email,
                        SoDienThoai,
                        VaiTro,
                        TrangThai
                    )
                    VALUES
                    (
                        @Id,
                        @Login,
                        @Password,
                        @Name,
                        @CCCD,
                        @Email,
                        @Phone,
                        @Role,
                        N'Hoạt động'
                    );";

                command.Parameters.Add(
                    "@Id", SqlDbType.VarChar, 20).Value = id;

                command.Parameters.Add(
                    "@Login", SqlDbType.VarChar, 50).Value = login;

                command.Parameters.Add(
                    "@Password", SqlDbType.VarChar, 255)
                    .Value = password;

                command.Parameters.Add(
                    "@Name", SqlDbType.NVarChar, 100)
                    .Value = fullName;

                command.Parameters.Add(
                    "@CCCD", SqlDbType.VarChar, 12).Value = cccd;

                command.Parameters.Add(
                    "@Email", SqlDbType.VarChar, 100)
                    .Value = hasEmail ? (object)email : DBNull.Value;

                command.Parameters.Add(
                    "@Phone", SqlDbType.VarChar, 15).Value = phone;

                command.Parameters.Add(
                    "@Role", SqlDbType.NVarChar, 50).Value = role;

                await command.ExecuteNonQueryAsync();

                await AdminService.AddProfileAsync(
                    connection,
                    transaction,
                    id,
                    role,
                    day);

                await AdminService.AuditAsync(
                    connection,
                    transaction,
                    $"Tạo tài khoản {login}, vai trò {role}.");

                transaction.Commit();
            }

            if (!hasEmail)
            {
                return new CreatedAccount(id, false, password);
            }

            // 2. Thử gửi email sau khi CSDL đã lưu thành công
            try
            {
                using var smtp = CreateSmtp(out string from);
                using var mail = new MailMessage(
                    from,
                    email,
                    "Tạo tài khoản hệ thống quản lý nhà trọ thành công",
                    $"Xin chào {fullName},\n\n"
                    + "Tài khoản của bạn đã được tạo thành công.\n"
                    + $"Tên đăng nhập: {login}\n"
                    + $"Mật khẩu ban đầu: {password}\n"
                    + $"Vai trò: {role}\n\n"
                    + "Vui lòng đổi mật khẩu sau khi đăng nhập.");

                await smtp.SendMailAsync(mail);

                return new CreatedAccount(id, true);
            }
            catch (Exception ex)
            {
                await SystemLogService.TryWriteAsync(
                    adminId,
                    "Log lỗi runtime",
                    $"Đã tạo tài khoản {id} nhưng chưa gửi được email: {ex.Message}");

                // Trả về kèm InitialPassword để Admin vẫn có thông tin bàn giao cho người dùng
                return new CreatedAccount(id, false, password);
            }
        }

        public static async Task ResendEmailAsync(string id)
        {
            using var connection = Database.CreateConnection();

            await connection.OpenAsync();
            await AdminService.AuthorizeAsync(connection);

            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    TenDangNhap,
                    HoTen,
                    Email,
                    MatKhau
                FROM dbo.NguoiDung
                WHERE MaNguoiDung = @Id
                  AND TrangThai = N'Hoạt động';";

            command.Parameters.Add(
                "@Id", SqlDbType.VarChar, 20).Value = id;

            string login;
            string name;
            string email;
            string password;

            using (var reader =
                await command.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync()
                    || reader.IsDBNull(2))
                {
                    throw new InvalidOperationException(
                        "Tài khoản không hoạt động "
                        + "hoặc chưa có email.");
                }

                login = reader.GetString(0);
                name = reader.GetString(1);
                email = reader.GetString(2);
                password = reader.GetString(3);
            }

            using var smtp = CreateSmtp(out string from);

            using var mail = new MailMessage(
                from,
                email,
                "Tài khoản hệ thống quản lý nhà trọ",
                $"Xin chào {name},\n"
                    + $"Tên đăng nhập: {login}\n"
                    + $"Mật khẩu: {password}\n"
                    + "Vui lòng đổi mật khẩu sau khi đăng nhập.");

            await smtp.SendMailAsync(mail);

            await SystemLogService.TryWriteAsync(
                AdminService.UserId(),
                "Quản trị",
                $"Gửi lại email cấp mật khẩu cho tài khoản {id}.");
        }
    }
}