using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public static class LapHopDongService
    {
        public static async Task<(DataTable Phong, DataTable Khach)> TaiDuLieuAsync(
            string maQuanLy)
        {
            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            var phong = new DataTable();
            var khach = new DataTable();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT p.MaPhong, p.GiaThue, d.TenDayTro,
                           d.TenDayTro + N' - Phòng ' + p.SoPhong
                               AS TenHienThi
                    FROM dbo.PhongTro p
                    JOIN dbo.DayTro d ON d.MaDayTro = p.MaDayTro
                    WHERE p.TrangThai = N'Trống'
                      AND EXISTS (
                          SELECT 1 FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @QuanLy
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      )
                    ORDER BY d.TenDayTro, p.SoPhong;";

                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = maQuanLy;

                using var reader = await command.ExecuteReaderAsync();
                phong.Load(reader);
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT k.MaKhachThue,
                           n.HoTen + N' - ' + n.CCCD AS TenHienThi
                    FROM dbo.KhachThue k
                    JOIN dbo.NguoiDung n
                      ON n.MaNguoiDung = k.MaNguoiDung
                    WHERE n.VaiTro = N'Khách thuê'
                      AND n.TrangThai = N'Hoạt động'
                      AND (
                          EXISTS (
                              SELECT 1 FROM dbo.NhanVien nv
                              WHERE nv.MaDayTro = k.MaDayTro
                                AND nv.MaNguoiDung = @QuanLy
                                AND nv.ChucVu = N'Quản lý'
                                AND nv.TrangThaiLamViec = N'Đang làm'
                          )
                          OR EXISTS (
                              SELECT 1
                              FROM dbo.HopDongThue h
                              JOIN dbo.PhongTro p ON p.MaPhong = h.MaPhong
                              JOIN dbo.NhanVien nv
                                ON nv.MaDayTro = p.MaDayTro
                              WHERE h.MaKhachThue = k.MaKhachThue
                                AND nv.MaNguoiDung = @QuanLy
                                AND nv.ChucVu = N'Quản lý'
                                AND nv.TrangThaiLamViec = N'Đang làm'
                          )
                      )
                    ORDER BY n.HoTen;";

                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = maQuanLy;

                using var reader = await command.ExecuteReaderAsync();
                khach.Load(reader);
            }

            return (phong, khach);
        }

        public static async Task LuuAsync(
            string maQuanLy,
            string maHopDong,
            string maPhong,
            string maKhach,
            string daiDien,
            DateTime batDau,
            DateTime ketThuc,
            decimal tienCoc,
            int ky,
            Func<string> layDieuKhoan)
        {
            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            // Khóa kiểm tra và ghi trong cùng transaction.
            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = @"
                DECLARE @MaDay VARCHAR(20);
                DECLARE @GiaThue DECIMAL(18,2);
                DECLARE @TrangThaiPhong NVARCHAR(50);
                DECLARE @TaiKhoanKhach VARCHAR(20);

                SELECT @MaDay = p.MaDayTro,
                       @GiaThue = p.GiaThue,
                       @TrangThaiPhong = p.TrangThai
                FROM dbo.PhongTro p WITH (UPDLOCK, HOLDLOCK)
                WHERE p.MaPhong = @MaPhong;

                IF @MaDay IS NULL OR @TrangThaiPhong <> N'Trống'
                    THROW 50001, N'Phòng không còn trống.', 1;

                IF @GiaThue IS NULL OR @GiaThue <= 0
                    THROW 50002, N'Giá thuê phòng không hợp lệ.', 1;

                IF NOT EXISTS (
                    SELECT 1 FROM dbo.NhanVien nv
                    WHERE nv.MaDayTro = @MaDay
                      AND nv.MaNguoiDung = @QuanLy
                      AND nv.ChucVu = N'Quản lý'
                      AND nv.TrangThaiLamViec = N'Đang làm'
                )
                    THROW 50003, N'Bạn không có quyền quản lý phòng này.', 1;

                SELECT @TaiKhoanKhach = k.MaNguoiDung
                FROM dbo.KhachThue k WITH (UPDLOCK, HOLDLOCK)
                WHERE k.MaKhachThue = @MaKhach;

                IF @TaiKhoanKhach IS NULL
                    THROW 50004, N'Khách thuê không còn tồn tại.', 1;

                IF NOT EXISTS (
                    SELECT 1
                    FROM dbo.KhachThue k
                    JOIN dbo.NguoiDung n
                      ON n.MaNguoiDung = k.MaNguoiDung
                    WHERE k.MaKhachThue = @MaKhach
                      AND n.VaiTro = N'Khách thuê'
                      AND n.TrangThai = N'Hoạt động'
                      AND (
                          EXISTS (
                              SELECT 1 FROM dbo.NhanVien nv
                              WHERE nv.MaDayTro = k.MaDayTro
                                AND nv.MaNguoiDung = @QuanLy
                                AND nv.ChucVu = N'Quản lý'
                                AND nv.TrangThaiLamViec = N'Đang làm'
                          )
                          OR EXISTS (
                              SELECT 1
                              FROM dbo.HopDongThue h
                              JOIN dbo.PhongTro p ON p.MaPhong = h.MaPhong
                              JOIN dbo.NhanVien nv
                                ON nv.MaDayTro = p.MaDayTro
                              WHERE h.MaKhachThue = k.MaKhachThue
                                AND nv.MaNguoiDung = @QuanLy
                                AND nv.ChucVu = N'Quản lý'
                                AND nv.TrangThaiLamViec = N'Đang làm'
                          )
                      )
                )
                    THROW 50005, N'Bạn không có quyền với khách thuê này.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM dbo.HopDongThue h WITH (UPDLOCK, HOLDLOCK)
                    WHERE h.TrangThai IN (N'Hiệu lực', N'Yêu cầu trả phòng')
                      AND (h.MaPhong = @MaPhong
                           OR h.MaKhachThue = @MaKhach)
                      AND h.NgayBatDau <= @KetThuc
                      AND h.NgayKetThuc >= @BatDau
                )
                    THROW 50006,
                        N'Phòng hoặc khách thuê đã có hợp đồng trùng thời gian.', 1;

                INSERT INTO dbo.HopDongThue
                    (MaHopDong, MaPhong, MaKhachThue,
                     DaiDienBenChoThue, NgayBatDau, NgayKetThuc,
                     TienCoc, GiaThue, KyThanhToan, DieuKhoan, TrangThai)
                VALUES
                    (@MaHopDong, @MaPhong, @MaKhach,
                     @DaiDien, @BatDau, @KetThuc,
                     @TienCoc, @GiaThue, @Ky, @DieuKhoan, N'Hiệu lực');

                IF @BatDau <= CAST(GETDATE() AS DATE)
                   AND @KetThuc >= CAST(GETDATE() AS DATE)
                BEGIN
                    UPDATE dbo.PhongTro
                    SET TrangThai = N'Đang thuê'
                    WHERE MaPhong = @MaPhong;
                END;";

            command.Parameters.Add(
                "@QuanLy", SqlDbType.VarChar, 20)
                .Value = maQuanLy;
            command.Parameters.Add(
                "@MaHopDong", SqlDbType.VarChar, 20)
                .Value = maHopDong;
            command.Parameters.Add(
                "@MaPhong", SqlDbType.VarChar, 20).Value = maPhong;
            command.Parameters.Add(
                "@MaKhach", SqlDbType.VarChar, 20).Value = maKhach;
            command.Parameters.Add(
                "@DaiDien", SqlDbType.NVarChar, 100).Value = daiDien;
            command.Parameters.Add(
                "@BatDau", SqlDbType.Date).Value = batDau;
            command.Parameters.Add(
                "@KetThuc", SqlDbType.Date).Value = ketThuc;
            command.Parameters.Add(
                "@Ky", SqlDbType.Int).Value = ky;

            var thamSoCoc = command.Parameters.Add(
                "@TienCoc", SqlDbType.Decimal);
            thamSoCoc.Precision = 18;
            thamSoCoc.Scale = 2;
            thamSoCoc.Value = tienCoc;

            string dieuKhoan = layDieuKhoan();

            command.Parameters.Add(
                "@DieuKhoan", SqlDbType.NVarChar, -1)
                .Value = string.IsNullOrWhiteSpace(dieuKhoan)
                    ? (object)DBNull.Value
                    : dieuKhoan.Trim();

            await command.ExecuteNonQueryAsync();
            transaction.Commit();
        }
    }
}