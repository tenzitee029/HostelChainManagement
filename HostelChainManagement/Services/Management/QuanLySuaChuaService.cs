using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public static class QuanLySuaChuaService
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
                    s.MaSuCo,
                    s.MaPhong,
                    d.TenDayTro + N' - ' + p.SoPhong AS TenPhong,
                    n.HoTen AS TenNguoiGui,
                    s.NgayGui,
                    s.NoiDungSuCo,
                    s.MucDoUuTien,
                    s.TrangThai
                FROM dbo.PhieuSuCo s
                JOIN dbo.PhongTro p
                    ON p.MaPhong = s.MaPhong
                JOIN dbo.DayTro d
                    ON d.MaDayTro = p.MaDayTro
                JOIN dbo.NguoiDung n
                    ON n.MaNguoiDung = s.MaNguoiGui
                WHERE " + DieuKienQuyen + @"
                ORDER BY
                    CASE
                        WHEN s.MucDoUuTien = N'Khẩn cấp'
                            THEN 0
                        ELSE 1
                    END,
                    s.NgayGui DESC,
                    s.MaSuCo;";

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