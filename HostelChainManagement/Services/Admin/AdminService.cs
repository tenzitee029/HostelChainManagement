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
}