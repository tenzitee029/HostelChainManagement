using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Models;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Services.Tenant
{
    public static class HoaDonChiPhiService
    {
        private static LoggedInUser RequireTenant()
        {
            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Khách thuê")
            {
                throw new InvalidOperationException(
                    "Chức năng chỉ dành cho Khách thuê đã đăng nhập.");
            }

            return user;
        }

        // Lấy cả lịch sử hóa đơn thuộc tài khoản khách thuê.
        // Không giới hạn ở hợp đồng đang hiệu lực.
        private const string HeaderSql = @"
            SELECT
                hd.MaHoaDon,
                hd.MaHopDong,
                hd.Thang,
                hd.Nam,
                p.SoPhong,
                d.TenDayTro,
                n.HoTen AS TenKhachThue,
                hd.NgayLap,
                hd.HanThanhToan,
                hd.TongTien,
                hd.TrangThai,
                hd.PhuongThucThanhToan,
                hd.NgayThanhToan,

                ISNULL(tt.DaThanhToan, 0) AS DaThanhToan,

                CASE
                    WHEN hd.TongTien > ISNULL(tt.DaThanhToan, 0)
                    THEN hd.TongTien - ISNULL(tt.DaThanhToan, 0)
                    ELSE 0
                END AS ConPhaiTra

            FROM dbo.HoaDon AS hd

            INNER JOIN dbo.HopDongThue AS h
                ON h.MaHopDong = hd.MaHopDong
               AND h.MaPhong = hd.MaPhong

            INNER JOIN dbo.KhachThue AS k
                ON k.MaKhachThue = h.MaKhachThue

            INNER JOIN dbo.NguoiDung AS n
                ON n.MaNguoiDung = k.MaNguoiDung

            INNER JOIN dbo.PhongTro AS p
                ON p.MaPhong = hd.MaPhong

            INNER JOIN dbo.DayTro AS d
                ON d.MaDayTro = p.MaDayTro

            OUTER APPLY
            (
                SELECT SUM(t.SoTien) AS DaThanhToan
                FROM dbo.ThanhToan AS t
                WHERE t.MaHoaDon = hd.MaHoaDon
            ) AS tt

            WHERE k.MaNguoiDung = @MaNguoiDung";

        public static async Task<DataTable> GetDanhSachAsync()
        {
            var user = RequireTenant();

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = HeaderSql + @"
                ORDER BY
                    hd.Nam DESC,
                    hd.Thang DESC,
                    hd.MaHoaDon;";

            command.Parameters.Add(
                "@MaNguoiDung",
                SqlDbType.VarChar,
                20).Value = user.MaNguoiDung;

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }
    }
}