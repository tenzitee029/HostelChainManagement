using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Models;

namespace QuanLyChuoiNhaTro.Services.Tenant
{
    public sealed class ChiTietHoaDonKhachThue
    {
        public DataRowView ThongTin { get; init; } = null!;

        public DataTable KhoanThu { get; init; } = new();
    }

    public static class HoaDonKhachThueService
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

        public static async Task<ChiTietHoaDonKhachThue>
            GetChiTietAsync(string maHoaDon)
        {
            var user = RequireTenant();

            if (string.IsNullOrWhiteSpace(maHoaDon)
                || maHoaDon.Length > 20)
            {
                throw new InvalidOperationException(
                    "Mã hóa đơn không hợp lệ.");
            }

            using var connection = Database.CreateConnection();

            await connection.OpenAsync();

            var header = new DataTable();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = HeaderSql + @"
                    AND hd.MaHoaDon = @MaHoaDon;";

                command.Parameters.Add(
                    "@MaNguoiDung",
                    SqlDbType.VarChar,
                    20).Value = user.MaNguoiDung;

                command.Parameters.Add(
                    "@MaHoaDon",
                    SqlDbType.VarChar,
                    20).Value = maHoaDon;

                using var reader = await command.ExecuteReaderAsync();

                header.Load(reader);
            }

            if (header.Rows.Count != 1)
            {
                throw new InvalidOperationException(
                    "Không tìm thấy hóa đơn thuộc tài khoản của bạn.");
            }

            var items = new DataTable();

            using (var command = connection.CreateCommand())
            {
                // Kiểm tra quyền ở cả truy vấn chi tiết.
                command.CommandText = @"
                    SELECT
                        ct.LoaiKhoanThu,
                        ct.ChiSoCu,
                        ct.ChiSoMoi,
                        ct.SoLuong,
                        ct.DonGia,
                        ct.ThanhTien,
                        ct.GhiChu

                    FROM dbo.ChiTietHoaDon AS ct

                    INNER JOIN dbo.HoaDon AS hd
                        ON hd.MaHoaDon = ct.MaHoaDon

                    INNER JOIN dbo.HopDongThue AS h
                        ON h.MaHopDong = hd.MaHopDong
                       AND h.MaPhong = hd.MaPhong

                    INNER JOIN dbo.KhachThue AS k
                        ON k.MaKhachThue = h.MaKhachThue

                    WHERE ct.MaHoaDon = @MaHoaDon
                      AND k.MaNguoiDung = @MaNguoiDung

                    ORDER BY
                        CASE ct.LoaiKhoanThu
                            WHEN N'Tiền phòng' THEN 0
                            WHEN N'Điện' THEN 1
                            WHEN N'Nước' THEN 2
                            WHEN N'Dịch vụ' THEN 3
                            ELSE 4
                        END,
                        ct.MaChiTietHoaDon;";

                command.Parameters.Add(
                    "@MaNguoiDung",
                    SqlDbType.VarChar,
                    20).Value = user.MaNguoiDung;

                command.Parameters.Add(
                    "@MaHoaDon",
                    SqlDbType.VarChar,
                    20).Value = maHoaDon;

                using var reader = await command.ExecuteReaderAsync();

                items.Load(reader);
            }

            return new ChiTietHoaDonKhachThue
            {
                ThongTin = header.DefaultView[0],
                KhoanThu = items
            };
        }
    }
}