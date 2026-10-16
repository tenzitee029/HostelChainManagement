using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public static class QuanLyHoaDonService
    {
        private const string DieuKienQuyen = @"
            EXISTS (
                SELECT 1
                FROM dbo.NhanVien nv
                WHERE nv.MaDayTro = p.MaDayTro
                  AND nv.MaNguoiDung = @QuanLy
                  AND nv.ChucVu = N'Quản lý'
                  AND nv.TrangThaiLamViec = N'Đang làm'
            )";

        public static async Task<DataTable> LayDanhSachAsync(
            string maQuanLy)
        {
            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    hd.MaHoaDon,
                    hd.MaHopDong,

                    CAST(hd.Thang AS VARCHAR(2)) + '/'
                        + CAST(hd.Nam AS VARCHAR(4)) AS KyHoaDon,

                    d.TenDayTro + N' - ' + p.SoPhong AS TenPhong,
                    n.HoTen AS TenKhachThue,

                    hd.TongTien,
                    COALESCE(tt.DaTra, 0) AS DaTra,

                    CASE
                        WHEN hd.TongTien > COALESCE(tt.DaTra, 0)
                            THEN hd.TongTien - COALESCE(tt.DaTra, 0)
                        ELSE 0
                    END AS ConNo,

                    CASE
                        WHEN COALESCE(tt.DaTra, 0) >= hd.TongTien
                            THEN N'Đã thanh toán'
                        WHEN COALESCE(tt.DaTra, 0) > 0
                            THEN N'Còn nợ'
                        ELSE N'Chưa thanh toán'
                    END AS TrangThai

                FROM dbo.HoaDon hd

                JOIN dbo.HopDongThue h
                    ON h.MaHopDong = hd.MaHopDong
                   AND h.MaPhong = hd.MaPhong

                JOIN dbo.KhachThue k
                    ON k.MaKhachThue = h.MaKhachThue

                JOIN dbo.NguoiDung n
                    ON n.MaNguoiDung = k.MaNguoiDung

                JOIN dbo.PhongTro p
                    ON p.MaPhong = hd.MaPhong

                JOIN dbo.DayTro d
                    ON d.MaDayTro = p.MaDayTro

                OUTER APPLY
                (
                    SELECT SUM(t.SoTien) AS DaTra
                    FROM dbo.ThanhToan t
                    WHERE t.MaHoaDon = hd.MaHoaDon
                ) tt

                WHERE " + DieuKienQuyen + @"

                ORDER BY
                    hd.Nam DESC,
                    hd.Thang DESC,
                    hd.MaHoaDon;";

            command.Parameters.Add(
                "@QuanLy", SqlDbType.VarChar, 20)
                .Value = maQuanLy;

            await connection.OpenAsync();

            using var reader =
                await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }
    }
}