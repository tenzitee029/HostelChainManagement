using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Models;

namespace QuanLyChuoiNhaTro.Services;

public static class ThongBaoKhachThueService
{
    private static LoggedInUser RequireUser()
    {
        var user = UserSession.CurrentUser;

        if (user == null || user.VaiTro != "Khách thuê")
        {
            throw new InvalidOperationException(
                "Bạn phải đăng nhập bằng tài khoản Khách thuê.");
        }

        return user;
    }

    public static async Task<DataTable> GetAsync()
    {
        var user = RequireUser();

        using var connection = Database.CreateConnection();
        using var command = connection.CreateCommand();

        command.CommandText = @"
            SELECT
                t.MaThongBao,
                t.TieuDe,
                t.NoiDung,
                t.NgayGui,
                u.HoTen AS NguoiGui,
                r.DaDoc,
                r.NgayDoc

            FROM dbo.ThongBao AS t

            JOIN dbo.ThongBaoNguoiNhan AS r
                ON r.MaThongBao = t.MaThongBao

            JOIN dbo.NguoiDung AS u
                ON u.MaNguoiDung = t.MaNguoiGui

            WHERE r.MaNguoiNhan = @User

            ORDER BY
                r.DaDoc ASC,
                t.NgayGui DESC,
                t.MaThongBao;";

        command.Parameters.Add(
            "@User",
            SqlDbType.VarChar,
            20).Value = user.MaNguoiDung;

        await connection.OpenAsync();

        using var reader = await command.ExecuteReaderAsync();

        var table = new DataTable();
        table.Load(reader);

        return table;
    }

    public static async Task<int> CountUnreadAsync()
    {
        var user = RequireUser();

        using var connection = Database.CreateConnection();
        using var command = connection.CreateCommand();

        command.CommandText = @"
            SELECT COUNT(*)
            FROM dbo.ThongBaoNguoiNhan
            WHERE MaNguoiNhan = @User
              AND DaDoc = 0;";

        command.Parameters.Add(
            "@User",
            SqlDbType.VarChar,
            20).Value = user.MaNguoiDung;

        await connection.OpenAsync();

        return Convert.ToInt32(
            await command.ExecuteScalarAsync());
    }

    public static async Task<DateTime> MarkReadAsync(string id)
    {
        var user = RequireUser();

        if (string.IsNullOrWhiteSpace(id) || id.Length > 20)
        {
            throw new InvalidOperationException(
                "Mã thông báo không hợp lệ.");
        }

        using var connection = Database.CreateConnection();
        using var command = connection.CreateCommand();

        command.CommandText = @"
            UPDATE dbo.ThongBaoNguoiNhan
            SET DaDoc = 1,
                NgayDoc = COALESCE(NgayDoc, GETDATE())

            OUTPUT inserted.NgayDoc

            WHERE MaThongBao = @Id
              AND MaNguoiNhan = @User;";

        command.Parameters.Add(
            "@Id",
            SqlDbType.VarChar,
            20).Value = id;

        command.Parameters.Add(
            "@User",
            SqlDbType.VarChar,
            20).Value = user.MaNguoiDung;

        await connection.OpenAsync();

        object? result = await command.ExecuteScalarAsync();

        if (result is not DateTime date)
        {
            throw new InvalidOperationException(
                "Không tìm thấy thông báo thuộc tài khoản của bạn.");
        }

        return date;
    }

    public static async Task MarkAllReadAsync()
    {
        var user = RequireUser();

        using var connection = Database.CreateConnection();
        using var command = connection.CreateCommand();

        command.CommandText = @"
            UPDATE dbo.ThongBaoNguoiNhan
            SET DaDoc = 1,
                NgayDoc = COALESCE(NgayDoc, GETDATE())

            WHERE MaNguoiNhan = @User
              AND DaDoc = 0;";

        command.Parameters.Add(
            "@User",
            SqlDbType.VarChar,
            20).Value = user.MaNguoiDung;

        await connection.OpenAsync();

        await command.ExecuteNonQueryAsync();
    }
}