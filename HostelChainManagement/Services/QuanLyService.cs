using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services
{
    public sealed class TongQuanQuanLy
    {
        public int TongPhong { get; init; }
        public int DangThue { get; init; }
        public int PhongTrong { get; init; }
        public int DangSuaChua { get; init; }
        public int ChuaThanhToan { get; init; }
        public int YeuCauSuaChua { get; init; }
        public int HopDongHieuLuc { get; init; }
        public int TongHoaDon { get; init; }
    }

    public static class QuanLyService
    {
        public static async Task<TongQuanQuanLy> GetTongQuanAsync()
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException(
                    "Bạn chưa đăng nhập.");

            if (user.VaiTro != "Nhân viên quản lý")
            {
                throw new InvalidOperationException(
                    "Chức năng chỉ dành cho Nhân viên quản lý.");
            }

            const string sql = @"
                DECLARE @Day TABLE
                (
                    MaDayTro VARCHAR(20) PRIMARY KEY
                );

                INSERT INTO @Day (MaDayTro)
                SELECT DISTINCT nv.MaDayTro
                FROM dbo.NhanVien AS nv
                WHERE nv.MaNguoiDung = @MaNguoiDung
                  AND nv.ChucVu = N'Quản lý'
                  AND nv.TrangThaiLamViec = N'Đang làm'
                  AND nv.MaDayTro IS NOT NULL;

                SELECT
                    (SELECT COUNT(*) FROM @Day) AS SoDay,

                    (SELECT COUNT(*)
                     FROM dbo.PhongTro AS p
                     WHERE p.MaDayTro IN
                         (SELECT MaDayTro FROM @Day)) AS TongPhong,

                    (SELECT COUNT(*)
                     FROM dbo.PhongTro AS p
                     WHERE p.MaDayTro IN
                         (SELECT MaDayTro FROM @Day)
                       AND p.TrangThai = N'Đang thuê') AS DangThue,

                    (SELECT COUNT(*)
                     FROM dbo.PhongTro AS p
                     WHERE p.MaDayTro IN
                         (SELECT MaDayTro FROM @Day)
                       AND p.TrangThai = N'Trống') AS PhongTrong,

                    (SELECT COUNT(*)
                     FROM dbo.PhongTro AS p
                     WHERE p.MaDayTro IN
                         (SELECT MaDayTro FROM @Day)
                       AND p.TrangThai = N'Đang sửa chữa')
                        AS DangSuaChua,

                    (SELECT COUNT(*)
                     FROM dbo.HoaDon AS h
                     INNER JOIN dbo.PhongTro AS p
                         ON p.MaPhong = h.MaPhong
                     WHERE p.MaDayTro IN
                         (SELECT MaDayTro FROM @Day)
                       AND h.TrangThai IN
                         (N'Chưa thanh toán', N'Còn nợ'))
                        AS ChuaThanhToan,

                    (SELECT COUNT(*)
                     FROM dbo.PhieuSuCo AS s
                     INNER JOIN dbo.PhongTro AS p
                         ON p.MaPhong = s.MaPhong
                     WHERE p.MaDayTro IN
                         (SELECT MaDayTro FROM @Day)
                       AND s.TrangThai NOT IN
                         (N'Đã hoàn thành', N'Từ chối'))
                        AS YeuCauSuaChua,

                    (SELECT COUNT(*)
                     FROM dbo.HopDongThue AS h
                     INNER JOIN dbo.PhongTro AS p
                         ON p.MaPhong = h.MaPhong
                     WHERE p.MaDayTro IN
                         (SELECT MaDayTro FROM @Day)
                       AND h.TrangThai = N'Hiệu lực'
                       AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                       AND h.NgayKetThuc >= CAST(GETDATE() AS DATE))
                        AS HopDongHieuLuc,

                    (SELECT COUNT(*)
                     FROM dbo.HoaDon AS h
                     INNER JOIN dbo.PhongTro AS p
                         ON p.MaPhong = h.MaPhong
                     WHERE p.MaDayTro IN
                         (SELECT MaDayTro FROM @Day))
                        AS TongHoaDon;";

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;
            command.Parameters.Add(
                "@MaNguoiDung", SqlDbType.VarChar, 20)
                .Value = user.MaNguoiDung;

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                throw new InvalidOperationException(
                    "Không tải được dữ liệu tổng quan.");

            if (reader.GetInt32(reader.GetOrdinal("SoDay")) == 0)
            {
                throw new InvalidOperationException(
                    "Tài khoản chưa có phân công quản lý dãy trọ " +
                    "đang hoạt động trong hồ sơ nhân viên.");
            }

            return new TongQuanQuanLy
            {
                TongPhong = reader.GetInt32(
                    reader.GetOrdinal("TongPhong")),

                DangThue = reader.GetInt32(
                    reader.GetOrdinal("DangThue")),

                PhongTrong = reader.GetInt32(
                    reader.GetOrdinal("PhongTrong")),

                DangSuaChua = reader.GetInt32(
                    reader.GetOrdinal("DangSuaChua")),

                ChuaThanhToan = reader.GetInt32(
                    reader.GetOrdinal("ChuaThanhToan")),

                YeuCauSuaChua = reader.GetInt32(
                    reader.GetOrdinal("YeuCauSuaChua")),

                HopDongHieuLuc = reader.GetInt32(
                    reader.GetOrdinal("HopDongHieuLuc")),

                TongHoaDon = reader.GetInt32(
                    reader.GetOrdinal("TongHoaDon"))
            };
        }
    }
}