using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Services.Owner
{
    public static class BaoCaoChuTroService
    {
        public static async Task<DataTable> LayBaoCaoAsync(
            string? maDayTro,
            int kieuKy,
            int luaChonThoiGian)
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException(
                    "Bạn chưa đăng nhập.");

            if (user.VaiTro != "Chủ nhà trọ")
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền xem báo cáo.");
            }

            if (!string.IsNullOrWhiteSpace(maDayTro)
                && maDayTro.Length > 20)
            {
                throw new InvalidOperationException(
                    "Mã dãy trọ không hợp lệ.");
            }

            if (kieuKy < 0 || kieuKy > 1
                || luaChonThoiGian < 0
                || luaChonThoiGian > 3)
            {
                throw new InvalidOperationException(
                    "Bộ lọc báo cáo không hợp lệ.");
            }

            bool theoNam = kieuKy == 1;

            int soKy = luaChonThoiGian switch
            {
                2 => 3,
                3 => 12,
                _ => 1
            };

            int doLech = luaChonThoiGian == 1 ? 1 : 0;

            DateTime homNay = DateTime.Today;

            DateTime kyCuoi = theoNam
                ? new DateTime(homNay.Year, 1, 1)
                    .AddYears(-doLech)
                : new DateTime(
                    homNay.Year,
                    homNay.Month,
                    1).AddMonths(-doLech);

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            var giaTriKy = new List<string>();

            for (int i = soKy - 1; i >= 0; i--)
            {
                int viTri = soKy - 1 - i;

                DateTime tuNgay = theoNam
                    ? kyCuoi.AddYears(-i)
                    : kyCuoi.AddMonths(-i);

                DateTime denNgay = theoNam
                    ? tuNgay.AddYears(1)
                    : tuNgay.AddMonths(1);

                string tenKy = theoNam
                    ? tuNgay.ToString("yyyy")
                    : tuNgay.ToString("MM/yyyy");

                giaTriKy.Add(
                    $"(@TenKy{viTri}, @TuNgay{viTri}, @DenNgay{viTri})");

                command.Parameters.Add(
                    $"@TenKy{viTri}",
                    SqlDbType.NVarChar,
                    20).Value = tenKy;

                command.Parameters.Add(
                    $"@TuNgay{viTri}",
                    SqlDbType.Date).Value = tuNgay;

                command.Parameters.Add(
                    $"@DenNgay{viTri}",
                    SqlDbType.Date).Value = denNgay;
            }

            string bangKy = string.Join(
                ",",
                giaTriKy);

            command.CommandText = @"
                SELECT
                    ky.TenKy,
                    ky.TuNgay,
                    ky.DenNgay,
                    ISNULL(doanhThu.DoanhThu, 0)
                        AS DoanhThu,
                    ISNULL(doanhThu.SoGiaoDich, 0)
                        AS SoGiaoDich,
                    ISNULL(congNo.CongNo, 0)
                        AS CongNo,
                    ISNULL(congNo.SoHoaDonConNo, 0)
                        AS SoHoaDonConNo,
                    ISNULL(lapDay.TongPhong, 0)
                        AS TongPhong,
                    ISNULL(lapDay.PhongDangThue, 0)
                        AS PhongDangThue,
                    CAST(
                        CASE
                            WHEN ISNULL(lapDay.TongPhong, 0) = 0
                                THEN 0
                            ELSE
                                ISNULL(lapDay.PhongDangThue, 0)
                                * 100.0
                                / lapDay.TongPhong
                        END
                        AS DECIMAL(6,2)
                    ) AS TyLeLapDay
                FROM
                (
                    VALUES " + bangKy + @"
                ) AS ky(TenKy, TuNgay, DenNgay)

                OUTER APPLY
                (
                    SELECT
                        ISNULL(SUM(tt.SoTien), 0)
                            AS DoanhThu,
                        COUNT(*) AS SoGiaoDich
                    FROM dbo.ThanhToan AS tt
                    INNER JOIN dbo.HoaDon AS hd
                        ON hd.MaHoaDon = tt.MaHoaDon
                    INNER JOIN dbo.PhongTro AS p
                        ON p.MaPhong = hd.MaPhong
                    INNER JOIN dbo.DayTro AS d
                        ON d.MaDayTro = p.MaDayTro
                    WHERE d.MaChuTro = @MaChuTro
                      AND
                      (
                          @MaDayTro IS NULL
                          OR d.MaDayTro = @MaDayTro
                      )
                      AND tt.NgayThanhToan >= ky.TuNgay
                      AND tt.NgayThanhToan < ky.DenNgay
                ) AS doanhThu

                OUTER APPLY
                (
                    SELECT
                        ISNULL(
                            SUM(
                                CASE
                                    WHEN hd.TongTien >
                                        ISNULL(daTra.SoTien, 0)
                                    THEN hd.TongTien -
                                        ISNULL(daTra.SoTien, 0)
                                    ELSE 0
                                END
                            ),
                            0
                        ) AS CongNo,

                        ISNULL(
                            SUM(
                                CASE
                                    WHEN hd.TongTien >
                                        ISNULL(daTra.SoTien, 0)
                                    THEN 1
                                    ELSE 0
                                END
                            ),
                            0
                        ) AS SoHoaDonConNo
                    FROM dbo.HoaDon AS hd
                    INNER JOIN dbo.PhongTro AS p
                        ON p.MaPhong = hd.MaPhong
                    INNER JOIN dbo.DayTro AS d
                        ON d.MaDayTro = p.MaDayTro

                    OUTER APPLY
                    (
                        SELECT
                            SUM(tt.SoTien) AS SoTien
                        FROM dbo.ThanhToan AS tt
                        WHERE tt.MaHoaDon = hd.MaHoaDon
                          AND tt.NgayThanhToan < ky.DenNgay
                    ) AS daTra

                    WHERE d.MaChuTro = @MaChuTro
                      AND
                      (
                          @MaDayTro IS NULL
                          OR d.MaDayTro = @MaDayTro
                      )
                      AND hd.NgayLap < ky.DenNgay
                ) AS congNo

                OUTER APPLY
                (
                    SELECT
                        COUNT(*) AS TongPhong,
                        ISNULL(
                            SUM(
                                CASE
                                    WHEN hopDong.CoHopDong = 1
                                    THEN 1
                                    ELSE 0
                                END
                            ),
                            0
                        ) AS PhongDangThue
                    FROM dbo.PhongTro AS p
                    INNER JOIN dbo.DayTro AS d
                        ON d.MaDayTro = p.MaDayTro

                    OUTER APPLY
                    (
                        SELECT TOP (1)
                            1 AS CoHopDong
                        FROM dbo.HopDongThue AS h
                        WHERE h.MaPhong = p.MaPhong
                          AND h.NgayBatDau <=
                          (
                              CASE
                                  WHEN ky.DenNgay > @HomNay
                                  THEN @HomNay
                                  ELSE DATEADD(
                                      DAY,
                                      -1,
                                      ky.DenNgay
                                  )
                              END
                          )
                          AND h.NgayKetThuc >=
                          (
                              CASE
                                  WHEN ky.DenNgay > @HomNay
                                  THEN @HomNay
                                  ELSE DATEADD(
                                      DAY,
                                      -1,
                                      ky.DenNgay
                                  )
                              END
                          )
                          AND
                          (
                              h.NgayThanhLy IS NULL
                              OR h.NgayThanhLy >
                              (
                                  CASE
                                      WHEN ky.DenNgay > @HomNay
                                      THEN @HomNay
                                      ELSE DATEADD(
                                          DAY,
                                          -1,
                                          ky.DenNgay
                                      )
                                  END
                              )
                          )
                        ORDER BY h.NgayBatDau DESC
                    ) AS hopDong

                    WHERE d.MaChuTro = @MaChuTro
                      AND
                      (
                          @MaDayTro IS NULL
                          OR d.MaDayTro = @MaDayTro
                      )
                ) AS lapDay

                ORDER BY ky.TuNgay;";

            command.Parameters.Add(
                "@MaChuTro",
                SqlDbType.VarChar,
                20).Value = user.MaNguoiDung;

            command.Parameters.Add(
                "@MaDayTro",
                SqlDbType.VarChar,
                20).Value =
                    string.IsNullOrWhiteSpace(maDayTro)
                        ? DBNull.Value
                        : maDayTro;

            command.Parameters.Add(
                "@HomNay",
                SqlDbType.Date).Value = homNay;

            await connection.OpenAsync();

            using var reader =
                await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }
    }
}
