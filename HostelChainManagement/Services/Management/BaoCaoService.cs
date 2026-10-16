using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public static class BaoCaoService
    {
        public static async Task<DataTable> LayDayTroAsync(
            string maQuanLy)
        {
            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT d.MaDayTro, d.TenDayTro
                FROM dbo.DayTro d
                WHERE EXISTS (
                    SELECT 1 FROM dbo.NhanVien nv
                    WHERE nv.MaDayTro = d.MaDayTro
                      AND nv.MaNguoiDung = @QuanLy
                      AND nv.ChucVu = N'Quản lý'
                      AND nv.TrangThaiLamViec = N'Đang làm'
                )
                ORDER BY d.TenDayTro;";

            command.Parameters.Add(
                "@QuanLy", SqlDbType.VarChar, 20)
                .Value = maQuanLy;

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }

        public static async Task<DataTable> TongHopAsync(
            string maQuanLy,
            string maDay,
            int nam,
            int thang,
            bool theoThang,
            DateTime tuNgay,
            DateTime denNgay)
        {
            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = @"
                DECLARE @Phong TABLE (
                    MaPhong VARCHAR(20) PRIMARY KEY,
                    TrangThai NVARCHAR(30)
                );

                INSERT INTO @Phong
                SELECT p.MaPhong, p.TrangThai
                FROM dbo.PhongTro p
                WHERE (@MaDay = '' OR p.MaDayTro = @MaDay)
                  AND EXISTS (
                      SELECT 1 FROM dbo.NhanVien nv
                      WHERE nv.MaDayTro = p.MaDayTro
                        AND nv.MaNguoiDung = @QuanLy
                        AND nv.ChucVu = N'Quản lý'
                        AND nv.TrangThaiLamViec = N'Đang làm'
                  );

                IF @MaDay <> '' AND NOT EXISTS (
                    SELECT 1 FROM dbo.NhanVien nv
                    WHERE nv.MaDayTro = @MaDay
                      AND nv.MaNguoiDung = @QuanLy
                      AND nv.ChucVu = N'Quản lý'
                      AND nv.TrangThaiLamViec = N'Đang làm'
                )
                    THROW 50001, N'Bạn không còn quyền với dãy đã chọn.', 1;

                SELECT
                    (
                        SELECT COALESCE(SUM(tt.SoTien), 0)
                        FROM dbo.ThanhToan tt
                        JOIN dbo.HoaDon hd ON hd.MaHoaDon = tt.MaHoaDon
                        JOIN @Phong p ON p.MaPhong = hd.MaPhong
                        WHERE tt.NgayThanhToan >= @TuNgay
                          AND tt.NgayThanhToan < @DenNgay
                    ) AS ThucThu,

                    (
                        SELECT COUNT(*)
                        FROM dbo.ThanhToan tt
                        JOIN dbo.HoaDon hd ON hd.MaHoaDon = tt.MaHoaDon
                        JOIN @Phong p ON p.MaPhong = hd.MaPhong
                        WHERE tt.NgayThanhToan >= @TuNgay
                          AND tt.NgayThanhToan < @DenNgay
                    ) AS SoLanThu,

                    (
                        SELECT COUNT(*)
                        FROM dbo.HoaDon hd
                        JOIN @Phong p ON p.MaPhong = hd.MaPhong
                        WHERE hd.Nam = @Nam
                          AND (@TheoThang = 0 OR hd.Thang = @Thang)
                    ) AS SoHoaDon,

                    (
                        SELECT COALESCE(SUM(hd.TongTien), 0)
                        FROM dbo.HoaDon hd
                        JOIN @Phong p ON p.MaPhong = hd.MaPhong
                        WHERE hd.Nam = @Nam
                          AND (@TheoThang = 0 OR hd.Thang = @Thang)
                    ) AS TongHoaDon,

                    (
                        SELECT COALESCE(SUM(
                            CASE WHEN hd.TongTien > COALESCE(tt.DaTra, 0)
                                 THEN hd.TongTien - COALESCE(tt.DaTra, 0)
                                 ELSE 0 END
                        ), 0)
                        FROM dbo.HoaDon hd
                        JOIN @Phong p ON p.MaPhong = hd.MaPhong
                        OUTER APPLY (
                            SELECT SUM(t.SoTien) AS DaTra
                            FROM dbo.ThanhToan t
                            WHERE t.MaHoaDon = hd.MaHoaDon
                        ) tt
                        WHERE hd.Nam = @Nam
                          AND (@TheoThang = 0 OR hd.Thang = @Thang)
                    ) AS ConNo,

                    (SELECT COUNT(*) FROM @Phong) AS TongPhong,
                    (SELECT COUNT(*) FROM @Phong
                     WHERE TrangThai = N'Trống') AS PhongTrong,
                    (SELECT COUNT(*) FROM @Phong
                     WHERE TrangThai = N'Đang thuê') AS DangThue,
                    (SELECT COUNT(*) FROM @Phong
                     WHERE TrangThai = N'Đang sửa chữa') AS DangSua,

                    (
                        SELECT COUNT(*)
                        FROM dbo.PhieuSuCo s
                        JOIN @Phong p ON p.MaPhong = s.MaPhong
                        WHERE s.NgayGui >= @TuNgay AND s.NgayGui < @DenNgay
                    ) AS TongSuCo,

                    (
                        SELECT COUNT(*)
                        FROM dbo.PhieuSuCo s
                        JOIN @Phong p ON p.MaPhong = s.MaPhong
                        WHERE s.NgayGui >= @TuNgay AND s.NgayGui < @DenNgay
                          AND s.TrangThai = N'Đã hoàn thành'
                    ) AS HoanThanh,

                    (
                        SELECT COUNT(*)
                        FROM dbo.PhieuSuCo s
                        JOIN @Phong p ON p.MaPhong = s.MaPhong
                        WHERE s.NgayGui >= @TuNgay AND s.NgayGui < @DenNgay
                          AND s.TrangThai = N'Từ chối'
                    ) AS TuChoi;";

            command.Parameters.Add(
                "@QuanLy", SqlDbType.VarChar, 20)
                .Value = maQuanLy;

            command.Parameters.Add(
                "@MaDay", SqlDbType.VarChar, 20).Value = maDay;

            command.Parameters.Add(
                "@Nam", SqlDbType.Int).Value = nam;

            command.Parameters.Add(
                "@Thang", SqlDbType.Int).Value = thang;

            command.Parameters.Add(
                "@TheoThang", SqlDbType.Bit).Value = theoThang;

            command.Parameters.Add(
                "@TuNgay", SqlDbType.Date).Value = tuNgay;

            command.Parameters.Add(
                "@DenNgay", SqlDbType.Date).Value = denNgay;

            var table = new DataTable();

            using (var reader = await command.ExecuteReaderAsync())
            {
                table.Load(reader);
            }

            transaction.Commit();

            return table;
        }
    }
}