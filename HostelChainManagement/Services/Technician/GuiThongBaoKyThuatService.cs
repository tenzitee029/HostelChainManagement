using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Services.Technician
{
    public static class GuiThongBaoKyThuatService
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
                "@User", SqlDbType.VarChar, 20)
                .Value = uid;

            command.Parameters.Add(
                "@Id", SqlDbType.VarChar, 20)
                .Value = id;

            int count = Convert.ToInt32(
                await command.ExecuteScalarAsync());

            if (count != 1)
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền gửi thông báo "
                    + "cho công việc này.");
            }

            await XuLyCongViecService.NotifyAsync(
                connection,
                transaction,
                uid,
                id,
                title,
                body);

            transaction.Commit();
        }
    }
}