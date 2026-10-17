using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Services.Owner
{
    public static class ChiTietHopDongChuTroService
    {
        public static async Task<DataTable> LayChiTietAsync(
            string maHopDong)
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException(
                    "Bạn chưa đăng nhập.");

            if (user.VaiTro != "Chủ nhà trọ")
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền xem chi tiết hợp đồng.");
            }

            if (string.IsNullOrWhiteSpace(maHopDong)
                || maHopDong.Length > 20)
            {
                throw new InvalidOperationException(
                    "Mã hợp đồng không hợp lệ.");
            }

            const string sql = @"
                SELECT TOP (1)
                    h.MaHopDong,
                    h.TrangThai,
                    nd.HoTen AS KhachThue,
                    p.SoPhong,
                    d.TenDayTro,
                    h.GiaThue,
                    h.NgayBatDau,
                    h.NgayKetThuc,
                    h.NgayKy,
                    h.DaiDienBenChoThue,
                    h.CCCDBenChoThue,
                    h.TienCoc,
                    h.KyThanhToan,
                    h.DieuKhoan,
                    nd.CCCD AS CCCDKhachThue,
                    nd.SoDienThoai AS SoDienThoaiKhachThue,
                    nd.DiaChi AS DiaChiKhachThue,
                    d.DiaChi AS DiaChiDayTro,
                    d.QuyDinh,
                    chu.HoTen AS TenChuTro,
                    chu.SoDienThoai AS SoDienThoaiChuTro,
                    chu.DiaChi AS DiaChiChuTro
                FROM dbo.HopDongThue AS h
                INNER JOIN dbo.PhongTro AS p
                    ON p.MaPhong = h.MaPhong
                INNER JOIN dbo.DayTro AS d
                    ON d.MaDayTro = p.MaDayTro
                INNER JOIN dbo.KhachThue AS k
                    ON k.MaKhachThue = h.MaKhachThue
                INNER JOIN dbo.NguoiDung AS nd
                    ON nd.MaNguoiDung = k.MaNguoiDung
                INNER JOIN dbo.NguoiDung AS chu
                    ON chu.MaNguoiDung = d.MaChuTro
                WHERE h.MaHopDong = @MaHopDong
                  AND d.MaChuTro = @MaChuTro;";

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;

            command.Parameters.Add(
                "@MaHopDong",
                SqlDbType.VarChar,
                20).Value = maHopDong.Trim();

            command.Parameters.Add(
                "@MaChuTro",
                SqlDbType.VarChar,
                20).Value = user.MaNguoiDung;

            await connection.OpenAsync();

            using var reader =
                await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }
    }
}