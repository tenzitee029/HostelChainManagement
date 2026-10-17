using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Admin
{
    public static class QuanLyTaiKhoanService
    {
        // Dùng cùng mảng vai trò hiện có, không tạo bản dữ liệu riêng.
        public static readonly string[] Roles = AdminService.Roles;

        public static Task<DataTable> GetAccountsAsync()
        {
            return AdminService.QueryAsync(@"
                SELECT
                    MaNguoiDung,
                    TenDangNhap,
                    HoTen,
                    Email,
                    SoDienThoai,
                    VaiTro,
                    TrangThai
                FROM dbo.NguoiDung
                ORDER BY NgayTao DESC, TenDangNhap;");
        }

        public static Task<DataTable> GetDaysAsync()
        {
            return AdminService.GetDaysAsync();
        }

        public static async Task SetStatusAsync(
            string id,
            string expected,
            string next)
        {
            if (next is not ("Hoạt động" or "Khóa"))
            {
                throw new InvalidOperationException(
                    "Trạng thái không hợp lệ.");
            }

            if (id == AdminService.UserId())
            {
                throw new InvalidOperationException(
                    "Không thể tự vô hiệu hóa tài khoản "
                    + "quản trị đang đăng nhập.");
            }

            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            await AdminService.LockAccountsAsync(
                connection,
                transaction);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = @"
                IF @Next <> N'Hoạt động'
                    AND EXISTS
                    (
                        SELECT 1
                        FROM dbo.NguoiDung
                        WHERE MaNguoiDung = @Id
                          AND VaiTro = N'Admin'
                          AND TrangThai = N'Hoạt động'
                    )
                    AND
                    (
                        SELECT COUNT(*)
                        FROM dbo.NguoiDung
                        WHERE VaiTro = N'Admin'
                          AND TrangThai = N'Hoạt động'
                    ) <= 1
                    THROW 50001,
                        N'Không thể khóa Admin hoạt động cuối cùng.',
                        1;

                UPDATE dbo.NguoiDung
                SET TrangThai = @Next
                WHERE MaNguoiDung = @Id
                  AND TrangThai = @Expected;";

            command.Parameters.Add(
                "@Id", SqlDbType.VarChar, 20).Value = id;

            command.Parameters.Add(
                "@Expected", SqlDbType.NVarChar, 30)
                .Value = expected;

            command.Parameters.Add(
                "@Next", SqlDbType.NVarChar, 30)
                .Value = next;

            if (await command.ExecuteNonQueryAsync() != 1)
            {
                throw new InvalidOperationException(
                    "Tài khoản đã thay đổi. "
                    + "Vui lòng tải lại danh sách.");
            }

            await AdminService.AuditAsync(
                connection,
                transaction,
                $"Đổi trạng thái tài khoản {id}: "
                    + $"{expected} → {next}.");

            transaction.Commit();
        }

        public static async Task DeleteAccountAsync(
            string id,
            string expectedStatus)
        {
            string adminId = AdminService.UserId();

            if (id == adminId)
            {
                throw new InvalidOperationException(
                    "Không thể xóa tài khoản Admin đang đăng nhập.");
            }

            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction(
                IsolationLevel.Serializable);

            await AdminService.LockAccountsAsync(
                connection,
                transaction);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.Parameters.Add(
                "@Id",
                SqlDbType.VarChar,
                20).Value = id;

            command.Parameters.Add(
                "@ExpectedStatus",
                SqlDbType.NVarChar,
                30).Value = expectedStatus;

            command.CommandText = @"
                SELECT TenDangNhap
                FROM dbo.NguoiDung WITH (UPDLOCK, HOLDLOCK)
                WHERE MaNguoiDung = @Id
                  AND TrangThai = @ExpectedStatus;";

            string? login =
                await command.ExecuteScalarAsync() as string;

            if (login == null)
            {
                throw new InvalidOperationException(
                    "Tài khoản đã thay đổi hoặc không còn tồn tại. "
                    + "Vui lòng tải lại danh sách.");
            }

            command.CommandText = @"
                IF EXISTS
                (
                    SELECT 1 FROM dbo.NguoiDung
                    WHERE MaNguoiDung = @Id
                      AND VaiTro = N'Admin'
                      AND TrangThai = N'Hoạt động'
                )
                AND
                (
                    SELECT COUNT(*) FROM dbo.NguoiDung
                    WHERE VaiTro = N'Admin'
                      AND TrangThai = N'Hoạt động'
                ) <= 1
                    THROW 50001,
                        N'Không thể xóa Admin hoạt động cuối cùng.',
                        1;

                IF EXISTS
                (
                    SELECT 1 FROM dbo.DayTro
                    WHERE MaChuTro = @Id
                )
                    THROW 50001,
                        N'Không thể xóa: tài khoản đang liên kết với dãy trọ sở hữu.',
                        1;

                IF EXISTS
                (
                    SELECT 1 FROM dbo.NhanVien
                    WHERE MaNguoiDung = @Id
                      AND ChucVu = N'Quản lý'
                      AND MaDayTro IS NOT NULL
                      AND TrangThaiLamViec = N'Đang làm'
                )
                    THROW 50001,
                        N'Cần bàn giao dãy trọ đang quản lý trước khi xóa tài khoản.',
                        1;

                IF EXISTS
                (
                    SELECT 1
                    FROM dbo.HopDongThue hd
                    JOIN dbo.KhachThue kt
                        ON kt.MaKhachThue = hd.MaKhachThue
                    WHERE kt.MaNguoiDung = @Id
                )
                    THROW 50001,
                        N'Không thể xóa: tài khoản có lịch sử hợp đồng thuê.',
                        1;

                IF EXISTS
                (
                    SELECT 1
                    FROM dbo.ChiTietSuaChua ct
                    JOIN dbo.NhanVien nv
                        ON nv.MaNhanVien = ct.MaNhanVien
                    WHERE nv.MaNguoiDung = @Id
                )
                    THROW 50001,
                        N'Không thể xóa: tài khoản có lịch sử công việc sửa chữa.',
                        1;

                IF EXISTS
                (
                    SELECT 1 FROM dbo.PhieuSuCo
                    WHERE MaNguoiGui = @Id
                )
                    THROW 50001,
                        N'Không thể xóa: tài khoản đã gửi phiếu sự cố.',
                        1;

                IF EXISTS
                (
                    SELECT 1 FROM dbo.ThanhToan
                    WHERE MaNguoiXacNhan = @Id
                )
                    THROW 50001,
                        N'Không thể xóa: tài khoản có lịch sử xác nhận thanh toán.',
                        1;

                IF EXISTS
                (
                    SELECT 1 FROM dbo.ThongBao
                    WHERE MaNguoiGui = @Id
                )
                    THROW 50001,
                        N'Không thể xóa: tài khoản có lịch sử gửi thông báo.',
                        1;

                DELETE FROM dbo.MaOtp
                WHERE MaNguoiDung = @Id;

                DELETE FROM dbo.ThongBaoNguoiNhan
                WHERE MaNguoiNhan = @Id;

                -- Giữ nhật ký, bỏ liên kết đến tài khoản được xóa.
                UPDATE dbo.NhatKyHeThong
                SET MaNguoiDung = NULL
                WHERE MaNguoiDung = @Id;

                DELETE FROM dbo.NhanVien
                WHERE MaNguoiDung = @Id;

                DELETE FROM dbo.KhachThue
                WHERE MaNguoiDung = @Id;

                DELETE FROM dbo.NguoiDung
                WHERE MaNguoiDung = @Id;

                IF @@ROWCOUNT <> 1
                    THROW 50001,
                        N'Không xóa được tài khoản. Vui lòng tải lại.',
                        1;";

            await command.ExecuteNonQueryAsync();

            await AdminService.AuditAsync(
                connection,
                transaction,
                $"Xóa vĩnh viễn tài khoản {login}, mã {id}.");

            transaction.Commit();
        }

        public static async Task SetRoleAsync(
            string id,
            string expected,
            string next,
            string? day)
        {
            if (!Roles.Contains(next))
            {
                throw new InvalidOperationException(
                    "Vai trò không hợp lệ.");
            }

            if (id == AdminService.UserId() && next != "Admin")
            {
                throw new InvalidOperationException(
                    "Không thể tự bỏ quyền Admin đang đăng nhập.");
            }

            if (expected == next)
                return;

            if (next == "Nhân viên quản lý"
                && string.IsNullOrWhiteSpace(day))
            {
                throw new InvalidOperationException(
                    "Chọn dãy trọ cho nhân viên quản lý.");
            }

            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            await AdminService.LockAccountsAsync(
                connection,
                transaction);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = @"
                IF @Next <> N'Chủ nhà trọ'
                    AND EXISTS
                    (
                        SELECT 1
                        FROM dbo.DayTro
                        WHERE MaChuTro = @Id
                    )
                    THROW 50001,
                        N'Cần bàn giao các dãy trọ trước khi đổi vai trò Chủ nhà trọ.',
                        1;

                IF @Next <> N'Nhân viên quản lý'
                    AND EXISTS
                    (
                        SELECT 1
                        FROM dbo.NhanVien
                        WHERE MaNguoiDung = @Id
                          AND ChucVu = N'Quản lý'
                          AND MaDayTro IS NOT NULL
                          AND TrangThaiLamViec = N'Đang làm'
                    )
                    THROW 50001,
                        N'Cần bàn giao dãy trọ đang quản lý trước khi đổi vai trò.',
                        1;

                IF @Next <> N'Nhân viên kỹ thuật'
                    AND EXISTS
                    (
                        SELECT 1
                        FROM dbo.ChiTietSuaChua ct
                        JOIN dbo.NhanVien nv
                            ON nv.MaNhanVien = ct.MaNhanVien
                        WHERE nv.MaNguoiDung = @Id
                          AND ct.TrangThai <> N'Hoàn thành'
                    )
                    THROW 50001,
                        N'Cần hoàn tất hoặc bàn giao công việc kỹ thuật trước khi đổi vai trò.',
                        1;

                IF @Next <> N'Khách thuê'
                    AND EXISTS
                    (
                        SELECT 1
                        FROM dbo.HopDongThue hd
                        JOIN dbo.KhachThue kt
                            ON kt.MaKhachThue = hd.MaKhachThue
                        WHERE kt.MaNguoiDung = @Id
                          AND hd.TrangThai = N'Hiệu lực'
                    )
                    THROW 50001,
                        N'Cần xử lý hợp đồng đang hiệu lực trước khi đổi vai trò Khách thuê.',
                        1;

                IF @Expected = N'Admin'
                    AND EXISTS
                    (
                        SELECT 1
                        FROM dbo.NguoiDung
                        WHERE MaNguoiDung = @Id
                          AND TrangThai = N'Hoạt động'
                    )
                    AND
                    (
                        SELECT COUNT(*)
                        FROM dbo.NguoiDung
                        WHERE VaiTro = N'Admin'
                          AND TrangThai = N'Hoạt động'
                    ) <= 1
                    THROW 50001,
                        N'Không thể bỏ quyền Admin hoạt động cuối cùng.',
                        1;

                UPDATE dbo.NguoiDung
                SET VaiTro = @Next
                WHERE MaNguoiDung = @Id
                  AND VaiTro = @Expected;";

            command.Parameters.Add(
                "@Id", SqlDbType.VarChar, 20).Value = id;

            command.Parameters.Add(
                "@Expected", SqlDbType.NVarChar, 50)
                .Value = expected;

            command.Parameters.Add(
                "@Next", SqlDbType.NVarChar, 50)
                .Value = next;

            if (await command.ExecuteNonQueryAsync() != 1)
            {
                throw new InvalidOperationException(
                    "Tài khoản đã thay đổi. Hãy tải lại.");
            }

            command.CommandText = @"
                UPDATE dbo.NhanVien
                SET TrangThaiLamViec = N'Tạm nghỉ'
                WHERE MaNguoiDung = @Id
                  AND @Next NOT IN
                  (
                      N'Nhân viên quản lý',
                      N'Nhân viên kỹ thuật'
                  );

                UPDATE dbo.KhachThue
                SET TrangThai = N'Ngừng hoạt động'
                WHERE MaNguoiDung = @Id
                  AND @Next <> N'Khách thuê';";

            await command.ExecuteNonQueryAsync();

            await AdminService.AddProfileAsync(
                connection,
                transaction,
                id,
                next,
                day);

            await AdminService.AuditAsync(
                connection,
                transaction,
                $"Đổi vai trò tài khoản {id}: "
                    + $"{expected} → {next}.");

            transaction.Commit();
        }
    }
}