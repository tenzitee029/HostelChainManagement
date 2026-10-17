using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Models;

namespace QuanLyChuoiNhaTro.Services.Tenant
{
    public sealed class DuLieuSuCoKhachThue
    {
        public DataTable HopDong { get; init; } = new();

        public DataTable SuCo { get; init; } = new();
    }

    public static class SuCoKhachThueService
    {
        public const long MaxFileSize = 20L * 1024 * 1024;

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
            return prefix + Guid.NewGuid().ToString("N")[..17];
        }

        private static object Optional(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? DBNull.Value
                : value;
        }

        public static async Task<DuLieuSuCoKhachThue> GetAsync()
        {
            var user = RequireTenant();

            using var connection = Database.CreateConnection();

            await connection.OpenAsync();

            var contracts = new DataTable();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT
                        h.MaHopDong,
                        n.HoTen,
                        p.SoPhong,
                        d.TenDayTro

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
                      AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                      AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)

                    ORDER BY
                        h.NgayBatDau DESC,
                        h.MaHopDong;";

                command.Parameters.Add(
                    "@User",
                    SqlDbType.VarChar,
                    20).Value = user.MaNguoiDung;

                using var reader =
                    await command.ExecuteReaderAsync();

                contracts.Load(reader);
            }

            var incidents = new DataTable();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT
                        s.MaSuCo,
                        p.SoPhong,
                        d.TenDayTro,
                        s.NgayGui,
                        s.NoiDungSuCo,
                        s.MucDoUuTien,
                        s.ThoiGianMongMuon,
                        s.TepDinhKem,
                        s.TrangThai,
                        s.NgayTiepNhan,
                        s.LyDoTuChoi

                    FROM dbo.PhieuSuCo AS s

                    JOIN dbo.PhongTro AS p
                        ON p.MaPhong = s.MaPhong

                    JOIN dbo.DayTro AS d
                        ON d.MaDayTro = p.MaDayTro

                    WHERE s.MaNguoiGui = @User

                    ORDER BY
                        s.NgayGui DESC,
                        s.MaSuCo;";

                command.Parameters.Add(
                    "@User",
                    SqlDbType.VarChar,
                    20).Value = user.MaNguoiDung;

                using var reader =
                    await command.ExecuteReaderAsync();

                incidents.Load(reader);
            }

            return new DuLieuSuCoKhachThue
            {
                HopDong = contracts,
                SuCo = incidents
            };
        }

        private static async Task<string?> SaveAttachmentAsync(
            string? sourcePath,
            string maSuCo)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                return null;

            string extension =
                Path.GetExtension(sourcePath).ToLowerInvariant();

            if (extension is not (
                ".png" or ".jpg" or ".jpeg" or ".mp4"))
            {
                throw new InvalidOperationException(
                    "Chỉ nhận ảnh PNG/JPG hoặc video MP4.");
            }

            using var input = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                useAsync: true);

            if (input.Length == 0)
            {
                throw new InvalidOperationException(
                    "Tệp đính kèm không có nội dung.");
            }

            if (input.Length > MaxFileSize)
            {
                throw new InvalidOperationException(
                    "Dung lượng tệp đính kèm vượt quá 20 MB. " +
                    "Vui lòng chọn tệp dung lượng nhỏ hơn.");
            }

            // Kiểm tra dấu hiệu định dạng cơ bản,
            // tránh chỉ đổi đuôi file thành ảnh/video.
            byte[] header =
                new byte[(int)Math.Min(input.Length, 12)];

            await input.ReadExactlyAsync(header.AsMemory());

            bool valid = extension switch
            {
                ".png" =>
                    header.Length >= 8
                    && header.AsSpan(0, 8).SequenceEqual(
                        new byte[]
                        {
                            137, 80, 78, 71, 13, 10, 26, 10
                        }),

                ".jpg" or ".jpeg" =>
                    header.Length >= 3
                    && header[0] == 255
                    && header[1] == 216
                    && header[2] == 255,

                ".mp4" =>
                    header.Length >= 12
                    && header[4] == (byte)'f'
                    && header[5] == (byte)'t'
                    && header[6] == (byte)'y'
                    && header[7] == (byte)'p',

                _ => false
            };

            if (!valid)
            {
                throw new InvalidOperationException(
                    "Nội dung tệp không phù hợp định dạng PNG/JPG/MP4.");
            }

            input.Position = 0;

            // Có thể đặt biến này thành thư mục dùng chung
            // nếu nhiều máy cùng sử dụng một database.
            string root =
                Environment.GetEnvironmentVariable(
                    "NHATRO_ATTACHMENT_DIR")
                ?? Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "QuanLyChuoiNhaTro",
                    "TepSuCo");

            root = Path.GetFullPath(root);

            string destination =
                Path.Combine(root, maSuCo + extension);

            if (destination.Length > 500)
            {
                throw new InvalidOperationException(
                    "Đường dẫn lưu tệp quá dài.");
            }

            Directory.CreateDirectory(root);

            bool created = false;

            try
            {
                using var output = new FileStream(
                    destination,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true);

                created = true;

                byte[] buffer = new byte[81920];
                long total = 0;

                int count;

                while ((count = await input.ReadAsync(
                    buffer.AsMemory())) > 0)
                {
                    total += count;

                    if (total > MaxFileSize)
                    {
                        throw new InvalidOperationException(
                            "Dung lượng tệp đính kèm vượt quá 20 MB.");
                    }

                    await output.WriteAsync(
                        buffer.AsMemory(0, count));
                }

                return destination;
            }
            catch
            {
                if (created)
                    TryDelete(destination);

                throw;
            }
        }

        private static void TryDelete(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Không che mất lỗi chính nếu dọn tệp thất bại.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public static async Task<string> GuiAsync(
            string maHopDong,
            string nhomSuCo,
            string noiDung,
            string mucDoUuTien,
            string thoiGianMongMuon,
            string? sourceFile)
        {
            var user = RequireTenant();

            noiDung = noiDung.Trim();
            thoiGianMongMuon = thoiGianMongMuon.Trim();

            if (string.IsNullOrWhiteSpace(maHopDong)
                || maHopDong.Length > 20)
            {
                throw new InvalidOperationException(
                    "Vui lòng chọn hợp đồng đang thuê.");
            }

            if (nhomSuCo is not (
                "Điện"
                or "Nước"
                or "Thiết bị gia dụng"
                or "Kết cấu phòng"))
            {
                throw new InvalidOperationException(
                    "Vui lòng chọn nhóm sự cố hợp lệ.");
            }

            if (noiDung.Length == 0)
            {
                throw new InvalidOperationException(
                    "Vui lòng nhập nội dung yêu cầu sửa chữa.");
            }

            string fullContent =
                $"[Nhóm: {nhomSuCo}] {noiDung}";

            if (fullContent.Length > 500)
            {
                throw new InvalidOperationException(
                    "Nội dung yêu cầu quá dài.");
            }

            if (mucDoUuTien is not (
                "Bình thường" or "Khẩn cấp"))
            {
                throw new InvalidOperationException(
                    "Mức độ ưu tiên không hợp lệ.");
            }

            if (thoiGianMongMuon.Length == 0
                || thoiGianMongMuon.Length > 100)
            {
                throw new InvalidOperationException(
                    "Thời gian mong muốn phải có từ 1 đến 100 ký tự.");
            }

            string id = NewId("SC_");

            using var connection = Database.CreateConnection();

            await connection.OpenAsync();

            string? savedFile =
                await SaveAttachmentAsync(sourceFile, id);

            using var command = connection.CreateCommand();

            command.CommandText = @"
                SET XACT_ABORT ON;

                BEGIN TRY
                    BEGIN TRANSACTION;

                    DECLARE @Room VARCHAR(20);
                    DECLARE @Day VARCHAR(20);

                    SELECT
                        @Room = h.MaPhong,
                        @Day = p.MaDayTro

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
                      AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                      AND h.NgayKetThuc >= CAST(GETDATE() AS DATE);

                    IF @Room IS NULL
                        THROW 50010,
                            N'Không tìm thấy phòng đang thuê thuộc tài khoản của bạn.',
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
                        THROW 50011,
                            N'Dãy trọ chưa có quản lý đang hoạt động. Vui lòng liên hệ chủ nhà trọ.',
                            1;

                    INSERT INTO dbo.PhieuSuCo
                    (
                        MaSuCo,
                        MaPhong,
                        MaNguoiGui,
                        NgayGui,
                        NoiDungSuCo,
                        MucDoUuTien,
                        TepDinhKem,
                        ThoiGianMongMuon,
                        TrangThai
                    )
                    VALUES
                    (
                        @Id,
                        @Room,
                        @User,
                        GETDATE(),
                        @Content,
                        @Priority,
                        @File,
                        @Time,
                        N'Chờ tiếp nhận'
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
                        N'Yêu cầu sửa chữa / bảo trì',
                        N'Khách thuê gửi phiếu sự cố '
                        + @Id + N'. ' + @Content
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
                "@Content",
                SqlDbType.NVarChar,
                500).Value = fullContent;

            command.Parameters.Add(
                "@Priority",
                SqlDbType.NVarChar,
                30).Value = mucDoUuTien;

            command.Parameters.Add(
                "@Time",
                SqlDbType.NVarChar,
                100).Value = thoiGianMongMuon;

            command.Parameters.Add(
                "@File",
                SqlDbType.NVarChar,
                500).Value = Optional(savedFile);

            try
            {
                return (string)
                    (await command.ExecuteScalarAsync())!;
            }
            catch (SqlException ex)
                when (ex.Number is 50010 or 50011)
            {
                // Các lỗi nghiệp vụ này đã rollback SQL.
                TryDelete(savedFile);

                throw new InvalidOperationException(
                    ex.Message,
                    ex);
            }

            // Với lỗi mất kết nối, không xóa tệp tại đây:
            // SQL có thể đã commit nhưng phản hồi chưa tới ứng dụng.
        }
    }
}