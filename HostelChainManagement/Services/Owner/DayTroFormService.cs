using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Owner
{
    public static class DayTroFormService
    {
        public static async Task<string> LuuDayTroAsync(
            string? maDayTro,
            string tenDayTro,
            string diaChi,
            string? moTa,
            string? quyDinh,
            string trangThai)
        {
            var user = GetChuTro();

            bool themMoi = string.IsNullOrWhiteSpace(maDayTro);

            string maDay = themMoi
                ? ""
                : maDayTro!.Trim();

            tenDayTro = tenDayTro.Trim();
            diaChi = diaChi.Trim();
            moTa = moTa?.Trim();
            quyDinh = quyDinh?.Trim();

            if (string.IsNullOrWhiteSpace(tenDayTro))
            {
                throw new InvalidOperationException(
                    "Vui lòng nhập tên dãy trọ.");
            }

            if (string.IsNullOrWhiteSpace(diaChi))
            {
                throw new InvalidOperationException(
                    "Vui lòng nhập địa chỉ dãy trọ.");
            }

            if (tenDayTro.Length > 100)
            {
                throw new InvalidOperationException(
                    "Tên dãy trọ không được quá 100 ký tự.");
            }

            if (diaChi.Length > 255)
            {
                throw new InvalidOperationException(
                    "Địa chỉ không được quá 255 ký tự.");
            }

            if (moTa != null && moTa.Length > 500)
            {
                throw new InvalidOperationException(
                    "Mô tả không được quá 500 ký tự.");
            }

            if (!themMoi && maDay.Length > 20)
            {
                throw new InvalidOperationException(
                    "Mã dãy trọ không hợp lệ.");
            }

            if (trangThai != "Đang hoạt động"
                && trangThai != "Tạm ngưng"
                && trangThai != "Ngừng hoạt động")
            {
                throw new InvalidOperationException(
                    "Vui lòng chọn trạng thái hợp lệ.");
            }

            using var connection = Database.CreateConnection();

            await connection.OpenAsync();

            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            try
            {
                if (themMoi)
                {
                    maDay = await TaoMaDayTroAsync(
                        connection,
                        transaction);
                }
                else
                {
                    using var checkOwner = connection.CreateCommand();

                    checkOwner.Transaction = transaction;

                    checkOwner.CommandText = @"
                        SELECT COUNT(*)
                        FROM dbo.DayTro WITH (UPDLOCK, HOLDLOCK)
                        WHERE MaDayTro = @MaDayTro
                          AND MaChuTro = @MaChuTro;";

                    checkOwner.Parameters.Add(
                        "@MaDayTro",
                        SqlDbType.VarChar,
                        20).Value = maDay;

                    checkOwner.Parameters.Add(
                        "@MaChuTro",
                        SqlDbType.VarChar,
                        20).Value = user.MaNguoiDung;

                    int soDay = Convert.ToInt32(
                        await checkOwner.ExecuteScalarAsync());

                    if (soDay == 0)
                    {
                        throw new InvalidOperationException(
                            "Dãy trọ không tồn tại hoặc không thuộc quyền quản lý của bạn.");
                    }
                }

                using (var checkDuplicate = connection.CreateCommand())
                {
                    checkDuplicate.Transaction = transaction;

                    checkDuplicate.CommandText = @"
                        SELECT COUNT(*)
                        FROM dbo.DayTro WITH (UPDLOCK, HOLDLOCK)
                        WHERE MaChuTro = @MaChuTro
                          AND TenDayTro = @TenDayTro
                          AND DiaChi = @DiaChi
                          AND MaDayTro <> @MaDayTro;";

                    checkDuplicate.Parameters.Add(
                        "@MaChuTro",
                        SqlDbType.VarChar,
                        20).Value = user.MaNguoiDung;

                    checkDuplicate.Parameters.Add(
                        "@TenDayTro",
                        SqlDbType.NVarChar,
                        100).Value = tenDayTro;

                    checkDuplicate.Parameters.Add(
                        "@DiaChi",
                        SqlDbType.NVarChar,
                        255).Value = diaChi;

                    checkDuplicate.Parameters.Add(
                        "@MaDayTro",
                        SqlDbType.VarChar,
                        20).Value = maDay;

                    int soTrung = Convert.ToInt32(
                        await checkDuplicate.ExecuteScalarAsync());

                    if (soTrung > 0)
                    {
                        throw new InvalidOperationException(
                            "Đã tồn tại dãy trọ có cùng tên và địa chỉ.");
                    }
                }

                if (!themMoi && trangThai == "Ngừng hoạt động")
                {
                    using var checkContract = connection.CreateCommand();

                    checkContract.Transaction = transaction;

                    checkContract.CommandText = @"
                        SELECT COUNT(*)
                        FROM dbo.HopDongThue AS h
                        INNER JOIN dbo.PhongTro AS p
                            ON p.MaPhong = h.MaPhong
                        WHERE p.MaDayTro = @MaDayTro
                          AND h.TrangThai = N'Hiệu lực';";

                    checkContract.Parameters.Add(
                        "@MaDayTro",
                        SqlDbType.VarChar,
                        20).Value = maDay;

                    int soHopDong = Convert.ToInt32(
                        await checkContract.ExecuteScalarAsync());

                    if (soHopDong > 0)
                    {
                        throw new InvalidOperationException(
                            "Không thể ngừng hoạt động dãy trọ vì vẫn còn hợp đồng hiệu lực.");
                    }
                }

                using var command = connection.CreateCommand();

                command.Transaction = transaction;

                if (themMoi)
                {
                    command.CommandText = @"
                        INSERT INTO dbo.DayTro
                        (
                            MaDayTro,
                            MaChuTro,
                            TenDayTro,
                            DiaChi,
                            MoTa,
                            QuyDinh,
                            TrangThai
                        )
                        VALUES
                        (
                            @MaDayTro,
                            @MaChuTro,
                            @TenDayTro,
                            @DiaChi,
                            @MoTa,
                            @QuyDinh,
                            @TrangThai
                        );";
                }
                else
                {
                    command.CommandText = @"
                        UPDATE dbo.DayTro
                        SET TenDayTro = @TenDayTro,
                            DiaChi = @DiaChi,
                            MoTa = @MoTa,
                            QuyDinh = @QuyDinh,
                            TrangThai = @TrangThai
                        WHERE MaDayTro = @MaDayTro
                          AND MaChuTro = @MaChuTro;";
                }

                command.Parameters.Add(
                    "@MaDayTro",
                    SqlDbType.VarChar,
                    20).Value = maDay;

                command.Parameters.Add(
                    "@MaChuTro",
                    SqlDbType.VarChar,
                    20).Value = user.MaNguoiDung;

                command.Parameters.Add(
                    "@TenDayTro",
                    SqlDbType.NVarChar,
                    100).Value = tenDayTro;

                command.Parameters.Add(
                    "@DiaChi",
                    SqlDbType.NVarChar,
                    255).Value = diaChi;

                command.Parameters.Add(
                    "@MoTa",
                    SqlDbType.NVarChar,
                    500).Value = string.IsNullOrWhiteSpace(moTa)
                        ? DBNull.Value
                        : moTa;

                command.Parameters.Add(
                    "@QuyDinh",
                    SqlDbType.NVarChar,
                    -1).Value = string.IsNullOrWhiteSpace(quyDinh)
                        ? DBNull.Value
                        : quyDinh;

                command.Parameters.Add(
                    "@TrangThai",
                    SqlDbType.NVarChar,
                    30).Value = trangThai;

                int soDong = await command.ExecuteNonQueryAsync();

                if (soDong != 1)
                {
                    throw new InvalidOperationException(
                        "Không thể lưu thông tin dãy trọ.");
                }

                if (!themMoi && trangThai != "Đang hoạt động")
                {
                    bool coBangLienKet =
                        await SoDoChuTroService.CoBangLienKetAsync(
                            connection, transaction);

                    if (!coBangLienKet &&
                        !await SoDoChuTroService.LaChuTroDuyNhatAsync(
                            connection, user.MaNguoiDung, transaction))
                    {
                        throw new InvalidOperationException(
                            "Không thể tự động hủy phân công khi database " +
                            "có nhiều Chủ trọ mà thiếu bảng ChuTroNhanVien.");
                    }

                    using var huyPhanCong = connection.CreateCommand();
                    huyPhanCong.Transaction = transaction;

                    huyPhanCong.CommandText = @"
                        UPDATE nv
                        SET MaDayTro = NULL
                        FROM dbo.NhanVien AS nv
                        INNER JOIN dbo.DayTro AS d
                            ON d.MaDayTro = nv.MaDayTro
                        WHERE d.MaDayTro = @MaDayTro
                          AND d.MaChuTro = @MaChuTro
                          AND d.TrangThai IN
                          (
                              N'Tạm ngưng',
                              N'Ngừng hoạt động'
                          );";

                    huyPhanCong.Parameters.Add(
                        "@MaDayTro",
                        SqlDbType.VarChar,
                        20).Value = maDay;

                    huyPhanCong.Parameters.Add(
                        "@MaChuTro",
                        SqlDbType.VarChar,
                        20).Value = user.MaNguoiDung;

                    await huyPhanCong.ExecuteNonQueryAsync();
                }

                transaction.Commit();

                return maDay;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static async Task<string> TaoMaDayTroAsync(
            Microsoft.Data.SqlClient.SqlConnection connection,
            Microsoft.Data.SqlClient.SqlTransaction transaction)
        {
            using var command = connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText = @"
                SELECT ISNULL(
                    MAX(
                        TRY_CONVERT(
                            INT,
                            SUBSTRING(MaDayTro, 3, 6)
                        )
                    ),
                    0
                )
                FROM dbo.DayTro WITH (TABLOCKX, HOLDLOCK)
                WHERE MaDayTro LIKE
                    'DT[0-9][0-9][0-9][0-9][0-9][0-9]'
                  AND LEN(MaDayTro) = 8;";

            int soLonNhat = Convert.ToInt32(
                await command.ExecuteScalarAsync());

            int soTiepTheo = soLonNhat + 1;

            if (soTiepTheo > 999999)
            {
                throw new InvalidOperationException(
                    "Đã hết mã dãy trọ trong khoảng 6 chữ số.");
            }

            return $"DT{soTiepTheo:D6}";
        }

        private static QuanLyChuoiNhaTro.Models.LoggedInUser GetChuTro()
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException(
                    "Bạn chưa đăng nhập.");

            if (user.VaiTro != "Chủ nhà trọ")
            {
                throw new InvalidOperationException(
                    "Chức năng chỉ dành cho Chủ nhà trọ.");
            }

            return user;
        }
    }
}