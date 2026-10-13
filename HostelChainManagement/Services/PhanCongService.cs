
using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services
{
    public static class PhanCongService
    {
        private sealed record ThongTinNhanVien(
            string MaNguoiDung,
            string HoTen,
            string? MaDayTro);

        public static async Task<DataTable> GetDanhSachDayTroAsync()
        {
            string maChuTro = LayMaChuTro();

            const string sql = @"
                SELECT
                    d.MaDayTro,
                    d.TenDayTro,
                    d.TrangThai,
                    COALESCE(
                        STRING_AGG(
                            CONVERT(NVARCHAR(MAX), nd.HoTen),
                            N', '
                        ),
                        N'Chưa phân công'
                    ) AS NguoiPhuTrach
                FROM dbo.DayTro AS d
                LEFT JOIN dbo.NhanVien AS nv
                    ON nv.MaDayTro = d.MaDayTro
                   AND nv.ChucVu = N'Quản lý'
                   AND nv.TrangThaiLamViec = N'Đang làm'
                LEFT JOIN dbo.NguoiDung AS nd
                    ON nd.MaNguoiDung = nv.MaNguoiDung
                   AND nd.TrangThai = N'Hoạt động'
                WHERE d.MaChuTro = @MaChuTro
                GROUP BY
                    d.MaDayTro,
                    d.TenDayTro,
                    d.TrangThai
                ORDER BY
                    d.TenDayTro,
                    d.MaDayTro;";

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;

            command.Parameters.Add(
                "@MaChuTro",
                SqlDbType.VarChar,
                20).Value = maChuTro;

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }

        public static async Task<DataTable> GetNhanVienQuanLyAsync()
        {
            string maChuTro = LayMaChuTro();

            const string sql = @"
                SELECT
                    nv.MaNhanVien,
                    nv.MaNguoiDung,
                    nd.HoTen,
                    nd.HoTen AS HoTenHienThi,
                    nv.MaDayTro,
                    nv.TrangThaiLamViec
                FROM dbo.ChuTroNhanVien AS ct
                INNER JOIN dbo.NhanVien AS nv
                    ON nv.MaNhanVien = ct.MaNhanVien
                INNER JOIN dbo.NguoiDung AS nd
                    ON nd.MaNguoiDung = nv.MaNguoiDung
                LEFT JOIN dbo.DayTro AS d
                    ON d.MaDayTro = nv.MaDayTro
                WHERE ct.MaChuTro = @MaChuTro
                  AND nv.ChucVu = N'Quản lý'
                  AND nv.TrangThaiLamViec = N'Đang làm'
                  AND nd.VaiTro = N'Nhân viên quản lý'
                  AND nd.TrangThai = N'Hoạt động'
                  AND
                  (
                      nv.MaDayTro IS NULL
                      OR d.MaChuTro = @MaChuTro
                  )
                ORDER BY
                    nd.HoTen,
                    nv.MaNhanVien;";

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;

            command.Parameters.Add(
                "@MaChuTro",
                SqlDbType.VarChar,
                20).Value = maChuTro;

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }

        public static async Task<string?> LuuPhanCongAsync(
            string maDayTro,
            string maNhanVien)
        {
            string maChuTro = LayMaChuTro();

            KiemTraMa(maDayTro, "Mã dãy trọ");
            KiemTraMa(maNhanVien, "Mã nhân viên");

            using var connection = Database.CreateConnection();

            await connection.OpenAsync();

            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            try
            {
                string tenDayTro = await LayTenDayTroAsync(
                    connection,
                    transaction,
                    maChuTro,
                    maDayTro,
                    true);

                ThongTinNhanVien nhanVien =
                    await LayNhanVienAsync(
                        connection,
                        transaction,
                        maChuTro,
                        maNhanVien,
                        true);

                if (nhanVien.MaDayTro == maDayTro)
                {
                    throw new InvalidOperationException(
                        "Nhân viên đã phụ trách dãy trọ này.");
                }

                string? maDayCu = nhanVien.MaDayTro;

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
                        UPDATE dbo.NhanVien
                        SET MaDayTro = @MaDayTro
                        WHERE MaNhanVien = @MaNhanVien;";

                    command.Parameters.Add(
                        "@MaDayTro",
                        SqlDbType.VarChar,
                        20).Value = maDayTro;

                    command.Parameters.Add(
                        "@MaNhanVien",
                        SqlDbType.VarChar,
                        20).Value = maNhanVien;

                    int soDong =
                        await command.ExecuteNonQueryAsync();

                    if (soDong != 1)
                    {
                        throw new InvalidOperationException(
                            "Không thể lưu phân công nhân viên.");
                    }
                }

                await GhiThongBaoAsync(
                    connection,
                    transaction,
                    maChuTro,
                    nhanVien.MaNguoiDung,
                    "Phân công quản lý dãy trọ",
                    "Bạn được phân công phụ trách dãy trọ "
                    + tenDayTro
                    + ". Vui lòng kiểm tra công việc tại dãy trọ mới.");

                string? dayChuaCoQuanLy = null;

                if (!string.IsNullOrWhiteSpace(maDayCu))
                {
                    dayChuaCoQuanLy =
                        await KiemTraDayChuaCoQuanLyAsync(
                            connection,
                            transaction,
                            maChuTro,
                            maDayCu);
                }

                transaction.Commit();

                return dayChuaCoQuanLy;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public static async Task<bool> HuyPhanCongAsync(
            string maDayTro,
            string maNhanVien)
        {
            string maChuTro = LayMaChuTro();

            KiemTraMa(maDayTro, "Mã dãy trọ");
            KiemTraMa(maNhanVien, "Mã nhân viên");

            using var connection = Database.CreateConnection();

            await connection.OpenAsync();

            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            try
            {
                string tenDayTro = await LayTenDayTroAsync(
                    connection,
                    transaction,
                    maChuTro,
                    maDayTro,
                    false);

                ThongTinNhanVien nhanVien =
                    await LayNhanVienAsync(
                        connection,
                        transaction,
                        maChuTro,
                        maNhanVien,
                        false);

                if (nhanVien.MaDayTro != maDayTro)
                {
                    throw new InvalidOperationException(
                        "Nhân viên không được phân công tại dãy trọ đã chọn.");
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
                        UPDATE dbo.NhanVien
                        SET MaDayTro = NULL
                        WHERE MaNhanVien = @MaNhanVien
                          AND MaDayTro = @MaDayTro;";

                    command.Parameters.Add(
                        "@MaNhanVien",
                        SqlDbType.VarChar,
                        20).Value = maNhanVien;

                    command.Parameters.Add(
                        "@MaDayTro",
                        SqlDbType.VarChar,
                        20).Value = maDayTro;

                    int soDong =
                        await command.ExecuteNonQueryAsync();

                    if (soDong != 1)
                    {
                        throw new InvalidOperationException(
                            "Không thể hủy quyền phân công.");
                    }
                }

                await GhiThongBaoAsync(
                    connection,
                    transaction,
                    maChuTro,
                    nhanVien.MaNguoiDung,
                    "Hủy phân công quản lý",
                    "Bạn không còn được phân công phụ trách dãy trọ "
                    + tenDayTro
                    + ". Quyền quản lý dãy trọ này đã được thu hồi.");

                string? dayChuaCoQuanLy =
                    await KiemTraDayChuaCoQuanLyAsync(
                        connection,
                        transaction,
                        maChuTro,
                        maDayTro);

                transaction.Commit();

                return dayChuaCoQuanLy != null;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static async Task<string> LayTenDayTroAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string maChuTro,
            string maDayTro,
            bool yeuCauHoatDong)
        {
            using var command = connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText = @"
                SELECT TenDayTro
                FROM dbo.DayTro WITH (UPDLOCK, HOLDLOCK)
                WHERE MaDayTro = @MaDayTro
                  AND MaChuTro = @MaChuTro
                  AND
                  (
                      @YeuCauHoatDong = 0
                      OR TrangThai = N'Đang hoạt động'
                  );";

            command.Parameters.Add(
                "@MaDayTro",
                SqlDbType.VarChar,
                20).Value = maDayTro;

            command.Parameters.Add(
                "@MaChuTro",
                SqlDbType.VarChar,
                20).Value = maChuTro;

            command.Parameters.Add(
                "@YeuCauHoatDong",
                SqlDbType.Bit).Value = yeuCauHoatDong;

            object? result = await command.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
            {
                throw new InvalidOperationException(
                    "Dãy trọ không thuộc quyền quản lý của bạn "
                    + "hoặc không đáp ứng điều kiện hoạt động.");
            }

            return Convert.ToString(result) ?? "";
        }

        private static async Task<ThongTinNhanVien> LayNhanVienAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string maChuTro,
            string maNhanVien,
            bool yeuCauDangLam)
        {
            using var command = connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText = @"
                SELECT
                    nd.MaNguoiDung,
                    nd.HoTen,
                    nv.MaDayTro
                FROM dbo.NhanVien AS nv WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.ChuTroNhanVien AS ct
                    ON ct.MaNhanVien = nv.MaNhanVien
                INNER JOIN dbo.NguoiDung AS nd
                    ON nd.MaNguoiDung = nv.MaNguoiDung
                LEFT JOIN dbo.DayTro AS d
                    ON d.MaDayTro = nv.MaDayTro
                WHERE nv.MaNhanVien = @MaNhanVien
                  AND ct.MaChuTro = @MaChuTro
                  AND nv.ChucVu = N'Quản lý'
                  AND nd.VaiTro = N'Nhân viên quản lý'
                  AND
                  (
                      nv.MaDayTro IS NULL
                      OR d.MaChuTro = @MaChuTro
                  )
                  AND
                  (
                      @YeuCauDangLam = 0
                      OR
                      (
                          nv.TrangThaiLamViec = N'Đang làm'
                          AND nd.TrangThai = N'Hoạt động'
                      )
                  );";

            command.Parameters.Add(
                "@MaNhanVien",
                SqlDbType.VarChar,
                20).Value = maNhanVien;

            command.Parameters.Add(
                "@MaChuTro",
                SqlDbType.VarChar,
                20).Value = maChuTro;

            command.Parameters.Add(
                "@YeuCauDangLam",
                SqlDbType.Bit).Value = yeuCauDangLam;

            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException(
                    "Nhân viên quản lý không tồn tại, "
                    + "không thuộc chủ trọ hoặc không đủ điều kiện phân công.");
            }

            string maNguoiDung = reader.GetString(0);
            string hoTen = reader.GetString(1);

            string? maDayTro = reader.IsDBNull(2)
                ? null
                : reader.GetString(2);

            return new ThongTinNhanVien(
                maNguoiDung,
                hoTen,
                maDayTro);
        }

        private static async Task<string?> KiemTraDayChuaCoQuanLyAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string maChuTro,
            string maDayTro)
        {
            using var command = connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText = @"
                SELECT
                    CASE
                        WHEN EXISTS
                        (
                            SELECT 1
                            FROM dbo.NhanVien AS nv
                            INNER JOIN dbo.NguoiDung AS nd
                                ON nd.MaNguoiDung = nv.MaNguoiDung
                            WHERE nv.MaDayTro = d.MaDayTro
                              AND nv.ChucVu = N'Quản lý'
                              AND nv.TrangThaiLamViec = N'Đang làm'
                              AND nd.TrangThai = N'Hoạt động'
                        )
                        THEN NULL
                        ELSE d.TenDayTro
                    END
                FROM dbo.DayTro AS d
                WHERE d.MaDayTro = @MaDayTro
                  AND d.MaChuTro = @MaChuTro;";

            command.Parameters.Add(
                "@MaDayTro",
                SqlDbType.VarChar,
                20).Value = maDayTro;

            command.Parameters.Add(
                "@MaChuTro",
                SqlDbType.VarChar,
                20).Value = maChuTro;

            object? result = await command.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
                return null;

            return Convert.ToString(result);
        }

        private static async Task GhiThongBaoAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string maNguoiGui,
            string maNguoiNhan,
            string tieuDe,
            string noiDung)
        {
            string maThongBao =
                "TB" + Guid.NewGuid().ToString("N")[..18];

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText = @"
                    INSERT INTO dbo.ThongBao
                    (
                        MaThongBao,
                        MaNguoiGui,
                        TieuDe,
                        NoiDung
                    )
                    VALUES
                    (
                        @MaThongBao,
                        @MaNguoiGui,
                        @TieuDe,
                        @NoiDung
                    );";

                command.Parameters.Add(
                    "@MaThongBao",
                    SqlDbType.VarChar,
                    20).Value = maThongBao;

                command.Parameters.Add(
                    "@MaNguoiGui",
                    SqlDbType.VarChar,
                    20).Value = maNguoiGui;

                command.Parameters.Add(
                    "@TieuDe",
                    SqlDbType.NVarChar,
                    200).Value = tieuDe;

                command.Parameters.Add(
                    "@NoiDung",
                    SqlDbType.NVarChar,
                    -1).Value = noiDung;

                await command.ExecuteNonQueryAsync();
            }

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText = @"
                    INSERT INTO dbo.ThongBaoNguoiNhan
                    (
                        MaThongBao,
                        MaNguoiNhan,
                        DaDoc
                    )
                    VALUES
                    (
                        @MaThongBao,
                        @MaNguoiNhan,
                        0
                    );";

                command.Parameters.Add(
                    "@MaThongBao",
                    SqlDbType.VarChar,
                    20).Value = maThongBao;

                command.Parameters.Add(
                    "@MaNguoiNhan",
                    SqlDbType.VarChar,
                    20).Value = maNguoiNhan;

                await command.ExecuteNonQueryAsync();
            }
        }

        private static void KiemTraMa(
            string ma,
            string tenTruong)
        {
            if (string.IsNullOrWhiteSpace(ma) || ma.Length > 20)
            {
                throw new InvalidOperationException(
                    tenTruong + " không hợp lệ.");
            }
        }

        private static string LayMaChuTro()
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException(
                    "Bạn chưa đăng nhập.");

            if (user.VaiTro != "Chủ nhà trọ")
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền phân công nhân viên quản lý.");
            }

            return user.MaNguoiDung;
        }
    }
}
