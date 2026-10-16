using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public sealed class ThemPhongService : IDisposable
    {
        private readonly SqlConnection _connection =
            Database.CreateConnection();

        public async Task MoKetNoiAsync()
        {
            await _connection.OpenAsync();
        }

        public async Task<DataTable> LayDanhSachDayAsync(
            string maNguoiDung)
        {
            var danhSachDay = new DataTable();

            using var command = _connection.CreateCommand();

            command.CommandText = @"
                SELECT d.MaDayTro, d.TenDayTro
                FROM dbo.DayTro AS d
                WHERE EXISTS (
                    SELECT 1
                    FROM dbo.NhanVien AS nv
                    WHERE nv.MaDayTro = d.MaDayTro
                      AND nv.MaNguoiDung = @MaNguoiDung
                      AND nv.ChucVu = N'Quản lý'
                      AND nv.TrangThaiLamViec = N'Đang làm'
                )
                ORDER BY d.TenDayTro;";

            command.Parameters.Add(
                "@MaNguoiDung", SqlDbType.VarChar, 20)
                .Value = maNguoiDung;

            using var reader =
                await command.ExecuteReaderAsync();

            danhSachDay.Load(reader);

            return danhSachDay;
        }

        public async Task<ThongTinPhongChinhSua?> LayPhongAsync(
            string maPhong,
            string maNguoiDung)
        {
            using var command = _connection.CreateCommand();

            command.CommandText = @"
                SELECT p.MaDayTro, p.SoPhong, p.LoaiPhong,
                       p.DienTich, p.GiaThue, p.TrangThai
                FROM dbo.PhongTro AS p
                WHERE p.MaPhong = @MaPhong
                  AND EXISTS (
                      SELECT 1
                      FROM dbo.NhanVien AS nv
                      WHERE nv.MaDayTro = p.MaDayTro
                        AND nv.MaNguoiDung = @MaNguoiDung
                        AND nv.ChucVu = N'Quản lý'
                        AND nv.TrangThaiLamViec = N'Đang làm'
                  );";

            command.Parameters.Add(
                "@MaPhong", SqlDbType.VarChar, 20)
                .Value = maPhong;

            command.Parameters.Add(
                "@MaNguoiDung", SqlDbType.VarChar, 20)
                .Value = maNguoiDung;

            using var reader =
                await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return new ThongTinPhongChinhSua
            {
                MaDayTro = reader["MaDayTro"],
                SoPhong = reader["SoPhong"],
                LoaiPhong = reader["LoaiPhong"],
                DienTich = reader["DienTich"],
                GiaThue = reader["GiaThue"],
                TrangThai = reader["TrangThai"]
            };
        }

        public static async Task<int> LuuAsync(
            bool laCheDoSua,
            string maNguoiDung,
            string maPhong,
            string maDayTro,
            string soPhong,
            string loaiPhong,
            decimal dienTich,
            decimal giaThue,
            string trangThai)
        {
            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            if (laCheDoSua)
            {
                command.CommandText = @"
                    UPDATE p
                    SET p.LoaiPhong = @LoaiPhong,
                        p.DienTich = @DienTich,
                        p.GiaThue = @GiaThue,
                        p.TrangThai = CASE
                            WHEN p.TrangThai = N'Đang thuê'
                                 OR @TrangThai = N'Đang thuê'
                                THEN p.TrangThai
                            ELSE @TrangThai
                        END
                    FROM dbo.PhongTro AS p
                    WHERE p.MaPhong = @MaPhong
                      AND p.MaDayTro = @MaDayTro
                      AND EXISTS (
                          SELECT 1
                          FROM dbo.NhanVien AS nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @MaNguoiDung
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      );";
            }
            else
            {
                command.CommandText = @"
                    INSERT INTO dbo.PhongTro
                        (MaPhong, MaDayTro, SoPhong, LoaiPhong,
                         DienTich, GiaThue, TrangThai)
                    SELECT
                        @MaPhong, @MaDayTro, @SoPhong, @LoaiPhong,
                        @DienTich, @GiaThue, @TrangThai
                    WHERE EXISTS (
                        SELECT 1
                        FROM dbo.NhanVien AS nv
                        WHERE nv.MaNguoiDung = @MaNguoiDung
                          AND nv.MaDayTro = @MaDayTro
                          AND nv.ChucVu = N'Quản lý'
                          AND nv.TrangThaiLamViec = N'Đang làm'
                    );";
            }

            command.Parameters.Add(
                "@MaNguoiDung", SqlDbType.VarChar, 20)
                .Value = maNguoiDung;

            command.Parameters.Add(
                "@MaPhong", SqlDbType.VarChar, 20)
                .Value = maPhong;

            command.Parameters.Add(
                "@MaDayTro", SqlDbType.VarChar, 20)
                .Value = maDayTro;

            command.Parameters.Add(
                "@SoPhong", SqlDbType.NVarChar, 20)
                .Value = soPhong;

            command.Parameters.Add(
                "@LoaiPhong", SqlDbType.NVarChar, 50)
                .Value = string.IsNullOrWhiteSpace(loaiPhong)
                    ? (object)DBNull.Value
                    : loaiPhong;

            var thamSoDienTich = command.Parameters.Add(
                "@DienTich", SqlDbType.Decimal);

            thamSoDienTich.Precision = 10;
            thamSoDienTich.Scale = 2;
            thamSoDienTich.Value = dienTich;

            var thamSoGiaThue = command.Parameters.Add(
                "@GiaThue", SqlDbType.Decimal);

            thamSoGiaThue.Precision = 18;
            thamSoGiaThue.Scale = 2;
            thamSoGiaThue.Value = giaThue;

            command.Parameters.Add(
                "@TrangThai", SqlDbType.NVarChar, 50)
                .Value = trangThai;

            await connection.OpenAsync();

            return await command.ExecuteNonQueryAsync();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }

    public sealed class ThongTinPhongChinhSua
    {
        public object MaDayTro { get; init; } = DBNull.Value;
        public object SoPhong { get; init; } = DBNull.Value;
        public object LoaiPhong { get; init; } = DBNull.Value;
        public object DienTich { get; init; } = DBNull.Value;
        public object GiaThue { get; init; } = DBNull.Value;
        public object TrangThai { get; init; } = DBNull.Value;
    }
}