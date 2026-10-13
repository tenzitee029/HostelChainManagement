using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace QuanLyChuoiNhaTro.Services
{
    internal static class SoDoChuTroService
    {
        internal static async Task<bool> CoBangLienKetAsync(
            SqlConnection connection,
            SqlTransaction? transaction = null)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                SELECT CASE
                    WHEN OBJECT_ID(N'dbo.ChuTroNhanVien', N'U') IS NULL
                    THEN CAST(0 AS BIT)
                    ELSE CAST(1 AS BIT)
                END;";

            return Convert.ToBoolean(await command.ExecuteScalarAsync());
        }
    }
}