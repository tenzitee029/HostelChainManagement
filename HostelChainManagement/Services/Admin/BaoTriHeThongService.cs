using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Services.Admin
{
    public static class BaoTriHeThongService
    {
        private static string UserId()
        {
            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Admin")
            {
                throw new InvalidOperationException(
                    "Chức năng chỉ dành cho Admin hệ thống.");
            }

            return user.MaNguoiDung;
        }

        private static async Task AuthorizeAsync(
            SqlConnection connection,
            SqlTransaction? transaction = null)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = @"
                SELECT COUNT(*)
                FROM dbo.NguoiDung
                WHERE MaNguoiDung = @User
                  AND VaiTro = N'Admin'
                  AND TrangThai = N'Hoạt động';";

            command.Parameters.Add(
                "@User", SqlDbType.VarChar, 20)
                .Value = UserId();

            int count = Convert.ToInt32(
                await command.ExecuteScalarAsync());

            if (count != 1)
            {
                throw new InvalidOperationException(
                    "Tài khoản Admin không còn hoạt động. "
                    + "Vui lòng đăng nhập lại.");
            }
        }

        private static void CheckBackupPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)
                || !Path.IsPathRooted(path)
                || Path.GetExtension(path).ToLowerInvariant()
                    != ".bak")
            {
                throw new InvalidOperationException(
                    "Nhập đường dẫn tuyệt đối tới file .bak "
                    + "trên máy chủ SQL Server.");
            }
        }

        private static string QuoteDatabase(string name)
        {
            return "[" + name.Replace("]", "]]") + "]";
        }

        public static async Task BackupAsync(string path)
        {
            CheckBackupPath(path);

            using var connection = Database.CreateConnection();

            await connection.OpenAsync();
            await AuthorizeAsync(connection);

            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT CONVERT(
                    int,
                    SERVERPROPERTY('EngineEdition'));";

            int edition = Convert.ToInt32(
                await command.ExecuteScalarAsync());

            command.CommandText =
                $"BACKUP DATABASE {QuoteDatabase(connection.Database)} "
                + "TO DISK = @Path "
                + "WITH COPY_ONLY, CHECKSUM"
                + (edition is 2 or 3
                    ? ", COMPRESSION;"
                    : ";");

            command.CommandTimeout = 600;

            command.Parameters.Add(
                "@Path", SqlDbType.NVarChar, 4000)
                .Value = path;

            await command.ExecuteNonQueryAsync();

            await SystemLogService.TryWriteAsync(
                UserId(),
                "Quản trị",
                "Sao lưu CSDL: " + path);
        }

        public static async Task<string> RestoreAsync(string path)
        {
            CheckBackupPath(path);

            var builder = new SqlConnectionStringBuilder(
                Database.CreateConnection().ConnectionString);

            string database;

            using (var connection = Database.CreateConnection())
            {
                await connection.OpenAsync();
                await AuthorizeAsync(connection);

                database = connection.Database;

                using var permission =
                    connection.CreateCommand();

                permission.CommandText = @"
                    SELECT IS_SRVROLEMEMBER('sysadmin');";

                if (Convert.ToInt32(
                    await permission.ExecuteScalarAsync()) != 1)
                {
                    throw new InvalidOperationException(
                        "Khôi phục cần kết nối SQL Server "
                        + "có quyền sysadmin.");
                }
            }

            builder.InitialCatalog = "master";
            builder.Pooling = false;

            using var master = new SqlConnection(
                builder.ConnectionString);

            await master.OpenAsync();

            using var command = master.CreateCommand();

            command.CommandTimeout = 600;

            command.Parameters.Add(
                "@Path", SqlDbType.NVarChar, 4000)
                .Value = path;

            command.CommandText =
                "RESTORE HEADERONLY FROM DISK = @Path;";

            int position = 0;

            using (var reader =
                await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    if (reader["DatabaseName"].ToString()
                            == database
                        && Convert.ToInt32(
                            reader["BackupType"]) == 1)
                    {
                        position = Math.Max(
                            position,
                            Convert.ToInt32(
                                reader["Position"]));
                    }
                }
            }

            if (position == 0)
            {
                throw new InvalidOperationException(
                    "Chỉ khôi phục bản sao lưu đầy đủ "
                    + "của đúng CSDL đang sử dụng.");
            }

            command.Parameters.Add(
                "@Position", SqlDbType.Int)
                .Value = position;

            command.CommandText = @"
                RESTORE VERIFYONLY
                FROM DISK = @Path
                WITH FILE = @Position, CHECKSUM;";

            await command.ExecuteNonQueryAsync();

            string safety = Path.Combine(
                Path.GetDirectoryName(path)!,
                "TruocKhoiPhuc_"
                    + DateTime.Now.ToString("yyyyMMdd_HHmmss")
                    + "_"
                    + Guid.NewGuid().ToString("N")[..8]
                    + ".bak");

            // Không khôi phục nếu chưa sao lưu được dữ liệu hiện tại.
            await BackupAsync(safety);

            string db = QuoteDatabase(database);

            SqlConnection.ClearAllPools();

            try
            {
                command.CommandText =
                    $"ALTER DATABASE {db} "
                    + "SET SINGLE_USER WITH ROLLBACK IMMEDIATE;";

                await command.ExecuteNonQueryAsync();

                command.CommandText =
                    $"RESTORE DATABASE {db} "
                    + "FROM DISK = @Path "
                    + "WITH FILE = @Position, REPLACE, RECOVERY;";

                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                command.CommandText =
                    $"ALTER DATABASE {db} SET MULTI_USER;";

                await command.ExecuteNonQueryAsync();
            }

            await SystemLogService.TryWriteAsync(
                null,
                "Quản trị",
                $"Khôi phục từ {path}; bản an toàn: {safety}.");

            return safety;
        }

        public static async Task<int> CleanCacheAsync()
        {
            using var connection = Database.CreateConnection();

            await connection.OpenAsync();
            await AuthorizeAsync(connection);

            string folder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "QuanLyChuoiNhaTro",
                "Cache");

            int count = 0;

            if (Directory.Exists(folder)
                && (File.GetAttributes(folder)
                    & FileAttributes.ReparsePoint) == 0)
            {
                foreach (string file
                    in Directory.EnumerateFiles(folder))
                {
                    if ((File.GetAttributes(file)
                        & FileAttributes.ReparsePoint) == 0)
                    {
                        File.Delete(file);
                        count++;
                    }
                }
            }

            await SystemLogService.TryWriteAsync(
                UserId(),
                "Quản trị",
                $"Dọn dẹp {count} tệp cache của ứng dụng.");

            return count;
        }
    }
}