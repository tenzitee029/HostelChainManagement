using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public sealed class ChiTietHoaDonQuanLy
    {
        public DataRowView ThongTin { get; init; } = null!;

        public DataTable KhoanThu { get; init; } = new();
    }

    public static class ChiTietHoaDonQuanLyService
    {
        // Các truy vấn phải có alias p là PhongTro.
        private const string Scope = @"
            EXISTS
            (
                SELECT 1
                FROM dbo.NhanVien nv
                WHERE nv.MaDayTro = p.MaDayTro
                  AND nv.MaNguoiDung = @QuanLy
                  AND nv.ChucVu = N'Quản lý'
                  AND nv.TrangThaiLamViec = N'Đang làm'
            )";

        public static async Task<ChiTietHoaDonQuanLy> GetAsync(
            string maHoaDon)
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException(
                    "Bạn chưa đăng nhập.");

            if (user.VaiTro != "Nhân viên quản lý")
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền xem hóa đơn quản lý.");
            }

            if (string.IsNullOrWhiteSpace(maHoaDon)
                || maHoaDon.Length > 20)
            {
                throw new InvalidOperationException(
                    "Mã hóa đơn không hợp lệ.");
            }

            using var connection = Database.CreateConnection();

            await connection.OpenAsync();

            var header = new DataTable();
            var items = new DataTable();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
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

                        CASE
                            WHEN ISNULL(tt.DaTra, 0) >= hd.TongTien
                                THEN N'Đã thanh toán'
                            WHEN ISNULL(tt.DaTra, 0) > 0
                                THEN N'Còn nợ'
                            ELSE N'Chưa thanh toán'
                        END AS TrangThai,

                        ISNULL(tt.DaTra, 0) AS DaThanhToan,

                        CASE
                            WHEN hd.TongTien > ISNULL(tt.DaTra, 0)
                                THEN hd.TongTien - ISNULL(tt.DaTra, 0)
                            ELSE 0
                        END AS ConPhaiTra

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

                    WHERE hd.MaHoaDon = @MaHoaDon
                      AND " + Scope + ";";

                AddParameters(
                    command,
                    maHoaDon,
                    user.MaNguoiDung);

                using var reader =
                    await command.ExecuteReaderAsync();

                header.Load(reader);
            }

            if (header.Rows.Count != 1)
            {
                throw new InvalidOperationException(
                    "Hóa đơn không tồn tại "
                    + "hoặc bạn không có quyền quản lý.");
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT
                        ct.LoaiKhoanThu,
                        ct.ChiSoCu,
                        ct.ChiSoMoi,
                        ct.SoLuong,
                        ct.DonGia,
                        ct.ThanhTien,
                        ct.GhiChu

                    FROM dbo.ChiTietHoaDon ct

                    JOIN dbo.HoaDon hd
                      ON hd.MaHoaDon = ct.MaHoaDon

                    JOIN dbo.PhongTro p
                      ON p.MaPhong = hd.MaPhong

                    WHERE hd.MaHoaDon = @MaHoaDon
                      AND " + Scope + @"

                    ORDER BY
                        CASE ct.LoaiKhoanThu
                            WHEN N'Tiền phòng' THEN 0
                            WHEN N'Điện' THEN 1
                            WHEN N'Nước' THEN 2
                            WHEN N'Dịch vụ' THEN 3
                            ELSE 4
                        END,
                        ct.MaChiTietHoaDon;";

                AddParameters(
                    command,
                    maHoaDon,
                    user.MaNguoiDung);

                using var reader =
                    await command.ExecuteReaderAsync();

                items.Load(reader);
            }

            return new ChiTietHoaDonQuanLy
            {
                ThongTin = header.DefaultView[0],
                KhoanThu = items
            };
        }

        private static void AddParameters(
            SqlCommand command,
            string maHoaDon,
            string quanLy)
        {
            command.Parameters.Add(
                "@MaHoaDon",
                SqlDbType.VarChar,
                20).Value = maHoaDon;

            command.Parameters.Add(
                "@QuanLy",
                SqlDbType.VarChar,
                20).Value = quanLy;
        }
    }
}