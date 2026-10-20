using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public sealed class KhachThueFormService : IDisposable
    {
        private readonly SqlConnection _connection =
            Database.CreateConnection();

        // Alias k phải là bảng KhachThue.
        private const string DieuKienQuyen = @"
            (
                EXISTS
                (
                    SELECT 1
                    FROM dbo.NhanVien nv
                    WHERE nv.MaDayTro = k.MaDayTro
                      AND nv.MaNguoiDung = @NguoiQuanLy
                      AND nv.ChucVu = N'Quản lý'
                      AND nv.TrangThaiLamViec = N'Đang làm'
                )
                OR EXISTS
                (
                    SELECT 1
                    FROM dbo.HopDongThue h
                    JOIN dbo.PhongTro p
                      ON p.MaPhong = h.MaPhong
                    JOIN dbo.NhanVien nv
                      ON nv.MaDayTro = p.MaDayTro
                    WHERE h.MaKhachThue = k.MaKhachThue
                      AND nv.MaNguoiDung = @NguoiQuanLy
                      AND nv.ChucVu = N'Quản lý'
                      AND nv.TrangThaiLamViec = N'Đang làm'
                )
            )";

        private static void KiemTraQuanLy(string maQuanLy)
        {
            var user = UserSession.CurrentUser;

            if (user == null
                || user.VaiTro != "Nhân viên quản lý"
                || user.MaNguoiDung != maQuanLy)
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền quản lý khách thuê.");
            }
        }

        public async Task MoKetNoiAsync()
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }
        }

        public static string TaoMa(string tienTo)
        {
            return tienTo
                + Guid.NewGuid().ToString("N")
                    .Substring(0, 20 - tienTo.Length)
                    .ToUpperInvariant();
        }

        public async Task<DataTable> LayDayTroAsync(
            string maQuanLy)
        {
            KiemTraQuanLy(maQuanLy);

            using var command = _connection.CreateCommand();

            command.CommandText = @"
                SELECT d.MaDayTro, d.TenDayTro
                FROM dbo.DayTro d
                WHERE EXISTS
                (
                    SELECT 1
                    FROM dbo.NhanVien nv
                    WHERE nv.MaDayTro = d.MaDayTro
                      AND nv.MaNguoiDung = @NguoiQuanLy
                      AND nv.ChucVu = N'Quản lý'
                      AND nv.TrangThaiLamViec = N'Đang làm'
                )
                ORDER BY d.TenDayTro;";

            command.Parameters.Add(
                "@NguoiQuanLy",
                SqlDbType.VarChar,
                20).Value = maQuanLy;

            using var reader =
                await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }

        public async Task<Dictionary<string, object>?>
            LayThanhVienAsync(
                string maThanhVien,
                string maQuanLy)
        {
            KiemTraQuanLy(maQuanLy);

            using var command = _connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    tv.HoTen,
                    tv.CCCD,
                    tv.SoDienThoai,
                    p.MaDayTro,
                    h.MaHopDong,
                    d.TenDayTro + N' - ' + p.SoPhong AS TenPhong,
                    tv.TrangThai
                FROM dbo.ThanhVienO tv
                JOIN dbo.HopDongThue h
                  ON h.MaHopDong = tv.MaHopDong
                JOIN dbo.PhongTro p
                  ON p.MaPhong = h.MaPhong
                JOIN dbo.DayTro d
                  ON d.MaDayTro = p.MaDayTro
                WHERE tv.MaThanhVien = @MaThanhVien
                  AND EXISTS
                  (
                      SELECT 1
                      FROM dbo.NhanVien nv
                      WHERE nv.MaDayTro = p.MaDayTro
                        AND nv.MaNguoiDung = @NguoiQuanLy
                        AND nv.ChucVu = N'Quản lý'
                        AND nv.TrangThaiLamViec = N'Đang làm'
                  );";

            command.Parameters.Add(
                "@MaThanhVien",
                SqlDbType.VarChar,
                20).Value = maThanhVien;

            command.Parameters.Add(
                "@NguoiQuanLy",
                SqlDbType.VarChar,
                20).Value = maQuanLy;

            return await DocMotDongAsync(command);
        }

        // Hàm đọc hồ sơ: chỉ nhận hai tham số.
        public async Task<Dictionary<string, object>?>
            LayKhachAsync(
                string maKhach,
                string maQuanLy)
        {
            KiemTraQuanLy(maQuanLy);

            using var command = _connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    n.HoTen,
                    n.CCCD,
                    n.SoDienThoai,
                    n.DiaChi,
                    COALESCE(k.MaDayTro, hd.MaDayTro) AS MaDayTro,
                    hd.MaHopDong,
                    hd.TenPhong,
                    hd.DangHieuLuc
                FROM dbo.KhachThue k
                JOIN dbo.NguoiDung n
                  ON n.MaNguoiDung = k.MaNguoiDung
                OUTER APPLY
                (
                    SELECT TOP (1)
                        h.MaHopDong,
                        p.MaDayTro,
                        d.TenDayTro + N' - ' + p.SoPhong AS TenPhong,
                        CASE
                            WHEN h.TrangThai = N'Hiệu lực'
                             AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                             AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                            THEN 1 ELSE 0
                        END AS DangHieuLuc
                    FROM dbo.HopDongThue h
                    JOIN dbo.PhongTro p
                      ON p.MaPhong = h.MaPhong
                    JOIN dbo.DayTro d
                      ON d.MaDayTro = p.MaDayTro
                    WHERE h.MaKhachThue = k.MaKhachThue
                      AND EXISTS
                      (
                          SELECT 1
                          FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @NguoiQuanLy
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
                WHERE k.MaKhachThue = @MaKhach
                  AND " + DieuKienQuyen + ";";

            command.Parameters.Add(
                "@MaKhach",
                SqlDbType.VarChar,
                20).Value = maKhach;

            command.Parameters.Add(
                "@NguoiQuanLy",
                SqlDbType.VarChar,
                20).Value = maQuanLy;

            return await DocMotDongAsync(command);
        }

        private static async Task<Dictionary<string, object>?>
            DocMotDongAsync(SqlCommand command)
        {
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

        public static async Task<DataTable> LayPhongOChungAsync(
            string maDay,
            string maQuanLy)
        {
            KiemTraQuanLy(maQuanLy);

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    h.MaHopDong,
                    p.MaPhong,
                    d.TenDayTro + N' - ' + p.SoPhong
                        + N' | Người đứng tên: ' + n.HoTen
                        AS TenHienThi
                FROM dbo.HopDongThue h
                JOIN dbo.PhongTro p
                  ON p.MaPhong = h.MaPhong
                JOIN dbo.DayTro d
                  ON d.MaDayTro = p.MaDayTro
                JOIN dbo.KhachThue k
                  ON k.MaKhachThue = h.MaKhachThue
                JOIN dbo.NguoiDung n
                  ON n.MaNguoiDung = k.MaNguoiDung
                WHERE p.MaDayTro = @MaDay
                  AND LTRIM(RTRIM(p.LoaiPhong)) = N'Phòng đôi'
                  AND h.TrangThai = N'Hiệu lực'
                  AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                  AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM dbo.ThanhVienO tv
                      JOIN dbo.HopDongThue h2
                        ON h2.MaHopDong = tv.MaHopDong
                      WHERE h2.MaPhong = p.MaPhong
                        AND tv.TrangThai = N'Đang ở'
                        AND h2.TrangThai = N'Hiệu lực'
                        AND h2.NgayBatDau <= CAST(GETDATE() AS DATE)
                        AND h2.NgayKetThuc >= CAST(GETDATE() AS DATE)
                  )
                  AND EXISTS
                  (
                      SELECT 1
                      FROM dbo.NhanVien nv
                      WHERE nv.MaDayTro = p.MaDayTro
                        AND nv.MaNguoiDung = @NguoiQuanLy
                        AND nv.ChucVu = N'Quản lý'
                        AND nv.TrangThaiLamViec = N'Đang làm'
                  )
                ORDER BY p.SoPhong;";

            command.Parameters.Add(
                "@MaDay",
                SqlDbType.VarChar,
                20).Value = maDay;

            command.Parameters.Add(
                "@NguoiQuanLy",
                SqlDbType.VarChar,
                20).Value = maQuanLy;

            await connection.OpenAsync();

            using var reader =
                await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }

        public static async Task LuuKhachAsync(
            string maQuanLy,
            bool dangSua,
            string maKhach,
            string tenDangNhapMoi,
            string matKhauMoi,
            Func<DuLieuNhapKhachThue> docThongTin)
        {
            KiemTraQuanLy(maQuanLy);

            if (!dangSua)
            {
                PasswordSecurity.ValidateNewPassword(matKhauMoi);
            }

            var thongTin =
                KiemTraDuLieu(docThongTin(), canDiaChi: true);

            if (!dangSua
                && string.IsNullOrWhiteSpace(thongTin.MaDayTro))
            {
                throw new InvalidOperationException(
                    "Vui lòng chọn dãy trọ.");
            }

            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            if (dangSua)
            {
                command.CommandText = @"
                    UPDATE n
                    SET
                        n.HoTen = @HoTen,
                        n.CCCD = @CCCD,
                        n.SoDienThoai = @SDT,
                        n.DiaChi = @DiaChi
                    OUTPUT INSERTED.MaNguoiDung
                    FROM dbo.NguoiDung n
                    JOIN dbo.KhachThue k
                      ON k.MaNguoiDung = n.MaNguoiDung
                    WHERE k.MaKhachThue = @MaKhach
                      AND " + DieuKienQuyen + ";";
            }
            else
            {
                command.CommandText = @"
                    IF NOT EXISTS
                    (
                        SELECT 1
                        FROM dbo.NhanVien nv
                        WHERE nv.MaDayTro = @MaDay
                          AND nv.MaNguoiDung = @NguoiQuanLy
                          AND nv.ChucVu = N'Quản lý'
                          AND nv.TrangThaiLamViec = N'Đang làm'
                    )
                        THROW 50001,
                            N'Bạn không có quyền với dãy trọ này.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM dbo.ThanhVienO tv
                        JOIN dbo.HopDongThue h
                          ON h.MaHopDong = tv.MaHopDong
                        WHERE tv.CCCD = @CCCD
                          AND tv.TrangThai = N'Đang ở'
                          AND h.TrangThai = N'Hiệu lực'
                          AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                          AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                    )
                        THROW 50002,
                            N'Người này đang là thành viên ở cùng. Không tạo hồ sơ trùng.', 1;

                    INSERT INTO dbo.NguoiDung
                    (
                        MaNguoiDung, TenDangNhap, MatKhau,
                        HoTen, CCCD, SoDienThoai, DiaChi, VaiTro
                    )
                    VALUES
                    (
                        @MaNguoiDung, @TenDangNhap, @MatKhau,
                        @HoTen, @CCCD, @SDT, @DiaChi, N'Khách thuê'
                    );

                    INSERT INTO dbo.KhachThue
                        (MaKhachThue, MaNguoiDung, MaDayTro)
                    VALUES
                        (@MaKhach, @MaNguoiDung, @MaDay);

                    SELECT @MaNguoiDung;";
            }

            command.Parameters.Add(
                "@NguoiQuanLy",
                SqlDbType.VarChar,
                20).Value = maQuanLy;

            command.Parameters.Add(
                "@MaKhach",
                SqlDbType.VarChar,
                20).Value = maKhach;

            ThemThongTin(command, thongTin);

            command.Parameters.Add(
                "@DiaChi",
                SqlDbType.NVarChar,
                255).Value = thongTin.DiaChi;

            if (!dangSua)
            {
                command.Parameters.Add(
                    "@MaNguoiDung",
                    SqlDbType.VarChar,
                    20).Value = TaoMa("ND");

                command.Parameters.Add(
                    "@MaDay",
                    SqlDbType.VarChar,
                    20).Value = thongTin.MaDayTro;

                command.Parameters.Add(
                    "@TenDangNhap",
                    SqlDbType.VarChar,
                    50).Value = tenDangNhapMoi;

                // Lưu mật khẩu thường.
                command.Parameters.Add(
                    "@MatKhau",
                    SqlDbType.VarChar,
                    255).Value = matKhauMoi;
            }

            object? result =
                await command.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
            {
                throw new InvalidOperationException(
                    "Hồ sơ không tồn tại hoặc bạn không còn quyền quản lý.");
            }

            transaction.Commit();
        }

        public static async Task CapNhatThanhVienAsync(
            string maQuanLy,
            string maThanhVien,
            Func<DuLieuNhapKhachThue> docThongTin)
        {
            KiemTraQuanLy(maQuanLy);

            var thongTin =
                KiemTraDuLieu(docThongTin(), canDiaChi: false);

            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = @"
                DECLARE @CCCDCu VARCHAR(12);
                DECLARE @TimThay BIT = 0;

                SELECT
                    @CCCDCu = tv.CCCD,
                    @TimThay = 1
                FROM dbo.ThanhVienO tv WITH (UPDLOCK, HOLDLOCK)
                JOIN dbo.HopDongThue h
                  ON h.MaHopDong = tv.MaHopDong
                JOIN dbo.PhongTro p
                  ON p.MaPhong = h.MaPhong
                WHERE tv.MaThanhVien = @MaThanhVien
                  AND EXISTS
                  (
                      SELECT 1
                      FROM dbo.NhanVien nv
                      WHERE nv.MaDayTro = p.MaDayTro
                        AND nv.MaNguoiDung = @NguoiQuanLy
                        AND nv.ChucVu = N'Quản lý'
                        AND nv.TrangThaiLamViec = N'Đang làm'
                  );

                IF @TimThay = 0
                    THROW 50006,
                        N'Thành viên không tồn tại hoặc không có quyền.', 1;

                IF
                (
                    SELECT COUNT(*)
                    FROM dbo.NguoiDung n
                    WHERE n.CCCD = @CCCDCu
                      AND n.VaiTro = N'Khách thuê'
                      AND NOT EXISTS
                      (
                          SELECT 1 FROM dbo.KhachThue k
                          WHERE k.MaNguoiDung = n.MaNguoiDung
                      )
                ) > 1
                    THROW 50007,
                        N'Có nhiều tài khoản trùng CCCD; cần kiểm tra dữ liệu.', 1;

                -- Giữ nguyên tên đăng nhập và mật khẩu.
                UPDATE n
                SET
                    n.HoTen = @HoTen,
                    n.CCCD = @CCCD,
                    n.SoDienThoai = @SDT
                FROM dbo.NguoiDung n
                WHERE n.CCCD = @CCCDCu
                  AND n.VaiTro = N'Khách thuê'
                  AND NOT EXISTS
                  (
                      SELECT 1 FROM dbo.KhachThue k
                      WHERE k.MaNguoiDung = n.MaNguoiDung
                  );

                UPDATE dbo.ThanhVienO
                SET
                    HoTen = @HoTen,
                    CCCD = @CCCD,
                    SoDienThoai = @SDT
                WHERE MaThanhVien = @MaThanhVien;";

            command.Parameters.Add(
                "@MaThanhVien",
                SqlDbType.VarChar,
                20).Value = maThanhVien;

            command.Parameters.Add(
                "@NguoiQuanLy",
                SqlDbType.VarChar,
                20).Value = maQuanLy;

            ThemThongTin(command, thongTin);

            await command.ExecuteNonQueryAsync();

            transaction.Commit();
        }

        // Hàm này chỉ thêm mới thành viên, không có dangSua.
        public static async Task LuuThanhVienAsync(
            string maQuanLy,
            string maPhong,
            string maHopDong,
            string maThanhVienMoi,
            string tenDangNhapThanhVienMoi,
            string matKhauMoi,
            Func<DuLieuNhapKhachThue> docThongTin)
        {
            KiemTraQuanLy(maQuanLy);

            PasswordSecurity.ValidateNewPassword(matKhauMoi);

            var thongTin =
                KiemTraDuLieu(docThongTin(), canDiaChi: false);

            if (string.IsNullOrWhiteSpace(maPhong)
                || string.IsNullOrWhiteSpace(maHopDong))
            {
                throw new InvalidOperationException(
                    "Vui lòng chọn phòng và hợp đồng hợp lệ.");
            }

            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = @"
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.PhongTro p WITH (UPDLOCK, HOLDLOCK)
                    WHERE p.MaPhong = @MaPhong
                      AND LTRIM(RTRIM(p.LoaiPhong)) = N'Phòng đôi'
                      AND EXISTS
                      (
                          SELECT 1
                          FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @NguoiQuanLy
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      )
                )
                    THROW 50001,
                        N'Phòng không phải phòng đôi hoặc bạn không còn quyền quản lý.', 1;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.HopDongThue h WITH (UPDLOCK, HOLDLOCK)
                    WHERE h.MaHopDong = @MaHopDong
                      AND h.MaPhong = @MaPhong
                      AND h.TrangThai = N'Hiệu lực'
                      AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                      AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                )
                    THROW 50002, N'Hợp đồng không còn hiệu lực.', 1;

                IF EXISTS
                (
                    SELECT 1
                    FROM dbo.ThanhVienO tv WITH (UPDLOCK, HOLDLOCK)
                    JOIN dbo.HopDongThue h
                      ON h.MaHopDong = tv.MaHopDong
                    WHERE h.MaPhong = @MaPhong
                      AND tv.TrangThai = N'Đang ở'
                      AND h.TrangThai = N'Hiệu lực'
                      AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                      AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                )
                    THROW 50003,
                        N'Phòng đã đủ 2 người, không thể thêm thành viên.', 1;

                IF EXISTS
                (
                    SELECT 1
                    FROM dbo.HopDongThue h
                    JOIN dbo.KhachThue k
                      ON k.MaKhachThue = h.MaKhachThue
                    JOIN dbo.NguoiDung n
                      ON n.MaNguoiDung = k.MaNguoiDung
                    WHERE n.CCCD = @CCCD
                      AND h.TrangThai = N'Hiệu lực'
                      AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                      AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                )
                    THROW 50004,
                        N'Người này đang đứng tên hợp đồng hiệu lực, không thể thêm trùng.', 1;

                IF EXISTS
                (
                    SELECT 1
                    FROM dbo.ThanhVienO tv
                    JOIN dbo.HopDongThue h
                      ON h.MaHopDong = tv.MaHopDong
                    WHERE tv.CCCD = @CCCD
                      AND tv.TrangThai = N'Đang ở'
                      AND h.TrangThai = N'Hiệu lực'
                      AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                      AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                )
                    THROW 50005,
                        N'Người này đã được ghi nhận đang ở trong một phòng.', 1;

                INSERT INTO dbo.NguoiDung
                (
                    MaNguoiDung, TenDangNhap, MatKhau,
                    HoTen, CCCD, SoDienThoai, DiaChi, VaiTro
                )
                VALUES
                (
                    @MaNguoiDung, @TenDangNhap, @MatKhau,
                    @HoTen, @CCCD, @SDT, @DiaChi, N'Khách thuê'
                );

                INSERT INTO dbo.ThanhVienO
                (
                    MaThanhVien, MaHopDong,
                    HoTen, CCCD, SoDienThoai,
                    QuanHe, TrangThai
                )
                VALUES
                (
                    @MaThanhVien, @MaHopDong,
                    @HoTen, @CCCD, @SDT,
                    N'Người ở cùng', N'Đang ở'
                );";

            command.Parameters.Add(
                "@NguoiQuanLy",
                SqlDbType.VarChar,
                20).Value = maQuanLy;

            command.Parameters.Add(
                "@MaPhong",
                SqlDbType.VarChar,
                20).Value = maPhong;

            command.Parameters.Add(
                "@MaHopDong",
                SqlDbType.VarChar,
                20).Value = maHopDong;

            command.Parameters.Add(
                "@MaThanhVien",
                SqlDbType.VarChar,
                20).Value = maThanhVienMoi;

            ThemThongTin(command, thongTin);

            command.Parameters.Add(
                "@MaNguoiDung",
                SqlDbType.VarChar,
                20).Value = TaoMa("ND");

            command.Parameters.Add(
                "@TenDangNhap",
                SqlDbType.VarChar,
                50).Value = tenDangNhapThanhVienMoi;

            // Lưu mật khẩu thường.
            command.Parameters.Add(
                "@MatKhau",
                SqlDbType.VarChar,
                255).Value = matKhauMoi;

            command.Parameters.Add(
                "@DiaChi",
                SqlDbType.NVarChar,
                255).Value = thongTin.DiaChi.Length > 0
                    ? thongTin.DiaChi
                    : "Chưa cập nhật";

            await command.ExecuteNonQueryAsync();

            transaction.Commit();
        }

        private static DuLieuNhapKhachThue KiemTraDuLieu(
            DuLieuNhapKhachThue thongTin,
            bool canDiaChi)
        {
            if (thongTin == null)
            {
                throw new InvalidOperationException(
                    "Chưa có thông tin khách thuê.");
            }

            string hoTen = thongTin.HoTen?.Trim() ?? "";
            string cccd = thongTin.CCCD?.Trim() ?? "";
            string sdt = thongTin.SoDienThoai?.Trim() ?? "";
            string diaChi = thongTin.DiaChi?.Trim() ?? "";
            string maDay = thongTin.MaDayTro?.Trim() ?? "";

            if (hoTen.Length == 0 || hoTen.Length > 100)
            {
                throw new InvalidOperationException(
                    "Họ tên phải có từ 1 đến 100 ký tự.");
            }

            if (!Regex.IsMatch(cccd, @"^[0-9]{12}$"))
            {
                throw new InvalidOperationException(
                    "CCCD phải gồm đúng 12 chữ số.");
            }

            if (!Regex.IsMatch(sdt, @"^0[0-9]{9}$"))
            {
                throw new InvalidOperationException(
                    "Số điện thoại gồm 10 chữ số, bắt đầu bằng 0.");
            }

            if (diaChi.Length > 255
                || (canDiaChi && diaChi.Length == 0))
            {
                throw new InvalidOperationException(
                    "Địa chỉ phải có từ 1 đến 255 ký tự.");
            }

            return new DuLieuNhapKhachThue
            {
                HoTen = hoTen,
                CCCD = cccd,
                SoDienThoai = sdt,
                DiaChi = diaChi,
                MaDayTro = maDay
            };
        }

        private static void ThemThongTin(
            SqlCommand command,
            DuLieuNhapKhachThue thongTin)
        {
            command.Parameters.Add(
                "@HoTen",
                SqlDbType.NVarChar,
                100).Value = thongTin.HoTen;

            command.Parameters.Add(
                "@CCCD",
                SqlDbType.VarChar,
                12).Value = thongTin.CCCD;

            command.Parameters.Add(
                "@SDT",
                SqlDbType.VarChar,
                15).Value = thongTin.SoDienThoai;
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }

    public sealed class DuLieuNhapKhachThue
    {
        public string HoTen { get; init; } = "";
        public string CCCD { get; init; } = "";
        public string SoDienThoai { get; init; } = "";
        public string DiaChi { get; init; } = "";
        public string MaDayTro { get; init; } = "";
    }
}