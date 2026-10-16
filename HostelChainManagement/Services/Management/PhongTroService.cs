using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public static class PhongTroService
    {
        public static async Task<DataTable> LayDanhSachAsync(
            string maNguoiDung)
        {
            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    p.MaPhong,
                    p.MaDayTro,
                    p.SoPhong,
                    d.TenDayTro,
                    p.LoaiPhong,
                    p.DienTich,
                    p.GiaThue,
                    p.TrangThai
                FROM dbo.PhongTro AS p
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
                ORDER BY d.TenDayTro, p.SoPhong;";

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

        public static async Task<int> XoaAsync(
            string maPhong,
            string maNguoiDung)
        {
            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                DELETE p
                FROM dbo.PhongTro AS p
                WHERE p.MaPhong = @MaPhong
                  AND p.TrangThai <> N'Đang thuê'
                  AND EXISTS (
                      SELECT 1
                      FROM dbo.NhanVien AS nv
                      WHERE nv.MaDayTro = p.MaDayTro
                        AND nv.MaNguoiDung = @MaNguoiDung
                        AND nv.ChucVu = N'Quản lý'
                        AND nv.TrangThaiLamViec = N'Đang làm'
                  );";

            command.Parameters.Add(
                "@MaPhong", SqlDbType.VarChar, 20)
                .Value = maPhong;

            command.Parameters.Add(
                "@MaNguoiDung", SqlDbType.VarChar, 20)
                .Value = maNguoiDung;

            await connection.OpenAsync();

            return await command.ExecuteNonQueryAsync();
        }
    }
}