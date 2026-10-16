using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public static class ChiTietHopDongService
    {
        public static async Task<DuLieuChiTietHopDong?> LayChiTietAsync(
            string maHopDong,
            string maNguoiQuanLy)
        {
            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            var hopDong = new DataTable();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT
                        h.MaHopDong,
                        n.HoTen AS TenKhachThue,
                        n.CCCD,
                        n.SoDienThoai,
                        d.TenDayTro + N' - ' + p.SoPhong AS TenPhong,
                        p.LoaiPhong,
                        h.DaiDienBenChoThue,
                        h.NgayKy,
                        h.NgayBatDau,
                        h.NgayKetThuc,
                        h.GiaThue,
                        h.TienCoc,
                        h.KyThanhToan,
                        h.DieuKhoan,
                        h.TrangThai
                    FROM dbo.HopDongThue h
                    JOIN dbo.KhachThue k
                      ON k.MaKhachThue = h.MaKhachThue
                    JOIN dbo.NguoiDung n
                      ON n.MaNguoiDung = k.MaNguoiDung
                    JOIN dbo.PhongTro p
                      ON p.MaPhong = h.MaPhong
                    JOIN dbo.DayTro d
                      ON d.MaDayTro = p.MaDayTro
                    WHERE h.MaHopDong = @MaHopDong
                      AND EXISTS (
                          SELECT 1
                          FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @NguoiQuanLy
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      );";

                command.Parameters.Add(
                    "@MaHopDong", SqlDbType.VarChar, 20)
                    .Value = maHopDong;

                command.Parameters.Add(
                    "@NguoiQuanLy", SqlDbType.VarChar, 20)
                    .Value = maNguoiQuanLy;

                using var reader = await command.ExecuteReaderAsync();
                hopDong.Load(reader);
            }

            if (hopDong.Rows.Count == 0)
                return null;

            var thanhVien = new DataTable();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT
                        tv.HoTen,
                        tv.CCCD,
                        tv.SoDienThoai,
                        tv.QuanHe,
                        tv.TrangThai
                    FROM dbo.ThanhVienO tv
                    JOIN dbo.HopDongThue h
                      ON h.MaHopDong = tv.MaHopDong
                    JOIN dbo.PhongTro p
                      ON p.MaPhong = h.MaPhong
                    WHERE tv.MaHopDong = @MaHopDong
                      AND EXISTS (
                          SELECT 1
                          FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @NguoiQuanLy
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      )
                    ORDER BY tv.HoTen, tv.MaThanhVien;";

                command.Parameters.Add(
                    "@MaHopDong", SqlDbType.VarChar, 20)
                    .Value = maHopDong;

                command.Parameters.Add(
                    "@NguoiQuanLy", SqlDbType.VarChar, 20)
                    .Value = maNguoiQuanLy;

                using var reader = await command.ExecuteReaderAsync();
                thanhVien.Load(reader);
            }

            return new DuLieuChiTietHopDong
            {
                HopDong = hopDong,
                ThanhVien = thanhVien
            };
        }
    }

    public sealed class DuLieuChiTietHopDong
    {
        public DataTable HopDong { get; init; } = new();
        public DataTable ThanhVien { get; init; } = new();
    }
}