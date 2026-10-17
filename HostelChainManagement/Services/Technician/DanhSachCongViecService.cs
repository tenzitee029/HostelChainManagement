using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Services.Technician
{
    public static class DanhSachCongViecService
    {
        private static string UserId()
        {
            var user = UserSession.CurrentUser;

            if (user == null
                || user.VaiTro != "Nhân viên kỹ thuật")
            {
                throw new InvalidOperationException(
                    "Chức năng chỉ dành cho Nhân viên kỹ thuật.");
            }

            return user.MaNguoiDung;
        }

        // Kiểm tra đúng nhân viên, chức vụ và tài khoản đang hoạt động.
        private const string Scope = @"
            nv.MaNguoiDung = @User
            AND nv.ChucVu = N'Kỹ thuật'
            AND nv.TrangThaiLamViec = N'Đang làm'
            AND EXISTS
            (
                SELECT 1
                FROM dbo.NguoiDung u
                WHERE u.MaNguoiDung = @User
                  AND u.VaiTro = N'Nhân viên kỹ thuật'
                  AND u.TrangThai = N'Hoạt động'
            )";

        private static async Task<DataTable> QueryAsync(
            string sql,
            string? id = null)
        {
            string uid = UserId();

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;

            command.Parameters.Add(
                "@User",
                SqlDbType.VarChar,
                20).Value = uid;

            if (id != null)
            {
                command.Parameters.Add(
                    "@Id",
                    SqlDbType.VarChar,
                    20).Value = id;
            }

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            var result = new DataTable();
            result.Load(reader);

            return result;
        }

        public static Task<DataTable> GetJobsAsync()
        {
            return QueryAsync(@"
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.NhanVien nv
                    WHERE " + Scope + @"
                )
                    THROW 50001,
                        N'Tài khoản chưa có hồ sơ kỹ thuật đang làm việc.',
                        1;

                SELECT
                    ct.*,
                    COALESCE
                    (
                        (
                            SELECT MAX(h.MaLichSu)
                            FROM dbo.LichSuTienDo h
                            WHERE h.MaChiTietSuaChua =
                                ct.MaChiTietSuaChua
                        ),
                        0
                    ) AS HistoryVersion,
                    s.NoiDungSuCo,
                    s.MucDoUuTien,
                    s.TepDinhKem,
                    s.ThoiGianMongMuon,
                    s.TrangThai AS TrangThaiSuCo,
                    p.SoPhong,
                    d.MaDayTro,
                    d.TenDayTro,
                    d.DiaChi,
                    u.HoTen AS KhachThue,
                    u.SoDienThoai
                FROM dbo.ChiTietSuaChua ct
                JOIN dbo.NhanVien nv
                    ON nv.MaNhanVien = ct.MaNhanVien
                JOIN dbo.PhieuSuCo s
                    ON s.MaSuCo = ct.MaSuCo
                JOIN dbo.PhongTro p
                    ON p.MaPhong = s.MaPhong
                JOIN dbo.DayTro d
                    ON d.MaDayTro = p.MaDayTro
                JOIN dbo.NguoiDung u
                    ON u.MaNguoiDung = s.MaNguoiGui
                WHERE " + Scope + @"
                ORDER BY
                    CASE
                        WHEN ct.TrangThai = N'Hoàn thành'
                        THEN 1 ELSE 0
                    END,
                    CASE
                        WHEN s.MucDoUuTien = N'Khẩn cấp'
                        THEN 0 ELSE 1
                    END,
                    CASE ct.TrangThai
                        WHEN N'Được phân công' THEN 0
                        WHEN N'Đang xử lý' THEN 1
                        ELSE 2
                    END,
                    ct.NgayPhanCong DESC;");
        }
    }
}