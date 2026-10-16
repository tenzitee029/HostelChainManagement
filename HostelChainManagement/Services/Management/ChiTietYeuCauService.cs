using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public static class ChiTietYeuCauService
    {
        public static async Task<Dictionary<string, object>?> LayThongTinAsync(
            string maSuCo,
            string maQuanLy)
        {
            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    s.NgayGui,
                    s.NoiDungSuCo,
                    s.MucDoUuTien,
                    s.ThoiGianMongMuon,
                    s.TrangThai,
                    s.NgayTiepNhan,
                    s.LyDoTuChoi,
                    s.TepDinhKem,
                    n.HoTen,
                    d.TenDayTro + N' - Phòng ' + p.SoPhong AS TenPhong
                FROM dbo.PhieuSuCo s
                JOIN dbo.PhongTro p ON p.MaPhong = s.MaPhong
                JOIN dbo.DayTro d ON d.MaDayTro = p.MaDayTro
                JOIN dbo.NguoiDung n ON n.MaNguoiDung = s.MaNguoiGui
                WHERE s.MaSuCo = @MaSuCo
                  AND EXISTS (
                      SELECT 1 FROM dbo.NhanVien nv
                      WHERE nv.MaDayTro = p.MaDayTro
                        AND nv.MaNguoiDung = @QuanLy
                        AND nv.ChucVu = N'Quản lý'
                        AND nv.TrangThaiLamViec = N'Đang làm'
                  );";

            command.Parameters.Add(
                "@MaSuCo", SqlDbType.VarChar, 20)
                .Value = maSuCo;

            command.Parameters.Add(
                "@QuanLy", SqlDbType.VarChar, 20)
                .Value = maQuanLy;

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            var result = new Dictionary<string, object>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                result[reader.GetName(i)] = reader.GetValue(i);
            }

            return result;
        }

        public static async Task<int> CapNhatAsync(
            string maSuCo,
            string maQuanLy,
            bool tuChoi,
            string lyDo)
        {
            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            // Kiểm tra trạng thái và quyền ngay trong câu cập nhật.
            command.CommandText = @"
                DECLARE @DaCapNhat TABLE (MaSuCo VARCHAR(20));

                UPDATE s
                SET s.TrangThai = CASE
                        WHEN @TuChoi = 1 THEN N'Từ chối'
                        ELSE N'Đã tiếp nhận'
                    END,
                    s.NgayTiepNhan = CASE
                        WHEN @TuChoi = 0 THEN GETDATE()
                        ELSE s.NgayTiepNhan
                    END,
                    s.LyDoTuChoi = CASE
                        WHEN @TuChoi = 1 THEN @LyDo
                        ELSE NULL
                    END
                OUTPUT INSERTED.MaSuCo INTO @DaCapNhat
                FROM dbo.PhieuSuCo s
                JOIN dbo.PhongTro p ON p.MaPhong = s.MaPhong
                WHERE s.MaSuCo = @MaSuCo
                  AND s.TrangThai = N'Chờ tiếp nhận'
                  AND EXISTS (
                      SELECT 1 FROM dbo.NhanVien nv
                      WHERE nv.MaDayTro = p.MaDayTro
                        AND nv.MaNguoiDung = @QuanLy
                        AND nv.ChucVu = N'Quản lý'
                        AND nv.TrangThaiLamViec = N'Đang làm'
                  );

                SELECT COUNT(*) FROM @DaCapNhat;";

            command.Parameters.Add(
                "@MaSuCo", SqlDbType.VarChar, 20)
                .Value = maSuCo;

            command.Parameters.Add(
                "@QuanLy", SqlDbType.VarChar, 20)
                .Value = maQuanLy;

            command.Parameters.Add(
                "@TuChoi", SqlDbType.Bit)
                .Value = tuChoi;

            command.Parameters.Add(
                "@LyDo", SqlDbType.NVarChar, 255)
                .Value = tuChoi ? (object)lyDo : DBNull.Value;

            await connection.OpenAsync();

            return Convert.ToInt32(
                await command.ExecuteScalarAsync());
        }
    }
}