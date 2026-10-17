using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Services.Owner
{
    public static class QuanLyDayTroService
    {
        public static async Task<DataTable> GetDanhSachDayTroAsync()
        {
            var user = GetChuTro();

            const string sql = @"
                SELECT
                    d.MaDayTro,
                    d.TenDayTro,
                    d.DiaChi,
                    d.MoTa,
                    d.QuyDinh,
                    d.TrangThai,
                    (
                        SELECT COUNT(*)
                        FROM dbo.PhongTro AS p
                        WHERE p.MaDayTro = d.MaDayTro
                    ) AS SoPhong
                FROM dbo.DayTro AS d
                WHERE d.MaChuTro = @MaChuTro
                ORDER BY d.TenDayTro, d.MaDayTro;";

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;

            command.Parameters.Add(
                "@MaChuTro",
                SqlDbType.VarChar,
                20).Value = user.MaNguoiDung;

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }

        private static QuanLyChuoiNhaTro.Models.LoggedInUser GetChuTro()
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException(
                    "Bạn chưa đăng nhập.");

            if (user.VaiTro != "Chủ nhà trọ")
            {
                throw new InvalidOperationException(
                    "Chức năng chỉ dành cho Chủ nhà trọ.");
            }

            return user;
        }
    }
}