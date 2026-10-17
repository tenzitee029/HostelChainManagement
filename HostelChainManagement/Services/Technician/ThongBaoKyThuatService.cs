using System;
using System.Data;
using System.Threading.Tasks;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Services.Technician
{
    public static class ThongBaoKyThuatService
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

        public static async Task<DataTable> GetNotificationsAsync()
        {
            string uid = UserId();

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
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
                ORDER BY t.NgayGui DESC;";

            command.Parameters.Add(
                "@User",
                SqlDbType.VarChar,
                20).Value = uid;

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            var result = new DataTable();
            result.Load(reader);

            return result;
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
    }
}