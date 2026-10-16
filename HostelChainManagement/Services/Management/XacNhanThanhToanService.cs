using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public static class XacNhanThanhToanService
    {
        public static async Task<ThongTinXacNhanThanhToan?> LayThongTinAsync(
            string maHoaDon,
            string maQuanLy)
        {
            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    n.HoTen,
                    hd.TongTien,
                    COALESCE(tt.DaTra, 0) AS DaTra
                FROM dbo.HoaDon hd
                JOIN dbo.HopDongThue h
                  ON h.MaHopDong = hd.MaHopDong
                JOIN dbo.KhachThue k
                  ON k.MaKhachThue = h.MaKhachThue
                JOIN dbo.NguoiDung n
                  ON n.MaNguoiDung = k.MaNguoiDung
                JOIN dbo.PhongTro p
                  ON p.MaPhong = hd.MaPhong
                OUTER APPLY (
                    SELECT SUM(t.SoTien) AS DaTra
                    FROM dbo.ThanhToan t
                    WHERE t.MaHoaDon = hd.MaHoaDon
                ) tt
                WHERE hd.MaHoaDon = @MaHoaDon
                  AND EXISTS (
                      SELECT 1 FROM dbo.NhanVien nv
                      WHERE nv.MaDayTro = p.MaDayTro
                        AND nv.MaNguoiDung = @QuanLy
                        AND nv.ChucVu = N'Quản lý'
                        AND nv.TrangThaiLamViec = N'Đang làm'
                  );";

            command.Parameters.Add(
                "@MaHoaDon", SqlDbType.VarChar, 20)
                .Value = maHoaDon;

            command.Parameters.Add(
                "@QuanLy", SqlDbType.VarChar, 20)
                .Value = maQuanLy;

            await connection.OpenAsync();

            using var reader =
                await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return new ThongTinXacNhanThanhToan
            {
                HoTen = reader["HoTen"],
                TongTien = reader["TongTien"],
                DaTra = reader["DaTra"]
            };
        }

        public static async Task LuuAsync(
            string maHoaDon,
            string maQuanLy,
            string phuongThuc,
            decimal soTien,
            decimal tongTien,
            decimal conNo)
        {
            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = @"
                DECLARE @Tong DECIMAL(18,0);
                DECLARE @DaTra DECIMAL(38,0);
                DECLARE @No DECIMAL(38,0);

                SELECT @Tong = hd.TongTien
                FROM dbo.HoaDon hd WITH (UPDLOCK, HOLDLOCK)
                JOIN dbo.PhongTro p ON p.MaPhong = hd.MaPhong
                WHERE hd.MaHoaDon = @MaHoaDon
                  AND EXISTS (
                      SELECT 1 FROM dbo.NhanVien nv
                      WHERE nv.MaDayTro = p.MaDayTro
                        AND nv.MaNguoiDung = @QuanLy
                        AND nv.ChucVu = N'Quản lý'
                        AND nv.TrangThaiLamViec = N'Đang làm'
                  );

                IF @Tong IS NULL
                    THROW 50001,
                        N'Hóa đơn không tồn tại hoặc bạn không còn quyền quản lý.', 1;

                SELECT @DaTra = COALESCE(SUM(t.SoTien), 0)
                FROM dbo.ThanhToan t WITH (UPDLOCK, HOLDLOCK)
                WHERE t.MaHoaDon = @MaHoaDon;

                SET @No = @Tong - @DaTra;

                IF @Tong <> @TongCu OR @No <> @NoCu
                    THROW 50002,
                        N'Hóa đơn hoặc số nợ đã thay đổi. Quay lại và mở form mới.', 1;

                IF @SoTien <= 0 OR @SoTien > @No
                    THROW 50003,
                        N'Số tiền thanh toán vượt quá số nợ hiện tại.', 1;

                INSERT INTO dbo.ThanhToan
                    (MaThanhToan, MaHoaDon, SoTien,
                     PhuongThuc, MaNguoiXacNhan, GhiChu)
                VALUES
                    (@MaThanhToan, @MaHoaDon, @SoTien,
                     @PhuongThuc, @QuanLy,
                     N'Ghi nhận từ form xác nhận thanh toán');

                UPDATE dbo.HoaDon
                SET TrangThai = CASE
                        WHEN @DaTra + @SoTien >= @Tong
                            THEN N'Đã thanh toán'
                        ELSE N'Còn nợ'
                    END,
                    PhuongThucThanhToan = @PhuongThuc,
                    NgayThanhToan = GETDATE(),
                    MaGiaoDich = NULL
                WHERE MaHoaDon = @MaHoaDon;";

            command.Parameters.Add(
                "@MaHoaDon", SqlDbType.VarChar, 20)
                .Value = maHoaDon;

            command.Parameters.Add(
                "@MaThanhToan", SqlDbType.VarChar, 20)
                .Value = "TT" + Guid.NewGuid().ToString("N")
                    .Substring(0, 18).ToUpperInvariant();

            command.Parameters.Add(
                "@QuanLy", SqlDbType.VarChar, 20)
                .Value = maQuanLy;

            command.Parameters.Add(
                "@PhuongThuc", SqlDbType.NVarChar, 50)
                .Value = phuongThuc;

            ThemTien(command, "@SoTien", soTien);
            ThemTien(command, "@TongCu", tongTien);
            ThemTien(command, "@NoCu", conNo);

            await command.ExecuteNonQueryAsync();

            transaction.Commit();
        }

        private static void ThemTien(
            SqlCommand command,
            string ten,
            decimal giaTri)
        {
            var parameter =
                command.Parameters.Add(ten, SqlDbType.Decimal);

            parameter.Precision = 18;
            parameter.Scale = 0;
            parameter.Value = giaTri;
        }
    }

    public sealed class ThongTinXacNhanThanhToan
    {
        public object HoTen { get; init; } = DBNull.Value;
        public object TongTien { get; init; } = DBNull.Value;
        public object DaTra { get; init; } = DBNull.Value;
    }
}