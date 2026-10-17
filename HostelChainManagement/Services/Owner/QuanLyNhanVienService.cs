using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Owner
{
    public static class QuanLyNhanVienService
    {
        public static async Task<DataTable> GetDanhSachNhanVienAsync()
        {
            var user = GetChuTro();

            const string sql = @"
                SELECT
                    nv.MaNhanVien,
                    nd.MaNguoiDung,
                    nd.HoTen,
                    nd.SoDienThoai,
                    nd.CCCD,
                    nv.ChucVu,
                    nv.TrangThaiLamViec,
                    nv.MaDayTro,
                    d.TenDayTro
                FROM dbo.ChuTroNhanVien AS ct
                INNER JOIN dbo.NhanVien AS nv
                    ON nv.MaNhanVien = ct.MaNhanVien
                INNER JOIN dbo.NguoiDung AS nd
                    ON nd.MaNguoiDung = nv.MaNguoiDung
                LEFT JOIN dbo.DayTro AS d
                    ON d.MaDayTro = nv.MaDayTro
                WHERE ct.MaChuTro = @MaChuTro
                  AND
                  (
                      nv.MaDayTro IS NULL
                      OR d.MaChuTro = @MaChuTro
                  )
                ORDER BY nd.HoTen, nv.MaNhanVien;";

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;

            command.Parameters.Add(
                "@MaChuTro",
                SqlDbType.VarChar,
                20).Value = user.MaNguoiDung;

            await connection.OpenAsync();

            if (!await SoDoChuTroService.CoBangLienKetAsync(connection))
            {
                bool motChuTro =
                    await SoDoChuTroService.LaChuTroDuyNhatAsync(
                        connection, user.MaNguoiDung);

                command.CommandText = @"
                    SELECT
                        nv.MaNhanVien,
                        nd.MaNguoiDung,
                        nd.HoTen,
                        nd.SoDienThoai,
                        nd.CCCD,
                        nv.ChucVu,
                        nv.TrangThaiLamViec,
                        nv.MaDayTro,
                        d.TenDayTro
                    FROM dbo.NhanVien AS nv
                    INNER JOIN dbo.NguoiDung AS nd
                        ON nd.MaNguoiDung = nv.MaNguoiDung
                    LEFT JOIN dbo.DayTro AS d
                        ON d.MaDayTro = nv.MaDayTro
                    WHERE
                    (
                        d.MaChuTro = @MaChuTro
                        OR (@MotChuTro = 1 AND nv.MaDayTro IS NULL)
                    )
                    ORDER BY nd.HoTen, nv.MaNhanVien;";

                command.Parameters.Add(
                    "@MotChuTro", SqlDbType.Bit)
                    .Value = motChuTro;
            }

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