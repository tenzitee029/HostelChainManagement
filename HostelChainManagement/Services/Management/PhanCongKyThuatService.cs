using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public sealed class PhanCongKyThuatService : IDisposable
    {
        private readonly SqlConnection _connection =
            Database.CreateConnection();

        public async Task MoKetNoiAsync()
        {
            await _connection.OpenAsync();
        }

        private static string TaoMa(string tienTo)
        {
            return tienTo + Guid.NewGuid().ToString("N")
                .Substring(0, 20 - tienTo.Length)
                .ToUpperInvariant();
        }

        public async Task<Dictionary<string, object>?> LayYeuCauAsync(
            string maSuCo,
            string maQuanLy)
        {
            using var command = _connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    s.TrangThai,
                    s.NoiDungSuCo,
                    s.MucDoUuTien,
                    p.MaDayTro,
                    d.TenDayTro + N' - ' + p.SoPhong AS TenPhong,
                    CASE WHEN EXISTS (
                        SELECT 1
                        FROM dbo.ChiTietSuaChua ct
                        WHERE ct.MaSuCo = s.MaSuCo
                    ) THEN 1 ELSE 0 END AS DaPhanCong
                FROM dbo.PhieuSuCo s
                JOIN dbo.PhongTro p ON p.MaPhong = s.MaPhong
                JOIN dbo.DayTro d ON d.MaDayTro = p.MaDayTro
                WHERE s.MaSuCo = @MaSuCo
                  AND EXISTS (
                      SELECT 1
                      FROM dbo.NhanVien nv
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

            using var reader =
                await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            var result = new Dictionary<string, object>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                result[reader.GetName(i)] = reader.GetValue(i);
            }

            return result;
        }

        public async Task<DataTable> LayNhanVienAsync(string maDay)
        {
            using var command = _connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    nv.MaNhanVien,
                    n.HoTen,
                    (
                        SELECT COUNT(*)
                        FROM dbo.ChiTietSuaChua ct
                        JOIN dbo.NhanVien nv2
                          ON nv2.MaNhanVien = ct.MaNhanVien
                        WHERE nv2.MaNguoiDung = nv.MaNguoiDung
                          AND ct.TrangThai IN
                              (N'Được phân công', N'Đang xử lý')
                    ) AS SoCongViec
                FROM dbo.NhanVien nv
                JOIN dbo.NguoiDung n
                  ON n.MaNguoiDung = nv.MaNguoiDung
                WHERE nv.MaDayTro = @MaDay
                  AND nv.ChucVu = N'Kỹ thuật'
                  AND nv.TrangThaiLamViec = N'Đang làm'
                  AND n.VaiTro = N'Nhân viên kỹ thuật'
                  AND n.TrangThai = N'Hoạt động'
                ORDER BY SoCongViec, n.HoTen, nv.MaNhanVien;";

            command.Parameters.Add(
                "@MaDay", SqlDbType.VarChar, 20)
                .Value = maDay;

            using var reader =
                await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }

        public static async Task LuuAsync(
            string maSuCo,
            string maDayDaTai,
            string maQuanLy,
            string maNhanVien,
            string uuTien,
            DateTime han)
        {
            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = @"
                DECLARE @MaDay VARCHAR(20);
                DECLARE @TrangThai NVARCHAR(30);
                DECLARE @NguoiNhan VARCHAR(20);

                SELECT
                    @MaDay = p.MaDayTro,
                    @TrangThai = s.TrangThai
                FROM dbo.PhieuSuCo s WITH (UPDLOCK, HOLDLOCK)
                JOIN dbo.PhongTro p ON p.MaPhong = s.MaPhong
                WHERE s.MaSuCo = @MaSuCo;

                IF @MaDay IS NULL
                    THROW 50001,
                        N'Yêu cầu không còn tồn tại.', 1;

                IF @MaDay <> @MaDayCu
                    THROW 50002,
                        N'Phòng đã đổi dãy. Hãy mở lại form phân công.', 1;

                IF @TrangThai <> N'Đã tiếp nhận'
                    THROW 50003,
                        N'Yêu cầu không còn ở trạng thái Đã tiếp nhận.', 1;

                IF NOT EXISTS (
                    SELECT 1
                    FROM dbo.NhanVien ql
                    WHERE ql.MaDayTro = @MaDay
                      AND ql.MaNguoiDung = @QuanLy
                      AND ql.ChucVu = N'Quản lý'
                      AND ql.TrangThaiLamViec = N'Đang làm'
                )
                    THROW 50004,
                        N'Bạn không còn quyền quản lý yêu cầu.', 1;

                SELECT @NguoiNhan = n.MaNguoiDung
                FROM dbo.NhanVien nv WITH (HOLDLOCK)
                JOIN dbo.NguoiDung n WITH (HOLDLOCK)
                  ON n.MaNguoiDung = nv.MaNguoiDung
                WHERE nv.MaNhanVien = @MaNhanVien
                  AND nv.MaDayTro = @MaDay
                  AND nv.ChucVu = N'Kỹ thuật'
                  AND nv.TrangThaiLamViec = N'Đang làm'
                  AND n.VaiTro = N'Nhân viên kỹ thuật'
                  AND n.TrangThai = N'Hoạt động';

                IF @NguoiNhan IS NULL
                    THROW 50005,
                        N'Kỹ thuật viên không còn phù hợp với dãy trọ.', 1;

                IF @Han < CAST(GETDATE() AS DATE)
                    THROW 50006,
                        N'Hạn dự kiến không được trong quá khứ.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM dbo.ChiTietSuaChua WITH (UPDLOCK, HOLDLOCK)
                    WHERE MaSuCo = @MaSuCo
                )
                    THROW 50007,
                        N'Yêu cầu đã có phân công. Hãy tải lại danh sách.', 1;

                INSERT INTO dbo.ChiTietSuaChua
                    (MaChiTietSuaChua, MaSuCo, MaNhanVien,
                     TienDo, TrangThai)
                VALUES
                    (@MaChiTiet, @MaSuCo, @MaNhanVien,
                     N'Đang kiểm tra', N'Được phân công');

                INSERT INTO dbo.LichSuTienDo
                    (MaChiTietSuaChua, TienDo, GhiChu)
                VALUES
                    (@MaChiTiet, N'Đang kiểm tra',
                     N'Phân công mới. Hạn hoàn thành dự kiến: '
                     + CONVERT(NVARCHAR(10), @Han, 103));

                UPDATE dbo.PhieuSuCo
                SET TrangThai = N'Đã phân công',
                    MucDoUuTien = @UuTien
                WHERE MaSuCo = @MaSuCo;

                INSERT INTO dbo.ThongBao
                    (MaThongBao, MaNguoiGui, TieuDe, NoiDung)
                VALUES
                    (@MaThongBao, @QuanLy,
                     N'Phân công xử lý yêu cầu sửa chữa',
                     N'Bạn được phân công yêu cầu ' + @MaSuCo
                     + N'. Ưu tiên: ' + @UuTien
                     + N'. Hạn dự kiến: '
                     + CONVERT(NVARCHAR(10), @Han, 103));

                INSERT INTO dbo.ThongBaoNguoiNhan
                    (MaThongBao, MaNguoiNhan, DaDoc)
                VALUES
                    (@MaThongBao, @NguoiNhan, 0);";

            command.Parameters.Add(
                "@MaSuCo", SqlDbType.VarChar, 20)
                .Value = maSuCo;

            command.Parameters.Add(
                "@MaDayCu", SqlDbType.VarChar, 20)
                .Value = maDayDaTai;

            command.Parameters.Add(
                "@QuanLy", SqlDbType.VarChar, 20)
                .Value = maQuanLy;

            command.Parameters.Add(
                "@MaNhanVien", SqlDbType.VarChar, 20)
                .Value = maNhanVien;

            command.Parameters.Add(
                "@MaChiTiet", SqlDbType.VarChar, 20)
                .Value = TaoMa("SC");

            command.Parameters.Add(
                "@MaThongBao", SqlDbType.VarChar, 20)
                .Value = TaoMa("TB");

            command.Parameters.Add(
                "@UuTien", SqlDbType.NVarChar, 30)
                .Value = uuTien;

            command.Parameters.Add(
                "@Han", SqlDbType.Date)
                .Value = han;

            await command.ExecuteNonQueryAsync();
            transaction.Commit();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}