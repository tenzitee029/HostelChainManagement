using QuanLyChuoiNhaTro.Data;
using System;
using System.Data;
using System.Threading.Tasks;

namespace QuanLyChuoiNhaTro.Services;

public static class SystemLogService
{
    public static async Task TryWriteAsync(
        string? userId,
        string action,
        string content)
    {
        try
        {
            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                INSERT dbo.NhatKyHeThong
                (
                    MaNguoiDung,
                    HanhDong,
                    NoiDung
                )
                VALUES (@User, @Action, @Content);";

            command.Parameters.Add(
                "@User", SqlDbType.VarChar, 20).Value =
                (object?)userId ?? DBNull.Value;

            command.Parameters.Add(
                "@Action", SqlDbType.NVarChar, 100).Value =
                action;

            command.Parameters.Add(
                "@Content", SqlDbType.NVarChar, -1).Value =
                content;

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }
        catch
        {
            // Lỗi ghi nhật ký không làm gián đoạn chức năng chính.
        }
    }
}