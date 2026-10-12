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

        public static DataTable GetRooms()
        {
            const string sql = @"
                SELECT
                    p.MaPhong,
                    d.TenDayTro,
                    p.SoPhong,
                    p.LoaiPhong,
                    p.DienTich,
                    p.GiaThue,
                    p.TrangThai
                FROM dbo.PhongTro AS p
                INNER JOIN dbo.DayTro AS d
                    ON p.MaDayTro = d.MaDayTro
                ORDER BY d.TenDayTro, p.SoPhong;";

            using (SqlConnection connection =
                   new SqlConnection(ConnectionString))
            using (SqlCommand command =
                   new SqlCommand(sql, connection))
            using (SqlDataAdapter adapter =
                   new SqlDataAdapter(command))
            {
                DataTable table = new DataTable();

                connection.Open();
                adapter.Fill(table);

                return table;
            }
        }
    }
}