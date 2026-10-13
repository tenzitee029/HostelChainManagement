
using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services
{
    public static class HopDongHoaDonChuTroService
    {
        public static async Task<DataTable> LayDanhSachDayTroAsync()
        {
            string maChuTro = LayMaChuTro();

            const string sql = @"
                SELECT
                    d.MaDayTro,
                    d.TenDayTro
                FROM dbo.DayTro AS d
                WHERE d.MaChuTro = @MaChuTro
                ORDER BY d.TenDayTro, d.MaDayTro;";

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;

            command.Parameters.Add(
                "@MaChuTro",
                SqlDbType.VarChar,
                20).Value = maChuTro;

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }

        public static async Task<DataTable> LayDanhSachPhongAsync(
            string? maDayTro)
        {
            string maChuTro = LayMaChuTro();

            const string sql = @"
                SELECT
                    p.MaPhong,
                    p.SoPhong,
                    d.TenDayTro
                FROM dbo.PhongTro AS p
                INNER JOIN dbo.DayTro AS d
                    ON d.MaDayTro = p.MaDayTro
                WHERE d.MaChuTro = @MaChuTro
                  AND
                  (
                      @MaDayTro IS NULL
                      OR d.MaDayTro = @MaDayTro
                  )
                ORDER BY
                    d.TenDayTro,
                    p.SoPhong;";

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;

            command.Parameters.Add(
                "@MaChuTro",
                SqlDbType.VarChar,
                20).Value = maChuTro;

            command.Parameters.Add(
                "@MaDayTro",
                SqlDbType.VarChar,
                20).Value =
                    (object?)maDayTro ?? DBNull.Value;

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }

        public static async Task<DataTable> TimHopDongAsync(
            string? maDayTro,
            string? maPhong,
            DateTime? tuNgay,
            DateTime? denNgay,
            string? trangThai)
        {
            string maChuTro = LayMaChuTro();

            const string sql = @"
                SELECT
                    h.MaHopDong,
                    nd.HoTen AS HoTenKhach,
                    p.SoPhong AS TenPhong,
                    d.TenDayTro,
                    h.NgayBatDau,
                    h.NgayKetThuc,
                    h.GiaThue,
                    h.TrangThai,
                    h.MaPhong,
                    d.MaDayTro
                FROM dbo.HopDongThue AS h
                INNER JOIN dbo.PhongTro AS p
                    ON p.MaPhong = h.MaPhong
                INNER JOIN dbo.DayTro AS d
                    ON d.MaDayTro = p.MaDayTro
                INNER JOIN dbo.KhachThue AS k
                    ON k.MaKhachThue = h.MaKhachThue
                INNER JOIN dbo.NguoiDung AS nd
                    ON nd.MaNguoiDung = k.MaNguoiDung
                WHERE d.MaChuTro = @MaChuTro
                  AND
                  (
                      @MaDayTro IS NULL
                      OR d.MaDayTro = @MaDayTro
                  )
                  AND
                  (
                      @MaPhong IS NULL
                      OR h.MaPhong = @MaPhong
                  )
                  AND
                  (
                      @TuNgay IS NULL
                      OR h.NgayBatDau >= @TuNgay
                  )
                  AND
                  (
                      @DenNgay IS NULL
                      OR h.NgayBatDau < @DenNgay
                  )
                  AND
                  (
                      @TrangThai IS NULL
                      OR h.TrangThai = @TrangThai
                  )
                ORDER BY
                    h.NgayBatDau DESC,
                    h.MaHopDong DESC;";

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;

            ThemBoLoc(
                command,
                maChuTro,
                maDayTro,
                maPhong,
                tuNgay,
                denNgay,
                trangThai);

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }

        public static async Task<DataTable> TimHoaDonAsync(
            string? maDayTro,
            string? maPhong,
            DateTime? tuNgay,
            DateTime? denNgay,
            string? trangThai)
        {
            string maChuTro = LayMaChuTro();

            const string sql = @"
                SELECT
                    hd.MaHoaDon,
                    CONCAT(
                        hd.Thang,
                        '/',
                        hd.Nam
                    ) AS KyThanhToan,
                    hd.Thang,
                    hd.Nam,
                    p.SoPhong AS TenPhong,
                    nd.HoTen AS HoTenKhach,
                    hd.TongTien,
                    hd.TrangThai,
                    hd.NgayLap,
                    hd.HanThanhToan,
                    hd.MaHopDong,
                    hd.MaPhong,
                    d.MaDayTro,
                    d.TenDayTro
                FROM dbo.HoaDon AS hd
                INNER JOIN dbo.HopDongThue AS h
                    ON h.MaHopDong = hd.MaHopDong
                   AND h.MaPhong = hd.MaPhong
                INNER JOIN dbo.PhongTro AS p
                    ON p.MaPhong = hd.MaPhong
                INNER JOIN dbo.DayTro AS d
                    ON d.MaDayTro = p.MaDayTro
                INNER JOIN dbo.KhachThue AS k
                    ON k.MaKhachThue = h.MaKhachThue
                INNER JOIN dbo.NguoiDung AS nd
                    ON nd.MaNguoiDung = k.MaNguoiDung
                WHERE d.MaChuTro = @MaChuTro
                  AND
                  (
                      @MaDayTro IS NULL
                      OR d.MaDayTro = @MaDayTro
                  )
                  AND
                  (
                      @MaPhong IS NULL
                      OR hd.MaPhong = @MaPhong
                  )
                  AND
                  (
                      @TuNgay IS NULL
                      OR DATEFROMPARTS(
                          hd.Nam,
                          hd.Thang,
                          1
                      ) >= @TuNgay
                  )
                  AND
                  (
                      @DenNgay IS NULL
                      OR DATEFROMPARTS(
                          hd.Nam,
                          hd.Thang,
                          1
                      ) < @DenNgay
                  )
                  AND
                  (
                      @TrangThai IS NULL
                      OR hd.TrangThai = @TrangThai
                  )
                ORDER BY
                    hd.Nam DESC,
                    hd.Thang DESC,
                    hd.MaHoaDon DESC;";

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;

            ThemBoLoc(
                command,
                maChuTro,
                maDayTro,
                maPhong,
                tuNgay,
                denNgay,
                trangThai);

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }

        private static void ThemBoLoc(
            SqlCommand command,
            string maChuTro,
            string? maDayTro,
            string? maPhong,
            DateTime? tuNgay,
            DateTime? denNgay,
            string? trangThai)
        {
            command.Parameters.Add(
                "@MaChuTro",
                SqlDbType.VarChar,
                20).Value = maChuTro;

            command.Parameters.Add(
                "@MaDayTro",
                SqlDbType.VarChar,
                20).Value =
                    (object?)maDayTro ?? DBNull.Value;

            command.Parameters.Add(
                "@MaPhong",
                SqlDbType.VarChar,
                20).Value =
                    (object?)maPhong ?? DBNull.Value;

            command.Parameters.Add(
                "@TuNgay",
                SqlDbType.Date).Value =
                    (object?)tuNgay ?? DBNull.Value;

            command.Parameters.Add(
                "@DenNgay",
                SqlDbType.Date).Value =
                    (object?)denNgay ?? DBNull.Value;

            command.Parameters.Add(
                "@TrangThai",
                SqlDbType.NVarChar,
                30).Value =
                    (object?)trangThai ?? DBNull.Value;
        }

        private static string LayMaChuTro()
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException(
                    "Bạn chưa đăng nhập.");

            if (user.VaiTro != "Chủ nhà trọ")
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền xem dữ liệu hợp đồng và hóa đơn.");
            }

            return user.MaNguoiDung;
        }
    }
}
