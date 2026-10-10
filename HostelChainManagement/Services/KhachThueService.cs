using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services
{
    public sealed class ThongTinPhongKhachThue
    {
        public string SoPhong { get; init; } = "";
        public string TenDayTro { get; init; } = "";
        public string DiaChi { get; init; } = "";
        public decimal DienTich { get; init; }
        public decimal GiaThue { get; init; }

        public DataTable ThietBi { get; init; } = new();
        public DataTable ThanhVien { get; init; } = new();
    }

    public static class KhachThueService
    {
        public static async Task<ThongTinPhongKhachThue?>
            GetPhongCuaToiAsync()
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException(
                    "Bạn chưa đăng nhập.");

            if (user.VaiTro != "Khách thuê")
                throw new InvalidOperationException(
                    "Chức năng chỉ dành cho Khách thuê.");

            const string sql = @"
                SELECT
                    h.MaHopDong,
                    p.MaPhong,
                    p.SoPhong,
                    d.TenDayTro,
                    d.DiaChi,
                    p.DienTich,
                    h.GiaThue
                FROM dbo.KhachThue AS k
                INNER JOIN dbo.HopDongThue AS h
                    ON h.MaKhachThue = k.MaKhachThue
                INNER JOIN dbo.PhongTro AS p
                    ON p.MaPhong = h.MaPhong
                INNER JOIN dbo.DayTro AS d
                    ON d.MaDayTro = p.MaDayTro
                WHERE k.MaNguoiDung = @MaNguoiDung
                  AND h.TrangThai = N'Hiệu lực'
                  AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                  AND h.NgayKetThuc >= CAST(GETDATE() AS DATE);";

            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            string maPhong;
            string maHopDong;
            string soPhong;
            string tenDay;
            string diaChi;
            decimal dienTich;
            decimal giaThue;

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.Parameters.Add(
                    "@MaNguoiDung", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;

                using var reader =
                    await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return null;

                maPhong = reader.GetString(
                    reader.GetOrdinal("MaPhong"));

                maHopDong = reader.GetString(
                    reader.GetOrdinal("MaHopDong"));

                soPhong = reader.GetString(
                    reader.GetOrdinal("SoPhong"));

                tenDay = reader.GetString(
                    reader.GetOrdinal("TenDayTro"));

                diaChi = reader.GetString(
                    reader.GetOrdinal("DiaChi"));

                dienTich = reader.GetDecimal(
                    reader.GetOrdinal("DienTich"));

                giaThue = reader.GetDecimal(
                    reader.GetOrdinal("GiaThue"));

                if (await reader.ReadAsync())
                    throw new InvalidOperationException(
                        "Tài khoản có nhiều hợp đồng đang hiệu lực. " +
                        "Vui lòng liên hệ quản lý để kiểm tra.");
            }

            var thietBi = new DataTable();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT
                        t.TenThietBi,
                        p.SoLuong,
                        p.TinhTrang,
                        p.GhiChu
                    FROM dbo.PhongThietBi AS p
                    INNER JOIN dbo.TrangThietBi AS t
                        ON t.MaThietBi = p.MaThietBi
                    WHERE p.MaPhong = @MaPhong
                    ORDER BY t.TenThietBi;";

                command.Parameters.Add(
                    "@MaPhong", SqlDbType.VarChar, 20)
                    .Value = maPhong;

                using var reader =
                    await command.ExecuteReaderAsync();

                thietBi.Load(reader);
            }

            var thanhVien = new DataTable();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT HoTen, SoDienThoai, QuanHe
                    FROM dbo.ThanhVienO
                    WHERE MaHopDong = @MaHopDong
                      AND TrangThai = N'Đang ở'
                    ORDER BY HoTen;";

                command.Parameters.Add(
                    "@MaHopDong", SqlDbType.VarChar, 20)
                    .Value = maHopDong;

                using var reader =
                    await command.ExecuteReaderAsync();

                thanhVien.Load(reader);
            }

            return new ThongTinPhongKhachThue
            {
                SoPhong = soPhong,
                TenDayTro = tenDay,
                DiaChi = diaChi,
                DienTich = dienTich,
                GiaThue = giaThue,
                ThietBi = thietBi,
                ThanhVien = thanhVien
            };
        }

        public static async Task<DataTable> GetHopDongCuaToiAsync()
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException("Bạn chưa đăng nhập.");

            if (user.VaiTro != "Khách thuê")
                throw new InvalidOperationException(
                    "Chức năng chỉ dành cho Khách thuê.");

            const string sql = @"
        SELECT
            h.MaHopDong,
            h.TrangThai,
            n.HoTen AS TenKhachThue,
            k.CCCD,
            n.SoDienThoai,
            p.SoPhong,
            d.TenDayTro,
            h.NgayKy,
            h.NgayBatDau,
            h.NgayKetThuc,
            h.TienCoc,
            h.GiaThue,
            h.KyThanhToan
        FROM dbo.HopDongThue AS h
        INNER JOIN dbo.KhachThue AS k
            ON k.MaKhachThue = h.MaKhachThue
        INNER JOIN dbo.NguoiDung AS n
            ON n.MaNguoiDung = k.MaNguoiDung
        INNER JOIN dbo.PhongTro AS p
            ON p.MaPhong = h.MaPhong
        INNER JOIN dbo.DayTro AS d
            ON d.MaDayTro = p.MaDayTro
        WHERE k.MaNguoiDung = @MaNguoiDung
        ORDER BY
            CASE WHEN h.TrangThai = N'Hiệu lực'
                 THEN 0 ELSE 1 END,
            h.NgayBatDau DESC,
            h.MaHopDong;";

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;
            command.Parameters.Add(
                "@MaNguoiDung", SqlDbType.VarChar, 20)
                .Value = user.MaNguoiDung;

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }
    }
}