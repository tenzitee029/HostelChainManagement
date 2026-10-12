using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QuanLyChuoiNhaTro.Services;

public sealed record CreatedAccount(
    string Id,
    bool EmailSent);

public static class AdminService
{
    public static readonly string[] Roles =
    {
        "Admin",
        "Chủ nhà trọ",
        "Nhân viên quản lý",
        "Nhân viên kỹ thuật",
        "Khách thuê"
    };

    private static string UserId()
    {
        var user = UserSession.CurrentUser;

        if (user == null || user.VaiTro != "Admin")
        {
            throw new InvalidOperationException(
                "Chức năng chỉ dành cho Admin hệ thống.");
        }

        return user.MaNguoiDung;
    }

    private static async Task AuthorizeAsync(
        SqlConnection connection,
        SqlTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        command.CommandText = @"
            SELECT COUNT(*)
            FROM dbo.NguoiDung
            WHERE MaNguoiDung = @User
              AND VaiTro = N'Admin'
              AND TrangThai = N'Hoạt động';";

        command.Parameters.Add(
            "@User", SqlDbType.VarChar, 20).Value =
            UserId();

        int count = Convert.ToInt32(
            await command.ExecuteScalarAsync());

        if (count != 1)
        {
            throw new InvalidOperationException(
                "Tài khoản Admin không còn hoạt động. "
                + "Vui lòng đăng nhập lại.");
        }
    }

    private static async Task LockAccountsAsync(
        SqlConnection connection,
        SqlTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        command.CommandText = @"
            DECLARE @Result INT;

            EXEC @Result = sys.sp_getapplock
                @Resource = N'QuanLyNhaTro_AdminAccounts',
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 10000;

            IF @Result < 0
                THROW 50001,
                    N'Tài khoản đang được cập nhật. Vui lòng thử lại.',
                    1;";

        await command.ExecuteNonQueryAsync();

        await AuthorizeAsync(
            connection,
            transaction);
    }

    private static async Task AuditAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string text)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        command.CommandText = @"
            INSERT dbo.NhatKyHeThong
            (
                MaNguoiDung,
                HanhDong,
                NoiDung
            )
            VALUES (@User, N'Quản trị', @Text);";

        command.Parameters.Add(
            "@User", SqlDbType.VarChar, 20).Value =
            UserId();

        command.Parameters.Add(
            "@Text", SqlDbType.NVarChar, -1).Value =
            text;

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<DataTable> QueryAsync(
        string sql)
    {
        using var connection = Database.CreateConnection();

        await connection.OpenAsync();
        await AuthorizeAsync(connection);

        using var command = connection.CreateCommand();

        command.CommandText = sql;

        command.Parameters.Add(
            "@User", SqlDbType.VarChar, 20).Value =
            UserId();

        using var reader =
            await command.ExecuteReaderAsync();

        var table = new DataTable();
        table.Load(reader);

        return table;
    }

    public static Task<DataTable> GetNotificationsAsync()
    {
        return QueryAsync(@"
            SELECT t.TieuDe, t.NoiDung
            FROM dbo.ThongBao t
            JOIN dbo.ThongBaoNguoiNhan n
                ON n.MaThongBao = t.MaThongBao
            WHERE n.MaNguoiNhan = @User
            ORDER BY t.NgayGui DESC;");
    }

    public static Task<DataTable> GetAccountsAsync()
    {
        return QueryAsync(@"
            SELECT
                MaNguoiDung,
                TenDangNhap,
                HoTen,
                Email,
                SoDienThoai,
                VaiTro,
                TrangThai
            FROM dbo.NguoiDung
            ORDER BY NgayTao DESC, TenDangNhap;");
    }

    public static Task<DataTable> GetDaysAsync()
    {
        return QueryAsync(@"
            SELECT MaDayTro, TenDayTro
            FROM dbo.DayTro
            WHERE TrangThai = N'Đang hoạt động'
            ORDER BY TenDayTro;");
    }

    public static Task<DataTable> GetLogsAsync()
    {
        return QueryAsync(@"
            SELECT
                MaNhatKy,
                HanhDong,
                NoiDung,
                ThoiGian,
                CASE
                    WHEN HanhDong = N'Log truy cập'
                        THEN N'Log truy cập'
                    WHEN HanhDong = N'Log lỗi runtime'
                        THEN N'Log lỗi runtime'
                    ELSE N'Quản trị'
                END AS LoaiNhatKy
            FROM dbo.NhatKyHeThong
            ORDER BY MaNhatKy DESC;");
    }

    public static async Task SetStatusAsync(
        string id,
        string expected,
        string next)
    {
        if (next is not ("Hoạt động" or "Khóa"))
        {
            throw new InvalidOperationException(
                "Trạng thái không hợp lệ.");
        }

        if (id == UserId())
        {
            throw new InvalidOperationException(
                "Không thể tự vô hiệu hóa tài khoản "
                + "quản trị đang đăng nhập.");
        }

        using var connection = Database.CreateConnection();
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        await LockAccountsAsync(
            connection,
            transaction);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        command.CommandText = @"
            IF @Next <> N'Hoạt động'
                AND EXISTS
                (
                    SELECT 1
                    FROM dbo.NguoiDung
                    WHERE MaNguoiDung = @Id
                      AND VaiTro = N'Admin'
                      AND TrangThai = N'Hoạt động'
                )
                AND
                (
                    SELECT COUNT(*)
                    FROM dbo.NguoiDung
                    WHERE VaiTro = N'Admin'
                      AND TrangThai = N'Hoạt động'
                ) <= 1
                THROW 50001,
                    N'Không thể khóa Admin hoạt động cuối cùng.',
                    1;

            UPDATE dbo.NguoiDung
            SET TrangThai = @Next
            WHERE MaNguoiDung = @Id
              AND TrangThai = @Expected;";

        command.Parameters.Add(
            "@Id", SqlDbType.VarChar, 20).Value = id;

        command.Parameters.Add(
            "@Expected", SqlDbType.NVarChar, 30).Value =
            expected;

        command.Parameters.Add(
            "@Next", SqlDbType.NVarChar, 30).Value =
            next;

        if (await command.ExecuteNonQueryAsync() != 1)
        {
            throw new InvalidOperationException(
                "Tài khoản đã thay đổi. "
                + "Vui lòng tải lại danh sách.");
        }

        await AuditAsync(
            connection,
            transaction,
            $"Đổi trạng thái tài khoản {id}: "
                + $"{expected} → {next}.");

        transaction.Commit();
    }

    private static string NewId(string prefix)
    {
        return prefix
            + Guid.NewGuid().ToString("N")[..18];
    }

    private static async Task AddProfileAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string id,
        string role,
        string? day)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        command.Parameters.Add(
            "@User", SqlDbType.VarChar, 20).Value = id;

        if (role is
            "Nhân viên quản lý" or "Nhân viên kỹ thuật")
        {
            command.CommandText = @"
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.NhanVien
                    WHERE MaNguoiDung = @User
                )
                    INSERT dbo.NhanVien
                    (
                        MaNhanVien,
                        MaNguoiDung,
                        MaDayTro,
                        ChucVu
                    )
                    VALUES (@Id, @User, @Day, @Position);
                ELSE
                    UPDATE dbo.NhanVien
                    SET
                        ChucVu = @Position,
                        MaDayTro = @Day,
                        TrangThaiLamViec = N'Đang làm'
                    WHERE MaNguoiDung = @User;";

            command.Parameters.Add(
                "@Id", SqlDbType.VarChar, 20).Value =
                NewId("NV");

            command.Parameters.Add(
                "@Day", SqlDbType.VarChar, 20).Value =
                (object?)day ?? DBNull.Value;

            command.Parameters.Add(
                "@Position", SqlDbType.NVarChar, 50).Value =
                role == "Nhân viên quản lý"
                    ? "Quản lý"
                    : "Kỹ thuật";
        }
        else if (role == "Khách thuê")
        {
            command.CommandText = @"
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.KhachThue
                    WHERE MaNguoiDung = @User
                )
                    INSERT dbo.KhachThue
                    (
                        MaKhachThue,
                        MaNguoiDung
                    )
                    VALUES (@Id, @User);
                ELSE
                    UPDATE dbo.KhachThue
                    SET TrangThai = N'Đang thuê'
                    WHERE MaNguoiDung = @User;";

            command.Parameters.Add(
                "@Id", SqlDbType.VarChar, 20).Value =
                NewId("KT");
        }
        else
        {
            return;
        }

        await command.ExecuteNonQueryAsync();
    }

    public static async Task SetRoleAsync(
        string id,
        string expected,
        string next,
        string? day)
    {
        if (!Roles.Contains(next))
        {
            throw new InvalidOperationException(
                "Vai trò không hợp lệ.");
        }

        if (id == UserId() && next != "Admin")
        {
            throw new InvalidOperationException(
                "Không thể tự bỏ quyền Admin đang đăng nhập.");
        }

        if (expected == next)
            return;

        if (next == "Nhân viên quản lý"
            && string.IsNullOrWhiteSpace(day))
        {
            throw new InvalidOperationException(
                "Chọn dãy trọ cho nhân viên quản lý.");
        }

        using var connection = Database.CreateConnection();
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        await LockAccountsAsync(
            connection,
            transaction);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        command.CommandText = @"
            IF @Next <> N'Chủ nhà trọ'
                AND EXISTS
                (
                    SELECT 1
                    FROM dbo.DayTro
                    WHERE MaChuTro = @Id
                )
                THROW 50001,
                    N'Cần bàn giao các dãy trọ trước khi đổi vai trò Chủ nhà trọ.',
                    1;

            IF @Next <> N'Nhân viên quản lý'
                AND EXISTS
                (
                    SELECT 1
                    FROM dbo.NhanVien
                    WHERE MaNguoiDung = @Id
                      AND ChucVu = N'Quản lý'
                      AND MaDayTro IS NOT NULL
                      AND TrangThaiLamViec = N'Đang làm'
                )
                THROW 50001,
                    N'Cần bàn giao dãy trọ đang quản lý trước khi đổi vai trò.',
                    1;

            IF @Next <> N'Nhân viên kỹ thuật'
                AND EXISTS
                (
                    SELECT 1
                    FROM dbo.ChiTietSuaChua ct
                    JOIN dbo.NhanVien nv
                        ON nv.MaNhanVien = ct.MaNhanVien
                    WHERE nv.MaNguoiDung = @Id
                      AND ct.TrangThai <> N'Hoàn thành'
                )
                THROW 50001,
                    N'Cần hoàn tất hoặc bàn giao công việc kỹ thuật trước khi đổi vai trò.',
                    1;

            IF @Next <> N'Khách thuê'
                AND EXISTS
                (
                    SELECT 1
                    FROM dbo.HopDongThue hd
                    JOIN dbo.KhachThue kt
                        ON kt.MaKhachThue = hd.MaKhachThue
                    WHERE kt.MaNguoiDung = @Id
                      AND hd.TrangThai = N'Hiệu lực'
                )
                THROW 50001,
                    N'Cần xử lý hợp đồng đang hiệu lực trước khi đổi vai trò Khách thuê.',
                    1;

            IF @Expected = N'Admin'
                AND EXISTS
                (
                    SELECT 1
                    FROM dbo.NguoiDung
                    WHERE MaNguoiDung = @Id
                      AND TrangThai = N'Hoạt động'
                )
                AND
                (
                    SELECT COUNT(*)
                    FROM dbo.NguoiDung
                    WHERE VaiTro = N'Admin'
                      AND TrangThai = N'Hoạt động'
                ) <= 1
                THROW 50001,
                    N'Không thể bỏ quyền Admin hoạt động cuối cùng.',
                    1;

            UPDATE dbo.NguoiDung
            SET VaiTro = @Next
            WHERE MaNguoiDung = @Id
              AND VaiTro = @Expected;";

        command.Parameters.Add(
            "@Id", SqlDbType.VarChar, 20).Value = id;

        command.Parameters.Add(
            "@Expected", SqlDbType.NVarChar, 50).Value =
            expected;

        command.Parameters.Add(
            "@Next", SqlDbType.NVarChar, 50).Value =
            next;

        if (await command.ExecuteNonQueryAsync() != 1)
        {
            throw new InvalidOperationException(
                "Tài khoản đã thay đổi. Hãy tải lại.");
        }

        command.CommandText = @"
            UPDATE dbo.NhanVien
            SET TrangThaiLamViec = N'Tạm nghỉ'
            WHERE MaNguoiDung = @Id
              AND @Next NOT IN
              (
                  N'Nhân viên quản lý',
                  N'Nhân viên kỹ thuật'
              );

            UPDATE dbo.KhachThue
            SET TrangThai = N'Ngừng hoạt động'
            WHERE MaNguoiDung = @Id
              AND @Next <> N'Khách thuê';";

        await command.ExecuteNonQueryAsync();

        await AddProfileAsync(
            connection,
            transaction,
            id,
            next,
            day);

        await AuditAsync(
            connection,
            transaction,
            $"Đổi vai trò tài khoản {id}: "
                + $"{expected} → {next}.");

        transaction.Commit();
    }

    private static SmtpClient CreateSmtp(
        out string from)
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

    public static async Task<CreatedAccount>
        CreateAccountAsync(
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

        if (!Regex.IsMatch(
            login,
            @"^[A-Za-z0-9_.-]{3,50}$"))
        {
            throw new InvalidOperationException(
                "Tên đăng nhập: 3–50 ký tự chữ, số, "
                + "_, . hoặc -.");
        }

        if (email.Length > 100
            || email.Any(character => character > 127)
            || new MailAddress(email).Address != email)
        {
            throw new InvalidOperationException(
                "Email không hợp lệ.");
        }

        if (!Regex.IsMatch(phone, @"^[0-9]{9,15}$"))
        {
            throw new InvalidOperationException(
                "SĐT cần 9–15 chữ số.");
        }

        if (!Roles.Contains(role)
            || fullName.Length is < 1 or > 100
            || !Regex.IsMatch(cccd, @"^[0-9]{12}$"))
        {
            throw new InvalidOperationException(
                "Chọn vai trò, nhập họ tên "
                + "(tối đa 100 ký tự) và CCCD 12 chữ số.");
        }

        if (role == "Nhân viên quản lý"
            && string.IsNullOrWhiteSpace(day))
        {
            throw new InvalidOperationException(
                "Chọn dãy trọ cho nhân viên quản lý.");
        }

        // Kiểm tra cấu hình email trước khi tạo tài khoản.
        using var smtp = CreateSmtp(out string from);

        string password =
            Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(18))
            + "@1aA";

        string id = NewId("ND");

        using (var connection = Database.CreateConnection())
        {
            await connection.OpenAsync();

            using var transaction =
                connection.BeginTransaction();

            await LockAccountsAsync(
                connection,
                transaction);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = @"
                INSERT dbo.NguoiDung
                (
                    MaNguoiDung,
                    TenDangNhap,
                    MatKhau,
                    HoTen,
                    CCCD,
                    Email,
                    SoDienThoai,
                    VaiTro
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
                    @Role
                );";

            command.Parameters.Add(
                "@Id", SqlDbType.VarChar, 20).Value = id;

            command.Parameters.Add(
                "@Login", SqlDbType.VarChar, 50).Value =
                login;

            command.Parameters.Add(
                "@Password", SqlDbType.VarChar, 255).Value =
                password;

            command.Parameters.Add(
                "@Name", SqlDbType.NVarChar, 100).Value =
                fullName;

            command.Parameters.Add(
                "@CCCD", SqlDbType.VarChar, 12).Value =
                cccd;

            command.Parameters.Add(
                "@Email", SqlDbType.VarChar, 100).Value =
                email;

            command.Parameters.Add(
                "@Phone", SqlDbType.VarChar, 15).Value =
                phone;

            command.Parameters.Add(
                "@Role", SqlDbType.NVarChar, 50).Value =
                role;

            await command.ExecuteNonQueryAsync();

            await AddProfileAsync(
                connection,
                transaction,
                id,
                role,
                day);

            await AuditAsync(
                connection,
                transaction,
                $"Tạo tài khoản {login}, vai trò {role}.");

            transaction.Commit();
        }

        try
        {
            using var mail = new MailMessage(
                from,
                email,
                "Tài khoản hệ thống quản lý nhà trọ",
                $"Xin chào {fullName},\n"
                    + $"Tên đăng nhập: {login}\n"
                    + $"Mật khẩu: {password}\n"
                    + "Vui lòng đổi mật khẩu sau khi đăng nhập.");

            await smtp.SendMailAsync(mail);

            return new CreatedAccount(id, true);
        }
        catch
        {
            await SystemLogService.TryWriteAsync(
                UserId(),
                "Log lỗi runtime",
                $"Đã tạo tài khoản {id} "
                    + "nhưng chưa gửi được email cấp mật khẩu.");

            return new CreatedAccount(id, false);
        }
    }

    public static async Task ResendEmailAsync(
        string id)
    {
        using var connection = Database.CreateConnection();

        await connection.OpenAsync();
        await AuthorizeAsync(connection);

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
            UserId(),
            "Quản trị",
            $"Gửi lại email cấp mật khẩu cho tài khoản {id}.");
    }

    public static async Task ChangePasswordAsync(
        string oldPassword,
        string next)
    {
        if (next.Length is < 8 or > 255
            || next.Any(character => character > 127)
            || next == oldPassword)
        {
            throw new InvalidOperationException(
                "Mật khẩu mới cần 8–255 ký tự ASCII "
                + "và khác mật khẩu cũ.");
        }

        using var connection = Database.CreateConnection();
        await connection.OpenAsync();

        using var transaction =
            connection.BeginTransaction();

        await LockAccountsAsync(
            connection,
            transaction);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        command.Parameters.Add(
            "@User", SqlDbType.VarChar, 20).Value =
            UserId();

        command.CommandText = @"
            SELECT MatKhau
            FROM dbo.NguoiDung WITH (UPDLOCK, HOLDLOCK)
            WHERE MaNguoiDung = @User;";

        string? current =
            await command.ExecuteScalarAsync() as string;

        if (!string.Equals(
            current,
            oldPassword,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Mật khẩu hiện tại không chính xác.");
        }

        command.CommandText = @"
            UPDATE dbo.NguoiDung
            SET MatKhau = @Password
            WHERE MaNguoiDung = @User;";

        command.Parameters.Add(
            "@Password", SqlDbType.VarChar, 255).Value =
            next;

        await command.ExecuteNonQueryAsync();

        await AuditAsync(
            connection,
            transaction,
            "Admin đổi mật khẩu cá nhân.");

        transaction.Commit();
    }

    private static void CheckBackupPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !Path.IsPathRooted(path)
            || Path.GetExtension(path).ToLowerInvariant()
                != ".bak")
        {
            throw new InvalidOperationException(
                "Nhập đường dẫn tuyệt đối tới file .bak "
                + "trên máy chủ SQL Server.");
        }
    }

    private static string QuoteDatabase(string name)
    {
        return "[" + name.Replace("]", "]]") + "]";
    }

    public static async Task BackupAsync(string path)
    {
        CheckBackupPath(path);

        using var connection = Database.CreateConnection();

        await connection.OpenAsync();
        await AuthorizeAsync(connection);

        using var command = connection.CreateCommand();

        command.CommandText = @"
            SELECT CONVERT(
                int,
                SERVERPROPERTY('EngineEdition'));";

        int edition = Convert.ToInt32(
            await command.ExecuteScalarAsync());

        command.CommandText =
            $"BACKUP DATABASE {QuoteDatabase(connection.Database)} "
            + "TO DISK = @Path "
            + "WITH COPY_ONLY, CHECKSUM"
            + (edition is 2 or 3
                ? ", COMPRESSION;"
                : ";");

        command.CommandTimeout = 600;

        command.Parameters.Add(
            "@Path", SqlDbType.NVarChar, 4000).Value =
            path;

        await command.ExecuteNonQueryAsync();

        await SystemLogService.TryWriteAsync(
            UserId(),
            "Quản trị",
            "Sao lưu CSDL: " + path);
    }

    public static async Task<string> RestoreAsync(
        string path)
    {
        CheckBackupPath(path);

        var builder = new SqlConnectionStringBuilder(
            Database.CreateConnection().ConnectionString);

        string database;

        using (var connection = Database.CreateConnection())
        {
            await connection.OpenAsync();
            await AuthorizeAsync(connection);

            database = connection.Database;

            using var permission =
                connection.CreateCommand();

            permission.CommandText = @"
                SELECT IS_SRVROLEMEMBER('sysadmin');";

            if (Convert.ToInt32(
                await permission.ExecuteScalarAsync()) != 1)
            {
                throw new InvalidOperationException(
                    "Khôi phục cần kết nối SQL Server "
                    + "có quyền sysadmin.");
            }
        }

        builder.InitialCatalog = "master";
        builder.Pooling = false;

        using var master = new SqlConnection(
            builder.ConnectionString);

        await master.OpenAsync();

        using var command = master.CreateCommand();

        command.CommandTimeout = 600;

        command.Parameters.Add(
            "@Path", SqlDbType.NVarChar, 4000).Value =
            path;

        command.CommandText =
            "RESTORE HEADERONLY FROM DISK = @Path;";

        int position = 0;

        using (var reader =
            await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                if (reader["DatabaseName"].ToString()
                        == database
                    && Convert.ToInt32(
                        reader["BackupType"]) == 1)
                {
                    position = Math.Max(
                        position,
                        Convert.ToInt32(
                            reader["Position"]));
                }
            }
        }

        if (position == 0)
        {
            throw new InvalidOperationException(
                "Chỉ khôi phục bản sao lưu đầy đủ "
                + "của đúng CSDL đang sử dụng.");
        }

        command.Parameters.Add(
            "@Position", SqlDbType.Int).Value =
            position;

        command.CommandText = @"
            RESTORE VERIFYONLY
            FROM DISK = @Path
            WITH FILE = @Position, CHECKSUM;";

        await command.ExecuteNonQueryAsync();

        string safety = Path.Combine(
            Path.GetDirectoryName(path)!,
            "TruocKhoiPhuc_"
                + DateTime.Now.ToString("yyyyMMdd_HHmmss")
                + "_"
                + Guid.NewGuid().ToString("N")[..8]
                + ".bak");

        // Không khôi phục nếu chưa sao lưu được dữ liệu hiện tại.
        await BackupAsync(safety);

        string db = QuoteDatabase(database);

        SqlConnection.ClearAllPools();

        try
        {
            command.CommandText =
                $"ALTER DATABASE {db} "
                + "SET SINGLE_USER WITH ROLLBACK IMMEDIATE;";

            await command.ExecuteNonQueryAsync();

            command.CommandText =
                $"RESTORE DATABASE {db} "
                + "FROM DISK = @Path "
                + "WITH FILE = @Position, REPLACE, RECOVERY;";

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            command.CommandText =
                $"ALTER DATABASE {db} SET MULTI_USER;";

            await command.ExecuteNonQueryAsync();
        }

        await SystemLogService.TryWriteAsync(
            null,
            "Quản trị",
            $"Khôi phục từ {path}; bản an toàn: {safety}.");

        return safety;
    }

    public static async Task<int> CleanCacheAsync()
    {
        using var connection = Database.CreateConnection();

        await connection.OpenAsync();
        await AuthorizeAsync(connection);

        string folder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "QuanLyChuoiNhaTro",
            "Cache");

        int count = 0;

        if (Directory.Exists(folder)
            && (File.GetAttributes(folder)
                & FileAttributes.ReparsePoint) == 0)
        {
            foreach (string file
                in Directory.EnumerateFiles(folder))
            {
                if ((File.GetAttributes(file)
                    & FileAttributes.ReparsePoint) == 0)
                {
                    File.Delete(file);
                    count++;
                }
            }
        }

        await SystemLogService.TryWriteAsync(
            UserId(),
            "Quản trị",
            $"Dọn dẹp {count} tệp cache của ứng dụng.");

        return count;
    }

    public static void ExportLogs(
        DataTable table,
        string path,
        bool excel)
    {
        string Clean(object value)
        {
            return Regex.Replace(
                value.ToString() ?? "",
                @"[\x00-\x08\x0B\x0C\x0E-\x1F\uFFFE\uFFFF]",
                "");
        }

        var rows = new List<string[]>
        {
            new[]
            {
                "Loại nhật ký",
                "Nội dung nhật ký",
                "Thời gian"
            }
        };

        rows.AddRange(
            table.AsEnumerable().Select(row => new[]
            {
                Clean(row["LoaiNhatKy"]),
                Clean(row["NoiDung"]),
                Convert.ToDateTime(row["ThoiGian"])
                    .ToString("dd/MM/yyyy HH:mm:ss")
            }));

        if (!excel)
        {
            File.WriteAllLines(
                path,
                rows.Select(row =>
                    string.Join(
                        "\t",
                        row.Select(value =>
                            value.Replace("\t", " ")
                                .Replace("\r", " ")
                                .Replace("\n", " ")))),
                new UTF8Encoding(true));

            return;
        }

        // Tạo workbook .xlsx bằng Open XML,
        // không yêu cầu cài thêm thư viện Excel.
        using var zip = ZipFile.Open(
            path,
            ZipArchiveMode.Create);

        void Part(string name, string content)
        {
            using var writer = new StreamWriter(
                zip.CreateEntry(name).Open(),
                new UTF8Encoding(false));

            writer.Write(content);
        }

        Part(
            "[Content_Types].xml",
            """
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
            </Types>
            """);

        Part(
            "_rels/.rels",
            """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);

        Part(
            "xl/workbook.xml",
            """
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                      xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets>
                <sheet name="Nhật ký" sheetId="1" r:id="rId1"/>
              </sheets>
            </workbook>
            """);

        Part(
            "xl/_rels/workbook.xml.rels",
            """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
            </Relationships>
            """);

        var xml = new StringBuilder(
            """
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <cols>
                <col min="1" max="1" width="22" customWidth="1"/>
                <col min="2" max="2" width="90" customWidth="1"/>
                <col min="3" max="3" width="24" customWidth="1"/>
              </cols>
              <sheetData>
            """);

        for (int i = 0; i < rows.Count; i++)
        {
            xml.Append($"<row r=\"{i + 1}\">");

            for (int j = 0; j < 3; j++)
            {
                string cell = $"{(char)('A' + j)}{i + 1}";

                xml.Append(
                    $"<c r=\"{cell}\" t=\"inlineStr\">"
                    + "<is><t xml:space=\"preserve\">"
                    + SecurityElement.Escape(rows[i][j])
                    + "</t></is></c>");
            }

            xml.Append("</row>");
        }

        xml.Append("</sheetData></worksheet>");

        Part(
            "xl/worksheets/sheet1.xml",
            xml.ToString());
    }
}