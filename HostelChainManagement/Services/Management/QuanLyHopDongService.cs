using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public static class QuanLyHopDongService
    {
        public static async Task<DataTable> LayDanhSachAsync(
            string maNguoiDung)
        {
            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    h.MaHopDong,
                    h.MaKhachThue,
                    h.MaPhong,
                    n.HoTen AS TenKhachThue,
                    n.CCCD,
                    n.SoDienThoai,
                    p.SoPhong,
                    d.MaDayTro,
                    d.TenDayTro,
                    h.NgayKy,
                    h.NgayBatDau,
                    h.NgayKetThuc,
                    h.TienCoc,
                    h.GiaThue,
                    h.KyThanhToan,
                    h.DaiDienBenChoThue,
                    h.DieuKhoan,
                    h.TrangThai
                FROM dbo.HopDongThue AS h
                INNER JOIN dbo.KhachThue AS k
                    ON k.MaKhachThue = h.MaKhachThue
                INNER JOIN dbo.NguoiDung AS n
                    ON n.MaNguoiDung = k.MaNguoiDung
                INNER JOIN dbo.PhongTro AS p
                    ON p.MaPhong = h.MaPhong
                INNER JOIN dbo.DayTro AS d
                    ON d.MaDayTro = p.MaDayTro
                WHERE EXISTS (
                    SELECT 1
                    FROM dbo.NhanVien AS nv
                    WHERE nv.MaDayTro = p.MaDayTro
                      AND nv.MaNguoiDung = @MaNguoiDung
                      AND nv.ChucVu = N'Quản lý'
                      AND nv.TrangThaiLamViec = N'Đang làm'
                )
                ORDER BY h.NgayBatDau DESC, h.MaHopDong;";

            command.Parameters.Add(
                "@MaNguoiDung", SqlDbType.VarChar, 20)
                .Value = maNguoiDung;

            await connection.OpenAsync();

            using var reader =
                await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }
    }
}