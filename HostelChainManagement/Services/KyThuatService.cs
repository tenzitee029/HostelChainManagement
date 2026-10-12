using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Models;

namespace QuanLyChuoiNhaTro.Services;

public static class KyThuatService
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

    public static Task<DataTable> GetHistoryAsync(string id)
    {
        return QueryAsync(@"
            SELECT
                h.NgayCapNhat,
                h.TienDo,
                h.GhiChu
            FROM dbo.LichSuTienDo h
            JOIN dbo.ChiTietSuaChua ct
                ON ct.MaChiTietSuaChua =
                    h.MaChiTietSuaChua
            JOIN dbo.NhanVien nv
                ON nv.MaNhanVien = ct.MaNhanVien
            WHERE ct.MaChiTietSuaChua = @Id
              AND " + Scope + @"
            ORDER BY h.MaLichSu DESC;",
            id);
    }

    public static Task<DataTable> GetMaterialsAsync(string id)
    {
        return QueryAsync(@"
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
              AND " + Scope + ";",
            id);
    }

    public static async Task SaveAsync(
        string id,
        string expectedStatus,
        string expectedProgress,
        long expectedVersion,
        string progress,
        string note,
        string? photo,
        IReadOnlyList<VatTuKyThuat> materials)
    {
        bool complete = progress == "Hoàn thành";

        if (progress is not (
            "Đang kiểm tra"
            or "Chờ linh kiện"
            or "Đang sửa"
            or "Hoàn thành"))
        {
            throw new InvalidOperationException(
                "Tiến độ không hợp lệ.");
        }

        note = note.Trim();

        if (note.Length == 0 || note.Length > 500)
        {
            throw new InvalidOperationException(
                "Nhập ghi chú xử lý từ 1 đến 500 ký tự.");
        }

        if (complete
            && (string.IsNullOrWhiteSpace(photo)
                || photo.Length > 500))
        {
            throw new InvalidOperationException(
                "Vui lòng chọn ảnh nghiệm thu trước khi hoàn tất.");
        }

        decimal total = 0;

        foreach (var material in materials)
        {
            if (string.IsNullOrWhiteSpace(material.TenVatTu)
                || material.TenVatTu.Trim().Length > 100
                || material.SoLuong <= 0
                || material.SoLuong > 99999999.99m
                || decimal.Round(material.SoLuong, 2)
                    != material.SoLuong
                || material.DonGia < 0
                || material.DonGia > 999999999999999999m
                || decimal.Truncate(material.DonGia)
                    != material.DonGia)
            {
                throw new InvalidOperationException(
                    "Vật tư: tên tối đa 100 ký tự, "
                    + "số lượng dương (2 số lẻ), "
                    + "đơn giá VND nguyên không âm.");
            }

            total += material.ThanhTien;
        }

        if (total > 999999999999999999m
            || decimal.Truncate(total) != total)
        {
            throw new InvalidOperationException(
                "Tổng chi phí phải là số VND nguyên "
                + "và nằm trong giới hạn CSDL.");
        }

        string uid = UserId();

        using var connection = Database.CreateConnection();
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText = @"
            DECLARE @SuCo VARCHAR(20);

            SELECT @SuCo = ct.MaSuCo
            FROM dbo.ChiTietSuaChua ct
            JOIN dbo.NhanVien nv
                ON nv.MaNhanVien = ct.MaNhanVien
            WHERE ct.MaChiTietSuaChua = @Id
              AND " + Scope + @";

            IF @SuCo IS NULL
                THROW 50001,
                    N'Công việc không còn được phân công cho bạn.',
                    1;

            -- Khóa phiếu trước khi thay đổi phân công.
            SELECT MaSuCo
            FROM dbo.PhieuSuCo WITH (UPDLOCK, HOLDLOCK)
            WHERE MaSuCo = @SuCo;

            IF NOT EXISTS
            (
                SELECT 1
                FROM dbo.ChiTietSuaChua ct
                    WITH (UPDLOCK, HOLDLOCK)
                JOIN dbo.NhanVien nv
                    ON nv.MaNhanVien = ct.MaNhanVien
                JOIN dbo.PhieuSuCo s
                    ON s.MaSuCo = ct.MaSuCo
                WHERE ct.MaChiTietSuaChua = @Id
                  AND " + Scope + @"
                  AND ct.TrangThai = @ExpectedStatus
                  AND ct.TienDo = @ExpectedProgress
                  AND COALESCE
                  (
                      (
                          SELECT MAX(h.MaLichSu)
                          FROM dbo.LichSuTienDo h
                          WHERE h.MaChiTietSuaChua =
                              ct.MaChiTietSuaChua
                      ),
                      0
                  ) = @Version
                  AND ct.TrangThai IN
                  (
                      N'Được phân công',
                      N'Đang xử lý'
                  )
                  AND s.TrangThai IN
                  (
                      N'Đã phân công',
                      N'Đang xử lý'
                  )
            )
                THROW 50001,
                    N'Công việc đã thay đổi hoặc đã đóng. Hãy tải lại danh sách.',
                    1;

            UPDATE dbo.ChiTietSuaChua
            SET
                TienDo = @Progress,
                NoiDungXuLy = @Note,
                ThoiGianBatDau =
                    COALESCE(ThoiGianBatDau, GETDATE()),
                TrangThai = CASE
                    WHEN @Complete = 1
                        THEN N'Hoàn thành'
                    ELSE N'Đang xử lý'
                END,
                ThoiGianHoanThanh = CASE
                    WHEN @Complete = 1 THEN GETDATE()
                    ELSE NULL
                END,
                AnhSauSua = CASE
                    WHEN @Complete = 1 THEN @Photo
                    ELSE AnhSauSua
                END,
                ChiPhiVatTu = CASE
                    WHEN @Complete = 1 THEN @Total
                    ELSE ChiPhiVatTu
                END
            WHERE MaChiTietSuaChua = @Id;

            INSERT dbo.LichSuTienDo
            (
                MaChiTietSuaChua,
                TienDo,
                GhiChu
            )
            VALUES (@Id, @Progress, @Note);

            UPDATE dbo.PhieuSuCo
            SET TrangThai = CASE
                WHEN @Complete = 1
                    AND NOT EXISTS
                    (
                        SELECT 1
                        FROM dbo.ChiTietSuaChua
                        WHERE MaSuCo = @SuCo
                          AND TrangThai <> N'Hoàn thành'
                    )
                    THEN N'Đã hoàn thành'
                ELSE N'Đang xử lý'
            END
            WHERE MaSuCo = @SuCo;";

        command.Parameters.Add(
            "@User", SqlDbType.VarChar, 20).Value = uid;

        command.Parameters.Add(
            "@Id", SqlDbType.VarChar, 20).Value = id;

        command.Parameters.Add(
            "@Version", SqlDbType.BigInt).Value =
            expectedVersion;

        command.Parameters.Add(
            "@ExpectedStatus", SqlDbType.NVarChar, 30).Value =
            expectedStatus;

        command.Parameters.Add(
            "@ExpectedProgress", SqlDbType.NVarChar, 50).Value =
            expectedProgress;

        command.Parameters.Add(
            "@Progress", SqlDbType.NVarChar, 50).Value =
            progress;

        command.Parameters.Add(
            "@Note", SqlDbType.NVarChar, 500).Value = note;

        command.Parameters.Add(
            "@Complete", SqlDbType.Bit).Value = complete;

        command.Parameters.Add(
            "@Photo", SqlDbType.NVarChar, 500).Value =
            (object?)photo ?? DBNull.Value;

        var cost = command.Parameters.Add(
            "@Total", SqlDbType.Decimal);

        cost.Precision = 18;
        cost.Scale = 0;
        cost.Value = total;

        await command.ExecuteNonQueryAsync();

        if (complete)
        {
            command.CommandText = @"
                DELETE dbo.VatTuSuaChua
                WHERE MaChiTietSuaChua = @Id;";

            await command.ExecuteNonQueryAsync();

            var name = command.Parameters.Add(
                "@Name", SqlDbType.NVarChar, 100);

            var quantity = command.Parameters.Add(
                "@Qty", SqlDbType.Decimal);

            quantity.Precision = 10;
            quantity.Scale = 2;

            var price = command.Parameters.Add(
                "@Price", SqlDbType.Decimal);

            price.Precision = 18;
            price.Scale = 0;

            foreach (var material in materials)
            {
                command.CommandText = @"
                    INSERT dbo.VatTuSuaChua
                    (
                        MaChiTietSuaChua,
                        TenVatTu,
                        SoLuong,
                        DonGia
                    )
                    VALUES (@Id, @Name, @Qty, @Price);";

                name.Value = material.TenVatTu.Trim();
                quantity.Value = material.SoLuong;
                price.Value = material.DonGia;

                await command.ExecuteNonQueryAsync();
            }
        }

        await NotifyAsync(
            connection,
            transaction,
            uid,
            id,
            complete
                ? "Hoàn tất công việc kỹ thuật"
                : "Cập nhật tiến độ sửa chữa",
            progress + ": " + note);

        transaction.Commit();
    }

    // Người nhận là người báo sự cố và quản lý phụ trách dãy.
    private static async Task NotifyAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string uid,
        string id,
        string title,
        string body)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        command.CommandText = @"
            INSERT dbo.ThongBao
            (
                MaThongBao,
                MaNguoiGui,
                TieuDe,
                NoiDung
            )
            SELECT
                @Notification,
                @User,
                @Title,
                N'Phiếu ' + s.MaSuCo
                    + N' - Phòng ' + p.SoPhong
                    + N': ' + @Body
            FROM dbo.ChiTietSuaChua ct
            JOIN dbo.PhieuSuCo s
                ON s.MaSuCo = ct.MaSuCo
            JOIN dbo.PhongTro p
                ON p.MaPhong = s.MaPhong
            WHERE ct.MaChiTietSuaChua = @Id;

            INSERT dbo.ThongBaoNguoiNhan
            (
                MaThongBao,
                MaNguoiNhan
            )
            SELECT
                @Notification,
                recipient.MaNguoiDung
            FROM
            (
                SELECT s.MaNguoiGui AS MaNguoiDung
                FROM dbo.ChiTietSuaChua ct
                JOIN dbo.PhieuSuCo s
                    ON s.MaSuCo = ct.MaSuCo
                WHERE ct.MaChiTietSuaChua = @Id

                UNION

                SELECT nv.MaNguoiDung
                FROM dbo.ChiTietSuaChua ct
                JOIN dbo.PhieuSuCo s
                    ON s.MaSuCo = ct.MaSuCo
                JOIN dbo.PhongTro p
                    ON p.MaPhong = s.MaPhong
                JOIN dbo.NhanVien nv
                    ON nv.MaDayTro = p.MaDayTro
                WHERE ct.MaChiTietSuaChua = @Id
                  AND nv.ChucVu = N'Quản lý'
                  AND nv.TrangThaiLamViec = N'Đang làm'
            ) recipient;";

        command.Parameters.Add(
            "@Notification",
            SqlDbType.VarChar,
            20).Value =
            "TB" + Guid.NewGuid().ToString("N")[..18];

        command.Parameters.Add(
            "@User", SqlDbType.VarChar, 20).Value = uid;

        command.Parameters.Add(
            "@Id", SqlDbType.VarChar, 20).Value = id;

        command.Parameters.Add(
            "@Title", SqlDbType.NVarChar, 200).Value = title;

        command.Parameters.Add(
            "@Body", SqlDbType.NVarChar, -1).Value = body;

        await command.ExecuteNonQueryAsync();
    }

    public static async Task SendMessageAsync(
        string id,
        string title,
        string body)
    {
        title = title.Trim();
        body = body.Trim();

        if (title.Length is < 1 or > 200
            || body.Length is < 1 or > 2000)
        {
            throw new InvalidOperationException(
                "Nhập tiêu đề (1–200) "
                + "và nội dung thông báo (1–2000 ký tự).");
        }

        string uid = UserId();

        using var connection = Database.CreateConnection();
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText = @"
            DECLARE @SuCo VARCHAR(20);

            SELECT @SuCo = ct.MaSuCo
            FROM dbo.ChiTietSuaChua ct
            JOIN dbo.NhanVien nv
                ON nv.MaNhanVien = ct.MaNhanVien
            WHERE ct.MaChiTietSuaChua = @Id
              AND " + Scope + @";

            DECLARE @Locked VARCHAR(20);

            SELECT @Locked = MaSuCo
            FROM dbo.PhieuSuCo WITH (UPDLOCK, HOLDLOCK)
            WHERE MaSuCo = @SuCo;

            SELECT COUNT(*)
            FROM dbo.ChiTietSuaChua ct
                WITH (UPDLOCK, HOLDLOCK)
            JOIN dbo.NhanVien nv
                ON nv.MaNhanVien = ct.MaNhanVien
            WHERE ct.MaChiTietSuaChua = @Id
              AND " + Scope;

        command.Parameters.Add(
            "@User", SqlDbType.VarChar, 20).Value = uid;

        command.Parameters.Add(
            "@Id", SqlDbType.VarChar, 20).Value = id;

        int count = Convert.ToInt32(
            await command.ExecuteScalarAsync());

        if (count != 1)
        {
            throw new InvalidOperationException(
                "Bạn không có quyền gửi thông báo "
                + "cho công việc này.");
        }

        await NotifyAsync(
            connection,
            transaction,
            uid,
            id,
            title,
            body);

        transaction.Commit();
    }

    public static Task<DataTable> GetNotificationsAsync()
    {
        return QueryAsync(@"
            SELECT
                t.MaThongBao,
                t.TieuDe,
                t.NoiDung,
                t.NgayGui,
                n.DaDoc,
                u.HoTen AS NguoiGui
            FROM dbo.ThongBao t
            JOIN dbo.ThongBaoNguoiNhan n
                ON n.MaThongBao = t.MaThongBao
            JOIN dbo.NguoiDung u
                ON u.MaNguoiDung = t.MaNguoiGui
            WHERE n.MaNguoiNhan = @User
            ORDER BY t.NgayGui DESC;");
    }

    public static async Task MarkReadAsync(string id)
    {
        using var connection = Database.CreateConnection();
        using var command = connection.CreateCommand();

        command.CommandText = @"
            UPDATE dbo.ThongBaoNguoiNhan
            SET
                DaDoc = 1,
                NgayDoc = COALESCE(NgayDoc, GETDATE())
            WHERE MaThongBao = @Id
              AND MaNguoiNhan = @User;";

        command.Parameters.Add(
            "@Id", SqlDbType.VarChar, 20).Value = id;

        command.Parameters.Add(
            "@User", SqlDbType.VarChar, 20).Value = UserId();

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    public static async Task ChangePasswordAsync(
        string oldPassword,
        string newPassword)
    {
        if (newPassword.Length < 8
            || newPassword.Length > 255
            || newPassword.Any(c => c > 127)
            || newPassword == oldPassword)
        {
            throw new InvalidOperationException(
                "Mật khẩu mới cần 8–255 ký tự ASCII "
                + "và khác mật khẩu cũ.");
        }

        using var connection = Database.CreateConnection();
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();

        command.Transaction = transaction;

        command.Parameters.Add(
            "@User", SqlDbType.VarChar, 20).Value = UserId();

        command.CommandText = @"
            SELECT MatKhau
            FROM dbo.NguoiDung WITH (UPDLOCK, HOLDLOCK)
            WHERE MaNguoiDung = @User
              AND VaiTro = N'Nhân viên kỹ thuật'
              AND TrangThai = N'Hoạt động';";

        string? currentPassword =
            await command.ExecuteScalarAsync() as string;

        if (!string.Equals(
            currentPassword,
            oldPassword,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Mật khẩu hiện tại không chính xác "
                + "hoặc tài khoản không hoạt động.");
        }

        command.CommandText = @"
            UPDATE dbo.NguoiDung
            SET MatKhau = @Password
            WHERE MaNguoiDung = @User;";

        command.Parameters.Add(
            "@Password",
            SqlDbType.VarChar,
            255).Value = newPassword;

        await command.ExecuteNonQueryAsync();
        transaction.Commit();
    }
}