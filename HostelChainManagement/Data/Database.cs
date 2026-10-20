using System.Data;
using Microsoft.Data.SqlClient;

namespace QuanLyChuoiNhaTro.Data
{
    public static class Database
    {
        public static SqlConnection CreateConnection()
        {
            return new SqlConnection(ConnectionString);
        }


        private const string ConnectionString =
            @"Server=LAPTOP-DQP9I1OD\SQLEXPRESS;Database=QuanLyNhaTro;" +
            "Integrated Security=True;Encrypt=True;" +
            "TrustServerCertificate=True;Connect Timeout=10;";

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