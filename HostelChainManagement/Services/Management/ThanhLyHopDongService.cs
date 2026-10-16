using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public sealed class ThanhLyHopDongService : IDisposable
    {
        private readonly SqlConnection _connection =
            Database.CreateConnection();

        public async Task MoKetNoiAsync()
        {
            await _connection.OpenAsync();
        }

        public async Task<ThongTinThanhLy?> LayThongTinAsync(
            string maHopDong,
            string maQuanLy)
        {
            using var command = _connection.CreateCommand();

            command.CommandText = @"
                SELECT h.MaPhong, h.MaKhachThue,
                       h.TienCoc, h.TrangThai,
                       h.NgayBatDau, n.HoTen,
                       d.TenDayTro + N' - ' + p.SoPhong AS TenPhong,
                       CAST(GETDATE() AS DATE) AS HomNay
                FROM dbo.HopDongThue h
                JOIN dbo.KhachThue k
                  ON k.MaKhachThue = h.MaKhachThue
                JOIN dbo.NguoiDung n
                  ON n.MaNguoiDung = k.MaNguoiDung
                JOIN dbo.PhongTro p ON p.MaPhong = h.MaPhong
                JOIN dbo.DayTro d ON d.MaDayTro = p.MaDayTro
                WHERE h.MaHopDong = @MaHopDong
                  AND EXISTS (
                      SELECT 1 FROM dbo.NhanVien nv
                      WHERE nv.MaDayTro = p.MaDayTro
                        AND nv.MaNguoiDung = @QuanLy
                        AND nv.ChucVu = N'Quản lý'
                        AND nv.TrangThaiLamViec = N'Đang làm'
                  );";

            command.Parameters.Add(
                "@MaHopDong", SqlDbType.VarChar, 20)
                .Value = maHopDong;

            command.Parameters.Add(
                "@QuanLy", SqlDbType.VarChar, 20)
                .Value = maQuanLy;

            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return new ThongTinThanhLy
            {
                MaPhong = reader["MaPhong"],
                MaKhachThue = reader["MaKhachThue"],
                TienCoc = reader["TienCoc"],
                TrangThai = reader["TrangThai"],
                NgayBatDau = reader["NgayBatDau"],
                HoTen = reader["HoTen"],
                TenPhong = reader["TenPhong"],
                HomNay = reader["HomNay"]
            };
        }

        public async Task<decimal> LayTienNoAsync(string maHopDong)
        {
            using var command = _connection.CreateCommand();

            command.CommandText = @"
                SELECT COALESCE(SUM(
                    CASE WHEN hd.TongTien > COALESCE(tt.DaTra, 0)
                         THEN hd.TongTien - COALESCE(tt.DaTra, 0)
                         ELSE 0 END
                ), 0)
                FROM dbo.HoaDon hd
                OUTER APPLY (
                    SELECT SUM(t.SoTien) AS DaTra
                    FROM dbo.ThanhToan t
                    WHERE t.MaHoaDon = hd.MaHoaDon
                ) tt
                WHERE hd.MaHopDong = @MaHopDong;";

            command.Parameters.Add(
                "@MaHopDong", SqlDbType.VarChar, 20)
                .Value = maHopDong;

            return Convert.ToDecimal(
                await command.ExecuteScalarAsync());
        }

        public async Task<DataTable> LayThietBiAsync(string maPhong)
        {
            using var command = _connection.CreateCommand();

            command.CommandText = @"
                SELECT t.TenThietBi, p.SoLuong,
                       p.TinhTrang, p.GhiChu
                FROM dbo.PhongThietBi p
                JOIN dbo.TrangThietBi t
                  ON t.MaThietBi = p.MaThietBi
                WHERE p.MaPhong = @MaPhong
                ORDER BY t.TenThietBi;";

            command.Parameters.Add(
                "@MaPhong", SqlDbType.VarChar, 20)
                .Value = maPhong;

            using var reader = await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }

        public async Task LuuAsync(
            string maHopDong,
            string maPhong,
            string maKhach,
            string maQuanLy,
            decimal tienCoc,
            decimal khauTru,
            string ghiChu)
        {
            using var transaction =
                _connection.BeginTransaction(IsolationLevel.Serializable);

            using var command = _connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = @"
                DECLARE @Khoa VARCHAR(20);
                DECLARE @Coc DECIMAL(18,0);
                DECLARE @No DECIMAL(38,0);
                DECLARE @HomNay DATE = CAST(GETDATE() AS DATE);

                SELECT @Khoa = MaPhong
                FROM dbo.PhongTro WITH (UPDLOCK, HOLDLOCK)
                WHERE MaPhong = @MaPhong;

                SET @Khoa = NULL;

                SELECT @Khoa = MaKhachThue
                FROM dbo.KhachThue WITH (UPDLOCK, HOLDLOCK)
                WHERE MaKhachThue = @MaKhach;

                SELECT @Coc = h.TienCoc
                FROM dbo.HopDongThue h WITH (UPDLOCK, HOLDLOCK)
                WHERE h.MaHopDong = @MaHopDong
                  AND h.MaPhong = @MaPhong
                  AND h.MaKhachThue = @MaKhach
                  AND h.TrangThai IN (N'Hiệu lực', N'Hết hạn')
                  AND h.NgayThanhLy IS NULL
                  AND h.NgayBatDau <= @HomNay;

                IF @Coc IS NULL
                    THROW 50001,
                        N'Hợp đồng đã thay đổi hoặc không đủ điều kiện thanh lý.', 1;

                IF @Coc <> @CocCu
                    THROW 50002,
                        N'Tiền cọc đã thay đổi. Hãy mở lại form.', 1;

                IF NOT EXISTS (
                    SELECT 1 FROM dbo.PhongTro p
                    JOIN dbo.NhanVien nv ON nv.MaDayTro = p.MaDayTro
                    WHERE p.MaPhong = @MaPhong
                      AND nv.MaNguoiDung = @QuanLy
                      AND nv.ChucVu = N'Quản lý'
                      AND nv.TrangThaiLamViec = N'Đang làm'
                )
                    THROW 50003,
                        N'Bạn không còn quyền quản lý hợp đồng.', 1;

                SELECT @No = COALESCE(SUM(
                    CASE WHEN hd.TongTien > COALESCE(tt.DaTra, 0)
                         THEN hd.TongTien - COALESCE(tt.DaTra, 0)
                         ELSE 0 END
                ), 0)
                FROM dbo.HoaDon hd WITH (UPDLOCK, HOLDLOCK)
                OUTER APPLY (
                    SELECT SUM(t.SoTien) AS DaTra
                    FROM dbo.ThanhToan t WITH (HOLDLOCK)
                    WHERE t.MaHoaDon = hd.MaHoaDon
                ) tt
                WHERE hd.MaHopDong = @MaHopDong;

                IF @No > 0
                    THROW 50004,
                        N'Hợp đồng còn nợ hóa đơn. Cần thanh toán trước.', 1;

                IF @KhauTru < 0 OR @KhauTru > @Coc
                    THROW 50005,
                        N'Số tiền khấu trừ không hợp lệ.', 1;

                UPDATE dbo.HopDongThue
                SET TrangThai = N'Đã thanh lý',
                    NgayThanhLy = @HomNay,
                    TienKhauTruCoc = @KhauTru,
                    TienHoanCoc = @Coc - @KhauTru,
                    GhiChuThanhLy = @GhiChu
                WHERE MaHopDong = @MaHopDong;

                UPDATE dbo.ThanhVienO
                SET TrangThai = N'Đã rời đi'
                WHERE MaHopDong = @MaHopDong
                  AND TrangThai = N'Đang ở';

                UPDATE dbo.YeuCauTraPhong
                SET TrangThai = N'Đã hoàn tất',
                    GhiChuXuLy = N'Đã thanh lý hợp đồng'
                WHERE MaHopDong = @MaHopDong
                  AND TrangThai IN (N'Chờ xử lý', N'Đã duyệt');

                -- Không giải phóng phòng có hợp đồng khác đang thuê.
                -- Giữ nguyên phòng đang sửa chữa.
                UPDATE p
                SET p.TrangThai = N'Trống'
                FROM dbo.PhongTro p
                WHERE p.MaPhong = @MaPhong
                  AND p.TrangThai = N'Đang thuê'
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.HopDongThue h
                      WHERE h.MaPhong = p.MaPhong
                        AND h.TrangThai = N'Hiệu lực'
                        AND h.NgayBatDau <= @HomNay
                        AND h.NgayKetThuc >= @HomNay
                  );

                UPDATE dbo.KhachThue
                SET TrangThai = N'Đã rời đi'
                WHERE MaKhachThue = @MaKhach
                  AND TrangThai = N'Đang thuê'
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.HopDongThue h
                      WHERE h.MaKhachThue = @MaKhach
                        AND h.TrangThai = N'Hiệu lực'
                        AND h.NgayBatDau <= @HomNay
                        AND h.NgayKetThuc >= @HomNay
                  );";

            command.Parameters.Add(
                "@MaHopDong", SqlDbType.VarChar, 20)
                .Value = maHopDong;

            command.Parameters.Add(
                "@MaPhong", SqlDbType.VarChar, 20)
                .Value = maPhong;

            command.Parameters.Add(
                "@MaKhach", SqlDbType.VarChar, 20)
                .Value = maKhach;

            command.Parameters.Add(
                "@QuanLy", SqlDbType.VarChar, 20)
                .Value = maQuanLy;

            var coc = command.Parameters.Add(
                "@CocCu", SqlDbType.Decimal);
            coc.Precision = 18;
            coc.Scale = 0;
            coc.Value = tienCoc;

            var tru = command.Parameters.Add(
                "@KhauTru", SqlDbType.Decimal);
            tru.Precision = 18;
            tru.Scale = 0;
            tru.Value = khauTru;

            command.Parameters.Add(
                "@GhiChu", SqlDbType.NVarChar, 500)
                .Value = ghiChu.Length == 0
                    ? (object)DBNull.Value
                    : ghiChu;

            await command.ExecuteNonQueryAsync();
            transaction.Commit();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }

    public sealed class ThongTinThanhLy
    {
        public object MaPhong { get; init; } = DBNull.Value;
        public object MaKhachThue { get; init; } = DBNull.Value;
        public object TienCoc { get; init; } = DBNull.Value;
        public object TrangThai { get; init; } = DBNull.Value;
        public object NgayBatDau { get; init; } = DBNull.Value;
        public object HoTen { get; init; } = DBNull.Value;
        public object TenPhong { get; init; } = DBNull.Value;
        public object HomNay { get; init; } = DBNull.Value;
    }
}