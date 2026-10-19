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

namespace QuanLyChuoiNhaTro.Services.Admin;

public sealed record CreatedAccount(
    string Id,
    bool EmailSent,
    string? InitialPassword = null);

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

    internal static string UserId()
    {
        var user = UserSession.CurrentUser;

        if (user == null || user.VaiTro != "Admin")
        {
            throw new InvalidOperationException(
                "Chức năng chỉ dành cho Admin hệ thống.");
        }

        return user.MaNguoiDung;
    }

    internal static async Task AuthorizeAsync(
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

    internal static async Task LockAccountsAsync(
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
        ORDER BY ThoiGian DESC, MaNhatKy DESC;");
    }
    internal static async Task AuditAsync(
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

    internal static async Task<DataTable> QueryAsync(
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

    public static Task<DataTable> GetDaysAsync()
    {
        return QueryAsync(@"
            SELECT MaDayTro, TenDayTro
            FROM dbo.DayTro
            WHERE TrangThai = N'Đang hoạt động'
            ORDER BY TenDayTro;");
    }

    internal static string NewId(string prefix)
    {
        return prefix
            + Guid.NewGuid().ToString("N")[..18];
    }

    internal static async Task AddProfileAsync(
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