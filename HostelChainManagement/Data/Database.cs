using System.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Text.Json;

namespace QuanLyChuoiNhaTro.Data
{
    public static class Database
    {
        public static SqlConnection CreateConnection()
        {
            return new SqlConnection(ConnectionString);
        }


        private sealed class DatabaseConfig
        {
            public string Server { get; set; } = "";

            public string Database { get; set; } = "";

            public string UserId { get; set; } = "";

            public string Password { get; set; } = "";
        }

        private static string ConnectionString
        {
            get
            {
                string configPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Data",
                    "database.json");

                if (!File.Exists(configPath))
                {
                    throw new InvalidOperationException(
                        "Không tìm thấy file database.json " +
                        "trong thư mục ứng dụng.");
                }

                var config =
                    JsonSerializer.Deserialize<DatabaseConfig>(
                        File.ReadAllText(configPath));

                if (config == null
                    || string.IsNullOrWhiteSpace(config.Server)
                    || string.IsNullOrWhiteSpace(config.Database)
                    || string.IsNullOrWhiteSpace(config.UserId)
                    || string.IsNullOrWhiteSpace(config.Password))
                {
                    throw new InvalidOperationException(
                        "Thông tin kết nối trong database.json " +
                        "chưa đầy đủ.");
                }

                var builder = new SqlConnectionStringBuilder
                {
                    DataSource = config.Server,
                    InitialCatalog = config.Database,

                    IntegratedSecurity = false,

                    UserID = config.UserId,
                    Password = config.Password,

                    TrustServerCertificate = true,

                    ConnectTimeout = 10,
                    PersistSecurityInfo = false
                };

                builder["Encrypt"] = true;

                return builder.ConnectionString;
            }
        }

        public static void TestConnection()
        {
            using (SqlConnection connection =
                   new SqlConnection(ConnectionString))
            {
                connection.Open();
            }
        }
    }
}