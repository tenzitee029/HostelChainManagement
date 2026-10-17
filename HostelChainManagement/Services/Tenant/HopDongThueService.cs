using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Services.Tenant
{
    public static class HopDongThueService
    {
        public static async Task<DataTable> GetHopDongCuaToiAsync()
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException("Bạn chưa đăng nhập.");

            if (user.VaiTro != "Khách thuê")
                throw new InvalidOperationException(
                    "Chức năng chỉ dành cho Khách thuê.");

            const string sql = @"
                SELECT
                    h.MaHopDong,
                    h.TrangThai,
                    n.HoTen AS TenKhachThue,
                    n.CCCD,
                    n.SoDienThoai,
                    p.SoPhong,
                    d.TenDayTro,
                    h.NgayKy,
                    h.DaiDienBenChoThue,
                    h.DieuKhoan,
                    h.NgayBatDau,
                    h.NgayKetThuc,
                    h.TienCoc,
                    h.GiaThue,
                    h.KyThanhToan

                FROM dbo.HopDongThue AS h
                INNER JOIN dbo.KhachThue AS k
                    ON k.MaKhachThue = h.MaKhachThue
                INNER JOIN dbo.NguoiDung AS n
                    ON n.MaNguoiDung = k.MaNguoiDung
                INNER JOIN dbo.PhongTro AS p
                    ON p.MaPhong = h.MaPhong
                INNER JOIN dbo.DayTro AS d
                    ON d.MaDayTro = p.MaDayTro
                WHERE k.MaNguoiDung = @MaNguoiDung
                ORDER BY
                    CASE WHEN h.TrangThai = N'Hiệu lực'
                         THEN 0 ELSE 1 END,
                    h.NgayBatDau DESC,
                    h.MaHopDong;";

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;

            command.Parameters.Add(
                "@MaNguoiDung", SqlDbType.VarChar, 20)
                .Value = user.MaNguoiDung;

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }
    }
}