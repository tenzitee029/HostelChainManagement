using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace QuanLyChuoiNhaTro.Services.Owner
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

        internal static async Task<bool> LaChuTroDuyNhatAsync(
            SqlConnection connection,
            string maChuTro,
            SqlTransaction? transaction = null)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                SELECT CAST(
                    CASE
                        WHEN COUNT_BIG(*) = 1
                         AND MAX(
                             CASE WHEN MaNguoiDung = @MaChuTro
                                 THEN 1 ELSE 0 END
                         ) = 1
                        THEN 1 ELSE 0
                    END AS BIT)
                FROM dbo.NguoiDung WITH (HOLDLOCK)
                WHERE VaiTro = N'Chủ nhà trọ';";
            command.Parameters.Add("@MaChuTro", System.Data.SqlDbType.VarChar, 20)
                .Value = maChuTro;

            return Convert.ToBoolean(await command.ExecuteScalarAsync());
        }
    }
}