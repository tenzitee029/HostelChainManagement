using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services
{
    public static class TongQuanChuTroService
    {
        public sealed record SoLieuTongQuan(
            int TongDayTro,
            int NhanVienDangLam,
            int HopDongHieuLuc,
            int HoaDonConNo,
            decimal DoanhThuDaThu,
            decimal TongCongNo,
            int PhongChuaThanhToan);

        public static async Task<SoLieuTongQuan> LaySoLieuAsync()
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException(
                    "Bạn chưa đăng nhập.");

            if (user.VaiTro != "Chủ nhà trọ")
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền xem Tổng quan Chủ trọ.");
            }

            const string sql = @"
                WITH CongNo AS
                (
                    SELECT
                        hd.MaHoaDon,
                        hd.MaPhong,
                        hd.TongTien
                            - ISNULL(daTra.TongDaTra, 0)
                            AS SoTienConNo
                    FROM dbo.HoaDon AS hd
                    INNER JOIN dbo.PhongTro AS p
                        ON p.MaPhong = hd.MaPhong
                    INNER JOIN dbo.DayTro AS d
                        ON d.MaDayTro = p.MaDayTro
                    OUTER APPLY
                    (
                        SELECT
                            SUM(tt.SoTien) AS TongDaTra
                        FROM dbo.ThanhToan AS tt
                        WHERE tt.MaHoaDon = hd.MaHoaDon
                    ) AS daTra
                    WHERE d.MaChuTro = @MaChuTro
                )
                SELECT
                    (
                        SELECT COUNT(*)
                        FROM dbo.DayTro AS d
                        WHERE d.MaChuTro = @MaChuTro
                    ) AS TongDayTro,

                    (
                        SELECT COUNT(DISTINCT nv.MaNhanVien)
                        FROM dbo.ChuTroNhanVien AS ct
                        INNER JOIN dbo.NhanVien AS nv
                            ON nv.MaNhanVien = ct.MaNhanVien
                        INNER JOIN dbo.NguoiDung AS nd
                            ON nd.MaNguoiDung = nv.MaNguoiDung
                        WHERE ct.MaChuTro = @MaChuTro
                          AND nv.TrangThaiLamViec = N'Đang làm'
                          AND nd.TrangThai = N'Hoạt động'
                          AND
                          (
                              nv.MaDayTro IS NULL
                              OR EXISTS
                              (
                                  SELECT 1
                                  FROM dbo.DayTro AS d
                                  WHERE d.MaDayTro = nv.MaDayTro
                                    AND d.MaChuTro = @MaChuTro
                              )
                          )
                    ) AS NhanVienDangLam,

                    (
                        SELECT COUNT(*)
                        FROM dbo.HopDongThue AS h
                        INNER JOIN dbo.PhongTro AS p
                            ON p.MaPhong = h.MaPhong
                        INNER JOIN dbo.DayTro AS d
                            ON d.MaDayTro = p.MaDayTro
                        WHERE d.MaChuTro = @MaChuTro
                          AND h.TrangThai = N'Hiệu lực'
                          AND h.NgayBatDau <= @HomNay
                          AND h.NgayKetThuc >= @HomNay
                          AND
                          (
                              h.NgayThanhLy IS NULL
                              OR h.NgayThanhLy > @HomNay
                          )
                    ) AS HopDongHieuLuc,

                    (
                        SELECT COUNT(*)
                        FROM CongNo
                        WHERE SoTienConNo > 0
                    ) AS HoaDonConNo,

                    (
                        SELECT ISNULL(SUM(tt.SoTien), 0)
                        FROM dbo.ThanhToan AS tt
                        INNER JOIN dbo.HoaDon AS hd
                            ON hd.MaHoaDon = tt.MaHoaDon
                        INNER JOIN dbo.PhongTro AS p
                            ON p.MaPhong = hd.MaPhong
                        INNER JOIN dbo.DayTro AS d
                            ON d.MaDayTro = p.MaDayTro
                        WHERE d.MaChuTro = @MaChuTro
                    ) AS DoanhThuDaThu,

                    (
                        SELECT ISNULL(
                            SUM(
                                CASE
                                    WHEN SoTienConNo > 0
                                    THEN SoTienConNo
                                    ELSE 0
                                END
                            ),
                            0
                        )
                        FROM CongNo
                    ) AS TongCongNo,

                    (
                        SELECT COUNT(DISTINCT MaPhong)
                        FROM CongNo
                        WHERE SoTienConNo > 0
                    ) AS PhongChuaThanhToan;";

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;

            command.Parameters.Add(
                "@MaChuTro",
                SqlDbType.VarChar,
                20).Value = user.MaNguoiDung;

            command.Parameters.Add(
                "@HomNay",
                SqlDbType.Date).Value = DateTime.Today;

            await connection.OpenAsync();

            if (!await SoDoChuTroService.CoBangLienKetAsync(connection))
            {
                bool motChuTro = await SoDoChuTroService.LaChuTroDuyNhatAsync(
                    connection, user.MaNguoiDung);
                command.Parameters.Add("@MotChuTro", SqlDbType.Bit)
                    .Value = motChuTro;

                command.CommandText = sql.Replace(
                    @"                    (
                        SELECT COUNT(DISTINCT nv.MaNhanVien)
                        FROM dbo.ChuTroNhanVien AS ct
                        INNER JOIN dbo.NhanVien AS nv
                            ON nv.MaNhanVien = ct.MaNhanVien
                        INNER JOIN dbo.NguoiDung AS nd
                            ON nd.MaNguoiDung = nv.MaNguoiDung
                        WHERE ct.MaChuTro = @MaChuTro
                          AND nv.TrangThaiLamViec = N'Đang làm'
                          AND nd.TrangThai = N'Hoạt động'
                          AND
                          (
                              nv.MaDayTro IS NULL
                              OR EXISTS
                              (
                                  SELECT 1
                                  FROM dbo.DayTro AS d
                                  WHERE d.MaDayTro = nv.MaDayTro
                                    AND d.MaChuTro = @MaChuTro
                              )
                          )
                    ) AS NhanVienDangLam,",
                    @"                    (
                        SELECT COUNT(DISTINCT nv.MaNhanVien)
                        FROM dbo.NhanVien AS nv
                        LEFT JOIN dbo.DayTro AS d
                            ON d.MaDayTro = nv.MaDayTro
                        INNER JOIN dbo.NguoiDung AS nd
                            ON nd.MaNguoiDung = nv.MaNguoiDung
                        WHERE
                          (
                              d.MaChuTro = @MaChuTro
                              OR (@MotChuTro = 1 AND nv.MaDayTro IS NULL)
                          )
                          AND nv.TrangThaiLamViec = N'Đang làm'
                          AND nd.TrangThai = N'Hoạt động'
                    ) AS NhanVienDangLam,");
            }

            using var reader =
                await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException(
                    "Không thể lấy dữ liệu Tổng quan.");
            }

            return new SoLieuTongQuan(
                Convert.ToInt32(reader["TongDayTro"]),
                Convert.ToInt32(reader["NhanVienDangLam"]),
                Convert.ToInt32(reader["HopDongHieuLuc"]),
                Convert.ToInt32(reader["HoaDonConNo"]),
                Convert.ToDecimal(reader["DoanhThuDaThu"]),
                Convert.ToDecimal(reader["TongCongNo"]),
                Convert.ToInt32(reader["PhongChuaThanhToan"]));
        }
    }
}