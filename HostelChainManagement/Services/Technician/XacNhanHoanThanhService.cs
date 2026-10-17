using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Models;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Services.Technician
{
    public static class XacNhanHoanThanhService
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

        public static Task<DataTable> GetJobsAsync()
        {
            return DanhSachCongViecService.GetJobsAsync();
        }

        public static async Task<DataTable> GetMaterialsAsync(string id)
        {
            string uid = UserId();

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    v.TenVatTu,
                    v.SoLuong,
                    v.DonGia,
                    v.ThanhTien
                FROM dbo.VatTuSuaChua v
                JOIN dbo.ChiTietSuaChua ct
                    ON ct.MaChiTietSuaChua =
                        v.MaChiTietSuaChua
                JOIN dbo.NhanVien nv
                    ON nv.MaNhanVien = ct.MaNhanVien
                WHERE ct.MaChiTietSuaChua = @Id
                  AND " + Scope + ";";

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

        public static Task SaveAsync(
            string id,
            string expectedStatus,
            string expectedProgress,
            long expectedVersion,
            string progress,
            string note,
            string? photo,
            IReadOnlyList<VatTuKyThuat> materials)
        {
            return XuLyCongViecService.SaveAsync(
                id,
                expectedStatus,
                expectedProgress,
                expectedVersion,
                progress,
                note,
                photo,
                materials);
        }
    }
}