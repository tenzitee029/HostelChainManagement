using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public static class GuiThongBaoService
    {
        public static async Task<(DataTable Day, DataTable Phong)>
            TaiDuLieuAsync(string maQuanLy)
        {
            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            var day = new DataTable();
            var phong = new DataTable();

            using (var command = connection.CreateCommand())
            {
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

                using var reader = await command.ExecuteReaderAsync();
                day.Load(reader);
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT p.MaPhong, p.MaDayTro,
                           N'Phòng ' + p.SoPhong AS TenHienThi
                    FROM dbo.PhongTro p
                    WHERE EXISTS (
                        SELECT 1 FROM dbo.NhanVien nv
                        WHERE nv.MaDayTro = p.MaDayTro
                          AND nv.MaNguoiDung = @QuanLy
                          AND nv.ChucVu = N'Quản lý'
                          AND nv.TrangThaiLamViec = N'Đang làm'
                    )
                    ORDER BY p.SoPhong;";

                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = maQuanLy;

                using var reader = await command.ExecuteReaderAsync();
                phong.Load(reader);
            }

            return (day, phong);
        }

        public static async Task<int> GuiAsync(
            Func<string> layMaThongBao,
            string maQuanLy,
            string maDay,
            string maPhong,
            bool theoPhong,
            string tieuDe,
            string noiDung)
        {
            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = @"
                IF NOT EXISTS (
                    SELECT 1 FROM dbo.NhanVien nv
                    WHERE nv.MaDayTro = @MaDay
                      AND nv.MaNguoiDung = @QuanLy
                      AND nv.ChucVu = N'Quản lý'
                      AND nv.TrangThaiLamViec = N'Đang làm'
                )
                    THROW 50001,
                        N'Bạn không còn quyền gửi tới dãy trọ này.', 1;

                IF @TheoPhong = 1 AND NOT EXISTS (
                    SELECT 1 FROM dbo.PhongTro p
                    WHERE p.MaPhong = @MaPhong
                      AND p.MaDayTro = @MaDay
                )
                    THROW 50002,
                        N'Phòng không thuộc dãy trọ đã chọn.', 1;

                DECLARE @NguoiNhan TABLE (
                    MaNguoiDung VARCHAR(20) PRIMARY KEY
                );

                INSERT INTO @NguoiNhan (MaNguoiDung)
                SELECT DISTINCT n.MaNguoiDung
                FROM dbo.HopDongThue h
                JOIN dbo.PhongTro p ON p.MaPhong = h.MaPhong
                JOIN dbo.KhachThue k
                  ON k.MaKhachThue = h.MaKhachThue
                JOIN dbo.NguoiDung n
                  ON n.MaNguoiDung = k.MaNguoiDung
                WHERE p.MaDayTro = @MaDay
                  AND (@TheoPhong = 0 OR p.MaPhong = @MaPhong)
                  AND h.TrangThai = N'Hiệu lực'
                  AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                  AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                  AND n.VaiTro = N'Khách thuê'
                  AND n.TrangThai = N'Hoạt động';

                IF NOT EXISTS (SELECT 1 FROM @NguoiNhan)
                    THROW 50003,
                        N'Không có khách thuê phù hợp để nhận thông báo.', 1;

                INSERT INTO dbo.ThongBao
                    (MaThongBao, MaNguoiGui, TieuDe, NoiDung)
                VALUES
                    (@MaThongBao, @QuanLy, @TieuDe, @NoiDung);

                INSERT INTO dbo.ThongBaoNguoiNhan
                    (MaThongBao, MaNguoiNhan, DaDoc)
                SELECT @MaThongBao, MaNguoiDung, 0
                FROM @NguoiNhan;

                SELECT COUNT(*) FROM @NguoiNhan;";

            command.Parameters.Add(
                "@MaThongBao", SqlDbType.VarChar, 20)
                .Value = layMaThongBao();

            command.Parameters.Add(
                "@QuanLy", SqlDbType.VarChar, 20)
                .Value = maQuanLy;

            command.Parameters.Add(
                "@MaDay", SqlDbType.VarChar, 20)
                .Value = maDay;

            command.Parameters.Add(
                "@MaPhong", SqlDbType.VarChar, 20)
                .Value = maPhong;

            command.Parameters.Add(
                "@TheoPhong", SqlDbType.Bit)
                .Value = theoPhong;

            command.Parameters.Add(
                "@TieuDe", SqlDbType.NVarChar, 200)
                .Value = tieuDe;

            command.Parameters.Add(
                "@NoiDung", SqlDbType.NVarChar, -1)
                .Value = noiDung;

            int soNguoiNhan = Convert.ToInt32(
                await command.ExecuteScalarAsync());

            transaction.Commit();

            return soNguoiNhan;
        }
    }
}