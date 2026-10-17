using System;
using System.Data;
using System.Security.Cryptography;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Owner
{
    public static class NhanVienFormService
    {
        public sealed record TaiKhoanNhanVienMoi(
            string TenDangNhap,
            string MatKhau,
            string? MaDayTroTuDongGan = null);

        public static async Task<TaiKhoanNhanVienMoi?> LuuNhanVienAsync(
            string? maNhanVien,
            string hoTen,
            string soDienThoai,
            string cccd,
            string chucVu)
        {
            var user = GetChuTro();

            bool themMoi = string.IsNullOrWhiteSpace(maNhanVien);

            hoTen = hoTen.Trim();
            soDienThoai = soDienThoai.Trim();
            cccd = cccd.Trim();

            if (hoTen.Length == 0 || hoTen.Length > 100)
            {
                throw new InvalidOperationException(
                    "Họ tên phải có từ 1 đến 100 ký tự.");
            }

            if ((soDienThoai.Length != 10
                    && soDienThoai.Length != 11)
                || !LaChuoiSo(soDienThoai))
            {
                throw new InvalidOperationException(
                    "Số điện thoại phải gồm 10 hoặc 11 chữ số.");
            }

            if (cccd.Length != 12 || !LaChuoiSo(cccd))
            {
                throw new InvalidOperationException(
                    "CCCD phải gồm đúng 12 chữ số.");
            }

            if (chucVu != "Quản lý" && chucVu != "Kỹ thuật")
            {
                throw new InvalidOperationException(
                    "Vui lòng chọn vai trò chuyên môn hợp lệ.");
            }

            string maNhanVienMoi = "";

            string maNguoiDungMoi =
                "ND" + Guid.NewGuid().ToString("N")[..18];

            string tenDangNhapMoi =
                "nv_" + Guid.NewGuid().ToString("N")[..12];

            string matKhauTam = TaoMatKhauTam();

            string vaiTro = chucVu == "Quản lý"
                ? "Nhân viên quản lý"
                : "Nhân viên kỹ thuật";

            using var connection = Database.CreateConnection();

            await connection.OpenAsync();

            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            try
            {
                bool coBangLienKet =
                    await SoDoChuTroService.CoBangLienKetAsync(
                        connection, transaction);

                if (themMoi && !coBangLienKet &&
                    !await SoDoChuTroService.LaChuTroDuyNhatAsync(
                        connection, user.MaNguoiDung, transaction))
                {
                    throw new InvalidOperationException(
                        "Database không có bảng ChuTroNhanVien và đang có nhiều " +
                        "Chủ trọ. Không thể tạo nhân viên chưa phân công an toàn.");
                }

                using (var checkOwner = connection.CreateCommand())
                {
                    checkOwner.Transaction = transaction;

                    checkOwner.CommandText = @"
                        SELECT COUNT(*)
                        FROM dbo.NguoiDung WITH (UPDLOCK, HOLDLOCK)
                        WHERE MaNguoiDung = @MaChuTro
                          AND VaiTro = N'Chủ nhà trọ'
                          AND TrangThai = N'Hoạt động';";

                    checkOwner.Parameters.Add(
                        "@MaChuTro",
                        SqlDbType.VarChar,
                        20).Value = user.MaNguoiDung;

                    int soChuTro = Convert.ToInt32(
                        await checkOwner.ExecuteScalarAsync());

                    if (soChuTro != 1)
                    {
                        throw new InvalidOperationException(
                            "Tài khoản Chủ nhà trọ không còn hoạt động.");
                    }
                }

                string maNguoiDung = "";

                if (!themMoi)
                {
                    using var checkNhanVien = connection.CreateCommand();

                    checkNhanVien.Transaction = transaction;

                    checkNhanVien.CommandText = @"
                        SELECT nv.MaNguoiDung
                        FROM dbo.NhanVien AS nv WITH (UPDLOCK, HOLDLOCK)
                        INNER JOIN dbo.ChuTroNhanVien AS ct
                            ON ct.MaNhanVien = nv.MaNhanVien
                        INNER JOIN dbo.NguoiDung AS nd
                            ON nd.MaNguoiDung = nv.MaNguoiDung
                        WHERE nv.MaNhanVien = @MaNhanVien
                          AND ct.MaChuTro = @MaChuTro
                          AND nd.VaiTro IN
                          (
                              N'Nhân viên quản lý',
                              N'Nhân viên kỹ thuật'
                          );";

                    if (!coBangLienKet)
                    {
                        bool motChuTro =
                            await SoDoChuTroService.LaChuTroDuyNhatAsync(
                                connection, user.MaNguoiDung, transaction);

                        checkNhanVien.CommandText = @"
                            SELECT nv.MaNguoiDung
                            FROM dbo.NhanVien AS nv WITH (UPDLOCK, HOLDLOCK)
                            LEFT JOIN dbo.DayTro AS d
                                ON d.MaDayTro = nv.MaDayTro
                            INNER JOIN dbo.NguoiDung AS nd
                                ON nd.MaNguoiDung = nv.MaNguoiDung
                            WHERE nv.MaNhanVien = @MaNhanVien
                              AND
                              (
                                  d.MaChuTro = @MaChuTro
                                  OR (@MotChuTro = 1 AND nv.MaDayTro IS NULL)
                              )
                              AND nd.VaiTro IN
                              (
                                  N'Nhân viên quản lý',
                                  N'Nhân viên kỹ thuật'
                              );";

                        checkNhanVien.Parameters.Add(
                            "@MotChuTro", SqlDbType.Bit)
                            .Value = motChuTro;
                    }

                    checkNhanVien.Parameters.Add(
                        "@MaNhanVien",
                        SqlDbType.VarChar,
                        20).Value = maNhanVien!.Trim();

                    checkNhanVien.Parameters.Add(
                        "@MaChuTro",
                        SqlDbType.VarChar,
                        20).Value = user.MaNguoiDung;

                    object? result =
                        await checkNhanVien.ExecuteScalarAsync();

                    if (result == null || result == DBNull.Value)
                    {
                        throw new InvalidOperationException(
                            "Nhân viên không tồn tại hoặc không thuộc quyền quản lý của bạn.");
                    }

                    maNguoiDung = Convert.ToString(result) ?? "";
                }

                using (var checkDuplicate = connection.CreateCommand())
                {
                    checkDuplicate.Transaction = transaction;

                    checkDuplicate.CommandText = @"
                        SELECT COUNT(*)
                        FROM dbo.NguoiDung WITH (UPDLOCK, HOLDLOCK)
                        WHERE MaNguoiDung <> @MaNguoiDung
                          AND
                          (
                              SoDienThoai = @SoDienThoai
                              OR CCCD = @CCCD
                          );";

                    checkDuplicate.Parameters.Add(
                        "@MaNguoiDung",
                        SqlDbType.VarChar,
                        20).Value = maNguoiDung;

                    checkDuplicate.Parameters.Add(
                        "@SoDienThoai",
                        SqlDbType.VarChar,
                        15).Value = soDienThoai;

                    checkDuplicate.Parameters.Add(
                        "@CCCD",
                        SqlDbType.VarChar,
                        12).Value = cccd;

                    int soTrung = Convert.ToInt32(
                        await checkDuplicate.ExecuteScalarAsync());

                    if (soTrung > 0)
                    {
                        throw new InvalidOperationException(
                            "Số điện thoại hoặc số CCCD này đã tồn tại trong hệ thống.");
                    }
                }

                if (themMoi)
                {
                    maNhanVienMoi = await TaoMaNhanVienAsync(
                        connection,
                        transaction);

                    using (var insertUser = connection.CreateCommand())
                    {
                        insertUser.Transaction = transaction;

                        insertUser.CommandText = @"
                            INSERT INTO dbo.NguoiDung
                            (
                                MaNguoiDung,
                                TenDangNhap,
                                MatKhau,
                                HoTen,
                                CCCD,
                                SoDienThoai,
                                VaiTro,
                                TrangThai
                            )
                            VALUES
                            (
                                @MaNguoiDung,
                                @TenDangNhap,
                                @MatKhau,
                                @HoTen,
                                @CCCD,
                                @SoDienThoai,
                                @VaiTro,
                                N'Hoạt động'
                            );";

                        insertUser.Parameters.Add(
                            "@MaNguoiDung",
                            SqlDbType.VarChar,
                            20).Value = maNguoiDungMoi;

                        insertUser.Parameters.Add(
                            "@TenDangNhap",
                            SqlDbType.VarChar,
                            50).Value = tenDangNhapMoi;

                        insertUser.Parameters.Add(
                            "@MatKhau",
                            SqlDbType.VarChar,
                            255).Value = matKhauTam;

                        insertUser.Parameters.Add(
                            "@HoTen",
                            SqlDbType.NVarChar,
                            100).Value = hoTen;

                        insertUser.Parameters.Add(
                            "@CCCD",
                            SqlDbType.VarChar,
                            12).Value = cccd;

                        insertUser.Parameters.Add(
                            "@SoDienThoai",
                            SqlDbType.VarChar,
                            15).Value = soDienThoai;

                        insertUser.Parameters.Add(
                            "@VaiTro",
                            SqlDbType.NVarChar,
                            50).Value = vaiTro;

                        await insertUser.ExecuteNonQueryAsync();
                    }

                    using (var insertNhanVien = connection.CreateCommand())
                    {
                        insertNhanVien.Transaction = transaction;

                        insertNhanVien.CommandText = @"
                            INSERT INTO dbo.NhanVien
                            (
                                MaNhanVien,
                                MaNguoiDung,
                                MaDayTro,
                                ChucVu,
                                TrangThaiLamViec
                            )
                            VALUES
                            (
                                @MaNhanVien,
                                @MaNguoiDung,
                                @MaDayTro,
                                @ChucVu,
                                N'Đang làm'
                            );";

                        insertNhanVien.Parameters.Add(
                            "@MaNhanVien",
                            SqlDbType.VarChar,
                            20).Value = maNhanVienMoi;

                        insertNhanVien.Parameters.Add(
                            "@MaNguoiDung",
                            SqlDbType.VarChar,
                            20).Value = maNguoiDungMoi;

                        insertNhanVien.Parameters.Add(
                            "@MaDayTro",
                            SqlDbType.VarChar,
                            20).Value = DBNull.Value;

                        insertNhanVien.Parameters.Add(
                            "@ChucVu",
                            SqlDbType.NVarChar,
                            50).Value = chucVu;

                        await insertNhanVien.ExecuteNonQueryAsync();
                    }

                    if (coBangLienKet)
                    {
                        using var insertLink = connection.CreateCommand();
                        insertLink.Transaction = transaction;

                        insertLink.CommandText = @"
                            INSERT INTO dbo.ChuTroNhanVien
                            (
                                MaNhanVien,
                                MaChuTro
                            )
                            VALUES
                            (
                                @MaNhanVien,
                                @MaChuTro
                            );";

                        insertLink.Parameters.Add(
                            "@MaNhanVien",
                            SqlDbType.VarChar,
                            20).Value = maNhanVienMoi;

                        insertLink.Parameters.Add(
                            "@MaChuTro",
                            SqlDbType.VarChar,
                            20).Value = user.MaNguoiDung;

                        await insertLink.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    using (var updateUser = connection.CreateCommand())
                    {
                        updateUser.Transaction = transaction;

                        updateUser.CommandText = @"
                            UPDATE dbo.NguoiDung
                            SET HoTen = @HoTen,
                                SoDienThoai = @SoDienThoai,
                                CCCD = @CCCD,
                                VaiTro = @VaiTro
                            WHERE MaNguoiDung = @MaNguoiDung
                              AND VaiTro IN
                              (
                                  N'Nhân viên quản lý',
                                  N'Nhân viên kỹ thuật'
                              );";

                        updateUser.Parameters.Add(
                            "@MaNguoiDung",
                            SqlDbType.VarChar,
                            20).Value = maNguoiDung;

                        updateUser.Parameters.Add(
                            "@HoTen",
                            SqlDbType.NVarChar,
                            100).Value = hoTen;

                        updateUser.Parameters.Add(
                            "@SoDienThoai",
                            SqlDbType.VarChar,
                            15).Value = soDienThoai;

                        updateUser.Parameters.Add(
                            "@CCCD",
                            SqlDbType.VarChar,
                            12).Value = cccd;

                        updateUser.Parameters.Add(
                            "@VaiTro",
                            SqlDbType.NVarChar,
                            50).Value = vaiTro;

                        int soDong =
                            await updateUser.ExecuteNonQueryAsync();

                        if (soDong != 1)
                        {
                            throw new InvalidOperationException(
                                "Không thể cập nhật tài khoản nhân viên.");
                        }
                    }

                    using (var updateNhanVien = connection.CreateCommand())
                    {
                        updateNhanVien.Transaction = transaction;

                        updateNhanVien.CommandText = @"
                            UPDATE dbo.NhanVien
                            SET ChucVu = @ChucVu
                            WHERE MaNhanVien = @MaNhanVien
                              AND MaNguoiDung = @MaNguoiDung;";

                        updateNhanVien.Parameters.Add(
                            "@MaNhanVien",
                            SqlDbType.VarChar,
                            20).Value = maNhanVien!.Trim();

                        updateNhanVien.Parameters.Add(
                            "@MaNguoiDung",
                            SqlDbType.VarChar,
                            20).Value = maNguoiDung;

                        updateNhanVien.Parameters.Add(
                            "@ChucVu",
                            SqlDbType.NVarChar,
                            50).Value = chucVu;

                        int soDong =
                            await updateNhanVien.ExecuteNonQueryAsync();

                        if (soDong != 1)
                        {
                            throw new InvalidOperationException(
                                "Không thể cập nhật hồ sơ nhân viên.");
                        }
                    }
                }

                transaction.Commit();

                return themMoi
                    ? new TaiKhoanNhanVienMoi(
                        tenDangNhapMoi,
                        matKhauTam)
                    : null;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static async Task<string> TaoMaNhanVienAsync(
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
                            SUBSTRING(MaNhanVien, 3, 6)
                        )
                    ),
                    0
                )
                FROM dbo.NhanVien WITH (TABLOCKX, HOLDLOCK)
                WHERE MaNhanVien LIKE
                    'NV[0-9][0-9][0-9][0-9][0-9][0-9]'
                  AND LEN(MaNhanVien) = 8;";

            int soLonNhat = Convert.ToInt32(
                await command.ExecuteScalarAsync());

            int soTiepTheo = soLonNhat + 1;

            if (soTiepTheo > 999999)
            {
                throw new InvalidOperationException(
                    "Đã hết mã nhân viên trong khoảng 6 chữ số.");
            }

            return $"NV{soTiepTheo:D6}";
        }

        private static bool LaChuoiSo(string giaTri)
        {
            foreach (char kyTu in giaTri)
            {
                if (kyTu < '0' || kyTu > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private static string TaoMatKhauTam()
        {
            const string kyTu =
                "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

            char[] ketQua = new char[16];

            ketQua[0] = 'N';
            ketQua[1] = 'v';
            ketQua[2] = '@';
            ketQua[3] = '1';

            for (int i = 4; i < ketQua.Length; i++)
            {
                int viTri = RandomNumberGenerator.GetInt32(
                    kyTu.Length);

                ketQua[i] = kyTu[viTri];
            }

            return new string(ketQua);
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