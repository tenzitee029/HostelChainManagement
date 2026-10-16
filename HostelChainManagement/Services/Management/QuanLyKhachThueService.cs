using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public static class QuanLyKhachThueService
    {
        public static async Task<DataTable> LayDanhSachAsync(
            string maNguoiDung)
        {
            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    k.MaKhachThue,
                    CAST(N'Người đứng tên hợp đồng' AS NVARCHAR(50))
                        AS LoaiHoSo,
                    k.MaNguoiDung,
                    k.MaDayTro,
                    n.HoTen,
                    n.CCCD,
                    n.SoDienThoai,
                    n.DiaChi,
                    hd.MaPhong,
                    hd.MaHopDong,

                    CASE
                        WHEN hd.DangHieuLuc = 1
                        THEN hd.TenDayTro + N' - ' + hd.SoPhong
                        ELSE N'—'
                    END AS PhongDangThue,

                    CASE
                        WHEN hd.DangHieuLuc = 1 THEN N'Đang thuê'
                        WHEN hd.MaHopDong IS NULL THEN N'Chưa có hợp đồng'
                        ELSE N'Không có hợp đồng hiệu lực'
                    END AS TrangThai

                FROM dbo.KhachThue k
                JOIN dbo.NguoiDung n
                  ON n.MaNguoiDung = k.MaNguoiDung

                OUTER APPLY (
                    SELECT TOP (1)
                        h.MaHopDong,
                        p.MaPhong,
                        p.SoPhong,
                        d.TenDayTro,

                        CASE
                            WHEN h.TrangThai = N'Hiệu lực'
                             AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                             AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                            THEN 1 ELSE 0
                        END AS DangHieuLuc

                    FROM dbo.HopDongThue h
                    JOIN dbo.PhongTro p ON p.MaPhong = h.MaPhong
                    JOIN dbo.DayTro d ON d.MaDayTro = p.MaDayTro

                    WHERE h.MaKhachThue = k.MaKhachThue
                      AND EXISTS (
                          SELECT 1
                          FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @MaNguoiDung
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      )

                    ORDER BY
                        CASE
                            WHEN h.TrangThai = N'Hiệu lực'
                             AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                             AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                            THEN 0 ELSE 1
                        END,
                        h.NgayBatDau DESC,
                        h.MaHopDong
                ) hd

                WHERE hd.MaHopDong IS NOT NULL
                   OR EXISTS (
                       SELECT 1
                       FROM dbo.NhanVien nv
                       WHERE nv.MaDayTro = k.MaDayTro
                         AND nv.MaNguoiDung = @MaNguoiDung
                         AND nv.ChucVu = N'Quản lý'
                         AND nv.TrangThaiLamViec = N'Đang làm'
                   )

                UNION ALL

                SELECT
                    tv.MaThanhVien AS MaKhachThue,
                    CAST(N'Thành viên ở cùng' AS NVARCHAR(50))
                        AS LoaiHoSo,
                    CAST(NULL AS VARCHAR(20)) AS MaNguoiDung,
                    p.MaDayTro,
                    tv.HoTen,
                    tv.CCCD,
                    tv.SoDienThoai,
                    CAST(NULL AS NVARCHAR(255)) AS DiaChi,
                    p.MaPhong,
                    h.MaHopDong,

                    CASE
                        WHEN tv.TrangThai = N'Đang ở'
                         AND h.TrangThai = N'Hiệu lực'
                         AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                         AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                        THEN d.TenDayTro + N' - ' + p.SoPhong
                        ELSE N'—'
                    END AS PhongDangThue,

                    CASE
                        WHEN tv.TrangThai = N'Đang ở'
                         AND h.TrangThai = N'Hiệu lực'
                         AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                         AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                        THEN N'Đang ở cùng'
                        ELSE N'Không ở hiện tại'
                    END AS TrangThai

                FROM dbo.ThanhVienO tv
                JOIN dbo.HopDongThue h
                  ON h.MaHopDong = tv.MaHopDong
                JOIN dbo.PhongTro p ON p.MaPhong = h.MaPhong
                JOIN dbo.DayTro d ON d.MaDayTro = p.MaDayTro

                WHERE EXISTS (
                    SELECT 1
                    FROM dbo.NhanVien nv
                    WHERE nv.MaDayTro = p.MaDayTro
                      AND nv.MaNguoiDung = @MaNguoiDung
                      AND nv.ChucVu = N'Quản lý'
                      AND nv.TrangThaiLamViec = N'Đang làm'
                )

                ORDER BY HoTen, MaKhachThue;";

            command.Parameters.Add(
                "@MaNguoiDung", SqlDbType.VarChar, 20)
                .Value = maNguoiDung;

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }

        public static async Task XoaAsync(
            string maKhach,
            string maNguoiQuanLy,
            bool laThanhVien,
            Action<bool> nhanKetQua)
        {
            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            if (laThanhVien)
            {
                command.CommandText = @"
                    DECLARE @DaXoa TABLE (MaThanhVien VARCHAR(20));

                    DELETE tv
                    OUTPUT DELETED.MaThanhVien INTO @DaXoa
                    FROM dbo.ThanhVienO tv
                    JOIN dbo.HopDongThue h ON h.MaHopDong = tv.MaHopDong
                    JOIN dbo.PhongTro p ON p.MaPhong = h.MaPhong
                    WHERE tv.MaThanhVien = @MaKhach
                      AND EXISTS (
                          SELECT 1 FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @NguoiQuanLy
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      );

                    SELECT COUNT(*) FROM @DaXoa;";
            }
            else
            {
                command.CommandText = @"
                    DECLARE @DaXoa TABLE (MaKhachThue VARCHAR(20));

                    DELETE k
                    OUTPUT DELETED.MaKhachThue INTO @DaXoa
                    FROM dbo.KhachThue k
                    WHERE k.MaKhachThue = @MaKhach
                      AND NOT EXISTS (
                          SELECT 1
                          FROM dbo.HopDongThue h
                          WHERE h.MaKhachThue = k.MaKhachThue
                      )
                      AND EXISTS (
                          SELECT 1
                          FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = k.MaDayTro
                            AND nv.MaNguoiDung = @NguoiQuanLy
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      );

                    SELECT COUNT(*) FROM @DaXoa;";
            }

            command.Parameters.Add(
                "@MaKhach", SqlDbType.VarChar, 20)
                .Value = maKhach;

            command.Parameters.Add(
                "@NguoiQuanLy", SqlDbType.VarChar, 20)
                .Value = maNguoiQuanLy;

            object? ketQua = await command.ExecuteScalarAsync();

            nhanKetQua(
                ketQua != null && Convert.ToInt32(ketQua) == 1);

            transaction.Commit();
        }
    }
}