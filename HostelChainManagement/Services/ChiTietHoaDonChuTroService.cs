
using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services
{
    public static class ChiTietHoaDonChuTroService
    {
        public sealed record KetQuaChiTiet(
            DataTable ThongTin,
            DataTable CacKhoanThu);

        public static async Task<KetQuaChiTiet> LayChiTietAsync(
            string maHoaDon)
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException(
                    "Bạn chưa đăng nhập.");

            if (user.VaiTro != "Chủ nhà trọ")
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền xem chi tiết hóa đơn.");
            }

            if (string.IsNullOrWhiteSpace(maHoaDon)
                || maHoaDon.Length > 20)
            {
                throw new InvalidOperationException(
                    "Mã hóa đơn không hợp lệ.");
            }

            const string sqlThongTin = @"
                SELECT TOP (1)
                    hd.MaHoaDon,
                    hd.MaHopDong,
                    hd.Thang,
                    hd.Nam,
                    hd.NgayLap,
                    hd.HanThanhToan,
                    hd.TongTien,
                    hd.TrangThai,
                    hd.PhuongThucThanhToan,
                    hd.NgayThanhToan,
                    hd.MaGiaoDich,
                    nd.HoTen AS HoTenKhach,
                    p.SoPhong,
                    d.TenDayTro
                FROM dbo.HoaDon AS hd
                INNER JOIN dbo.HopDongThue AS h
                    ON h.MaHopDong = hd.MaHopDong
                   AND h.MaPhong = hd.MaPhong
                INNER JOIN dbo.PhongTro AS p
                    ON p.MaPhong = hd.MaPhong
                INNER JOIN dbo.DayTro AS d
                    ON d.MaDayTro = p.MaDayTro
                INNER JOIN dbo.KhachThue AS k
                    ON k.MaKhachThue = h.MaKhachThue
                INNER JOIN dbo.NguoiDung AS nd
                    ON nd.MaNguoiDung = k.MaNguoiDung
                WHERE hd.MaHoaDon = @MaHoaDon
                  AND d.MaChuTro = @MaChuTro;";

            const string sqlKhoanThu = @"
                SELECT
                    ct.MaChiTietHoaDon,
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
                INNER JOIN dbo.PhongTro AS p
                    ON p.MaPhong = hd.MaPhong
                INNER JOIN dbo.DayTro AS d
                    ON d.MaDayTro = p.MaDayTro
                WHERE ct.MaHoaDon = @MaHoaDon
                  AND d.MaChuTro = @MaChuTro
                ORDER BY
                    CASE ct.LoaiKhoanThu
                        WHEN N'Tiền phòng' THEN 1
                        WHEN N'Điện' THEN 2
                        WHEN N'Nước' THEN 3
                        WHEN N'Dịch vụ' THEN 4
                        WHEN N'Phụ phí' THEN 5
                        ELSE 6
                    END,
                    ct.MaChiTietHoaDon;";

            using var connection = Database.CreateConnection();

            await connection.OpenAsync();

            var thongTin = new DataTable();
            var cacKhoanThu = new DataTable();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sqlThongTin;

                command.Parameters.Add(
                    "@MaHoaDon",
                    SqlDbType.VarChar,
                    20).Value = maHoaDon.Trim();

                command.Parameters.Add(
                    "@MaChuTro",
                    SqlDbType.VarChar,
                    20).Value = user.MaNguoiDung;

                using var reader =
                    await command.ExecuteReaderAsync();

                thongTin.Load(reader);
            }

            if (thongTin.Rows.Count == 0)
            {
                return new KetQuaChiTiet(
                    thongTin,
                    cacKhoanThu);
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sqlKhoanThu;

                command.Parameters.Add(
                    "@MaHoaDon",
                    SqlDbType.VarChar,
                    20).Value = maHoaDon.Trim();

                command.Parameters.Add(
                    "@MaChuTro",
                    SqlDbType.VarChar,
                    20).Value = user.MaNguoiDung;

                using var reader =
                    await command.ExecuteReaderAsync();

                cacKhoanThu.Load(reader);
            }

            return new KetQuaChiTiet(
                thongTin,
                cacKhoanThu);
        }
    }
}
