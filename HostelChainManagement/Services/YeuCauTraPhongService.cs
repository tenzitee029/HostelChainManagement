using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Models;

namespace QuanLyChuoiNhaTro.Services
{
    public sealed class DuLieuTraPhong
    {
        public DataTable HopDong { get; init; } = new();

        public DataTable YeuCau { get; init; } = new();

        public DateTime NgayHienTai { get; init; }
    }

    public static class YeuCauTraPhongService
    {
        private static LoggedInUser RequireTenant()
        {
            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Khách thuê")
            {
                throw new InvalidOperationException(
                    "Bạn phải đăng nhập bằng tài khoản Khách thuê.");
            }

            return user;
        }

        private static string NewId(string prefix)
        {
            // Tiền tố 3 ký tự + 17 ký tự = VARCHAR(20).
            return prefix + Guid.NewGuid().ToString("N")[..17];
        }

        private static object Optional(string text)
        {
            return text.Length == 0 ? DBNull.Value : text;
        }

        public static async Task<DuLieuTraPhong> GetAsync()
        {
            var user = RequireTenant();

            using var connection = Database.CreateConnection();

            await connection.OpenAsync();

            DateTime today;

            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT CAST(GETDATE() AS DATE);";

                today = (DateTime)
                    (await command.ExecuteScalarAsync())!;
            }

            var contracts = new DataTable();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT
                        h.MaHopDong,
                        n.HoTen,
                        p.SoPhong,
                        d.TenDayTro,
                        h.NgayKetThuc,
                        h.DieuKhoan

                    FROM dbo.HopDongThue AS h

                    JOIN dbo.KhachThue AS k
                        ON k.MaKhachThue = h.MaKhachThue

                    JOIN dbo.NguoiDung AS n
                        ON n.MaNguoiDung = k.MaNguoiDung

                    JOIN dbo.PhongTro AS p
                        ON p.MaPhong = h.MaPhong

                    JOIN dbo.DayTro AS d
                        ON d.MaDayTro = p.MaDayTro

                    WHERE k.MaNguoiDung = @User
                      AND h.TrangThai = N'Hiệu lực'
                      AND h.NgayThanhLy IS NULL
                      AND h.NgayBatDau <= @Today
                      AND h.NgayKetThuc >= @Today

                    ORDER BY
                        h.NgayBatDau DESC,
                        h.MaHopDong;";

                command.Parameters.Add(
                    "@User",
                    SqlDbType.VarChar,
                    20).Value = user.MaNguoiDung;

                command.Parameters.Add(
                    "@Today",
                    SqlDbType.Date).Value = today;

                using var reader =
                    await command.ExecuteReaderAsync();

                contracts.Load(reader);
            }

            var requests = new DataTable();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT
                        y.MaYeuCau,
                        y.MaHopDong,
                        p.SoPhong,
                        y.NgayYeuCau,
                        y.NgayDuKienTra,
                        y.LyDo,
                        y.ThongTinHoanCoc,
                        y.TrangThai,
                        y.GhiChuXuLy,
                        h.TrangThai AS TrangThaiHopDong,
                        h.NgayThanhLy

                    FROM dbo.YeuCauTraPhong AS y

                    JOIN dbo.HopDongThue AS h
                        ON h.MaHopDong = y.MaHopDong

                    JOIN dbo.KhachThue AS k
                        ON k.MaKhachThue = h.MaKhachThue

                    JOIN dbo.PhongTro AS p
                        ON p.MaPhong = h.MaPhong

                    WHERE k.MaNguoiDung = @User

                    ORDER BY
                        y.NgayYeuCau DESC,
                        y.MaYeuCau;";

                command.Parameters.Add(
                    "@User",
                    SqlDbType.VarChar,
                    20).Value = user.MaNguoiDung;

                using var reader =
                    await command.ExecuteReaderAsync();

                requests.Load(reader);
            }

            return new DuLieuTraPhong
            {
                HopDong = contracts,
                YeuCau = requests,
                NgayHienTai = today
            };
        }

        public static async Task<string> GuiAsync(
            string maHopDong,
            DateTime ngayTra,
            string lyDo,
            string thongTinHoanCoc)
        {
            var user = RequireTenant();

            lyDo = lyDo.Trim();
            thongTinHoanCoc = thongTinHoanCoc.Trim();

            if (string.IsNullOrWhiteSpace(maHopDong)
                || maHopDong.Length > 20)
            {
                throw new InvalidOperationException(
                    "Vui lòng chọn hợp đồng thuê.");
            }

            if (lyDo.Length > 255
                || thongTinHoanCoc.Length > 255)
            {
                throw new InvalidOperationException(
                    "Lý do và thông tin hoàn cọc " +
                    "không được quá 255 ký tự.");
            }

            string id = NewId("TP_");

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SET XACT_ABORT ON;

                BEGIN TRY
                    BEGIN TRANSACTION;

                    DECLARE @Today DATE =
                        CAST(GETDATE() AS DATE);

                    DECLARE @Day VARCHAR(20);
                    DECLARE @Room VARCHAR(20);

                    -- Khóa hợp đồng trong lúc ghi,
                    -- tránh hai lần gửi đồng thời.
                    SELECT
                        @Day = p.MaDayTro,
                        @Room = p.SoPhong

                    FROM dbo.HopDongThue AS h
                        WITH (UPDLOCK, HOLDLOCK)

                    JOIN dbo.KhachThue AS k
                        ON k.MaKhachThue = h.MaKhachThue

                    JOIN dbo.PhongTro AS p
                        ON p.MaPhong = h.MaPhong

                    WHERE h.MaHopDong = @Contract
                      AND k.MaNguoiDung = @User
                      AND h.TrangThai = N'Hiệu lực'
                      AND h.NgayThanhLy IS NULL
                      AND h.NgayBatDau <= @Today
                      AND h.NgayKetThuc >= @Today;

                    IF @Day IS NULL
                        THROW 50001,
                            N'Không tìm thấy hợp đồng đang thuê thuộc tài khoản của bạn.',
                            1;

                    IF @Date <= @Today
                        THROW 50002,
                            N'Ngày dự kiến trả phòng không hợp lệ. Vui lòng chọn ngày sau hôm nay.',
                            1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM dbo.YeuCauTraPhong
                        WHERE MaHopDong = @Contract
                          AND TrangThai IN
                              (N'Chờ xử lý', N'Đã duyệt')
                    )
                        THROW 50003,
                            N'Hợp đồng đã có yêu cầu trả phòng đang chờ xử lý hoặc đã duyệt.',
                            1;

                    DECLARE @Managers TABLE
                    (
                        Id VARCHAR(20) PRIMARY KEY
                    );

                    INSERT INTO @Managers (Id)
                    SELECT DISTINCT nv.MaNguoiDung

                    FROM dbo.NhanVien AS nv

                    JOIN dbo.NguoiDung AS n
                        ON n.MaNguoiDung = nv.MaNguoiDung

                    WHERE nv.MaDayTro = @Day
                      AND nv.ChucVu = N'Quản lý'
                      AND nv.TrangThaiLamViec = N'Đang làm'
                      AND n.VaiTro = N'Nhân viên quản lý'
                      AND n.TrangThai = N'Hoạt động';

                    IF NOT EXISTS
                        (SELECT 1 FROM @Managers)
                        THROW 50004,
                            N'Dãy trọ chưa có quản lý đang hoạt động. Vui lòng liên hệ chủ nhà trọ.',
                            1;

                    INSERT INTO dbo.YeuCauTraPhong
                    (
                        MaYeuCau,
                        MaHopDong,
                        NgayYeuCau,
                        NgayDuKienTra,
                        LyDo,
                        ThongTinHoanCoc,
                        TrangThai
                    )
                    VALUES
                    (
                        @Id,
                        @Contract,
                        GETDATE(),
                        @Date,
                        @Reason,
                        @Refund,
                        N'Chờ xử lý'
                    );

                    INSERT INTO dbo.ThongBao
                    (
                        MaThongBao,
                        MaNguoiGui,
                        TieuDe,
                        NoiDung
                    )
                    VALUES
                    (
                        @Notice,
                        @User,
                        N'Yêu cầu trả phòng',

                        N'Khách thuê gửi yêu cầu ' + @Id
                        + N', hợp đồng ' + @Contract
                        + N', phòng ' + @Room
                        + N', ngày dự kiến trả: '
                        + CONVERT(NVARCHAR(10), @Date, 103)
                    );

                    INSERT INTO dbo.ThongBaoNguoiNhan
                    (
                        MaThongBao,
                        MaNguoiNhan
                    )
                    SELECT @Notice, Id
                    FROM @Managers;

                    COMMIT TRANSACTION;

                    SELECT @Id;
                END TRY

                BEGIN CATCH
                    IF @@TRANCOUNT > 0
                        ROLLBACK TRANSACTION;

                    THROW;
                END CATCH;";

            command.Parameters.Add(
                "@User",
                SqlDbType.VarChar,
                20).Value = user.MaNguoiDung;

            command.Parameters.Add(
                "@Contract",
                SqlDbType.VarChar,
                20).Value = maHopDong;

            command.Parameters.Add(
                "@Id",
                SqlDbType.VarChar,
                20).Value = id;

            command.Parameters.Add(
                "@Notice",
                SqlDbType.VarChar,
                20).Value = NewId("TB_");

            command.Parameters.Add(
                "@Date",
                SqlDbType.Date).Value = ngayTra.Date;

            command.Parameters.Add(
                "@Reason",
                SqlDbType.NVarChar,
                255).Value = Optional(lyDo);

            command.Parameters.Add(
                "@Refund",
                SqlDbType.NVarChar,
                255).Value = Optional(thongTinHoanCoc);

            await connection.OpenAsync();

            try
            {
                return (string)
                    (await command.ExecuteScalarAsync())!;
            }
            catch (SqlException ex)
                when (ex.Number >= 50001
                    && ex.Number <= 50004)
            {
                throw new InvalidOperationException(
                    ex.Message,
                    ex);
            }
        }

        public static async Task HuyAsync(string maYeuCau)
        {
            var user = RequireTenant();

            if (string.IsNullOrWhiteSpace(maYeuCau)
                || maYeuCau.Length > 20)
            {
                throw new InvalidOperationException(
                    "Vui lòng chọn yêu cầu cần hủy.");
            }

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SET XACT_ABORT ON;

                BEGIN TRY
                    BEGIN TRANSACTION;

                    DECLARE @Contract VARCHAR(20);
                    DECLARE @Day VARCHAR(20);

                    SELECT @Contract = y.MaHopDong

                    FROM dbo.YeuCauTraPhong AS y

                    JOIN dbo.HopDongThue AS h
                        ON h.MaHopDong = y.MaHopDong

                    JOIN dbo.KhachThue AS k
                        ON k.MaKhachThue = h.MaKhachThue

                    WHERE y.MaYeuCau = @Id
                      AND k.MaNguoiDung = @User;

                    SELECT @Day = p.MaDayTro

                    FROM dbo.HopDongThue AS h
                        WITH (UPDLOCK, HOLDLOCK)

                    JOIN dbo.PhongTro AS p
                        ON p.MaPhong = h.MaPhong

                    JOIN dbo.KhachThue AS k
                        ON k.MaKhachThue = h.MaKhachThue

                    WHERE h.MaHopDong = @Contract
                      AND k.MaNguoiDung = @User
                      AND h.TrangThai = N'Hiệu lực'
                      AND h.NgayThanhLy IS NULL;

                    IF @Day IS NULL
                        THROW 50005,
                            N'Không thể hủy yêu cầu của hợp đồng đã kết thúc hoặc đã thanh lý.',
                            1;

                    UPDATE dbo.YeuCauTraPhong
                    SET TrangThai = N'Đã hủy'

                    WHERE MaYeuCau = @Id
                      AND MaHopDong = @Contract
                      AND TrangThai = N'Chờ xử lý'
                      AND NgayDuKienTra >
                          CAST(GETDATE() AS DATE);

                    IF @@ROWCOUNT <> 1
                        THROW 50006,
                            N'Chỉ có thể hủy yêu cầu đang chờ xử lý và chưa đến ngày bàn giao.',
                            1;

                    DECLARE @Managers TABLE
                    (
                        Id VARCHAR(20) PRIMARY KEY
                    );

                    INSERT INTO @Managers (Id)
                    SELECT DISTINCT nv.MaNguoiDung

                    FROM dbo.NhanVien AS nv

                    JOIN dbo.NguoiDung AS n
                        ON n.MaNguoiDung = nv.MaNguoiDung

                    WHERE nv.MaDayTro = @Day
                      AND nv.ChucVu = N'Quản lý'
                      AND nv.TrangThaiLamViec = N'Đang làm'
                      AND n.VaiTro = N'Nhân viên quản lý'
                      AND n.TrangThai = N'Hoạt động';

                    IF EXISTS
                        (SELECT 1 FROM @Managers)
                    BEGIN
                        INSERT INTO dbo.ThongBao
                        (
                            MaThongBao,
                            MaNguoiGui,
                            TieuDe,
                            NoiDung
                        )
                        VALUES
                        (
                            @Notice,
                            @User,
                            N'Hủy yêu cầu trả phòng',
                            N'Khách thuê đã hủy yêu cầu ' + @Id
                        );

                        INSERT INTO dbo.ThongBaoNguoiNhan
                        (
                            MaThongBao,
                            MaNguoiNhan
                        )
                        SELECT @Notice, Id
                        FROM @Managers;
                    END;

                    COMMIT TRANSACTION;
                END TRY

                BEGIN CATCH
                    IF @@TRANCOUNT > 0
                        ROLLBACK TRANSACTION;

                    THROW;
                END CATCH;";

            command.Parameters.Add(
                "@Id",
                SqlDbType.VarChar,
                20).Value = maYeuCau;

            command.Parameters.Add(
                "@User",
                SqlDbType.VarChar,
                20).Value = user.MaNguoiDung;

            command.Parameters.Add(
                "@Notice",
                SqlDbType.VarChar,
                20).Value = NewId("TB_");

            await connection.OpenAsync();

            try
            {
                await command.ExecuteNonQueryAsync();
            }
            catch (SqlException ex)
                when (ex.Number is 50005 or 50006)
            {
                throw new InvalidOperationException(
                    ex.Message,
                    ex);
            }
        }
    }
}