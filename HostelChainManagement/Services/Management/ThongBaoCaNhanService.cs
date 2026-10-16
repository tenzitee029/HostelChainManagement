using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public sealed class ThongBaoCaNhanService : IDisposable
    {
        private readonly SqlConnection _connection =
            Database.CreateConnection();

        public async Task MoKetNoiAsync()
        {
            await _connection.OpenAsync();
        }

        public static async Task<DataTable> LayDanhSachAsync(
            string maNguoiDung)
        {
            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    tb.MaThongBao,
                    tb.TieuDe,
                    tb.NgayGui,
                    n.HoTen AS NguoiGui,
                    nn.DaDoc
                FROM dbo.ThongBaoNguoiNhan nn
                JOIN dbo.ThongBao tb
                  ON tb.MaThongBao = nn.MaThongBao
                JOIN dbo.NguoiDung n
                  ON n.MaNguoiDung = tb.MaNguoiGui
                WHERE nn.MaNguoiNhan = @MaNguoiDung
                ORDER BY tb.NgayGui DESC, tb.MaThongBao;";

            command.Parameters.Add(
                "@MaNguoiDung", SqlDbType.VarChar, 20)
                .Value = maNguoiDung;

            await connection.OpenAsync();

            using var reader =
                await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }

        public async Task<ChiTietThongBaoCaNhan?> LayChiTietAsync(
            string maThongBao,
            string maNguoiDung)
        {
            using var command = _connection.CreateCommand();

            command.CommandText = @"
                SELECT tb.TieuDe, tb.NoiDung,
                       tb.NgayGui, n.HoTen AS NguoiGui
                FROM dbo.ThongBao tb
                JOIN dbo.ThongBaoNguoiNhan nn
                  ON nn.MaThongBao = tb.MaThongBao
                JOIN dbo.NguoiDung n
                  ON n.MaNguoiDung = tb.MaNguoiGui
                WHERE tb.MaThongBao = @MaThongBao
                  AND nn.MaNguoiNhan = @MaNguoiDung;";

            command.Parameters.Add(
                "@MaThongBao", SqlDbType.VarChar, 20)
                .Value = maThongBao;

            command.Parameters.Add(
                "@MaNguoiDung", SqlDbType.VarChar, 20)
                .Value = maNguoiDung;

            using var reader =
                await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return new ChiTietThongBaoCaNhan
            {
                TieuDe = Convert.ToString(reader["TieuDe"]) ?? "",
                NoiDung = Convert.ToString(reader["NoiDung"]) ?? "",
                NguoiGui = Convert.ToString(reader["NguoiGui"]) ?? "",
                NgayGui = Convert.ToDateTime(reader["NgayGui"])
            };
        }

        public async Task DanhDauDaDocAsync(
            string maThongBao,
            string maNguoiDung)
        {
            using var command = _connection.CreateCommand();

            command.CommandText = @"
                UPDATE dbo.ThongBaoNguoiNhan
                SET DaDoc = 1,
                    NgayDoc = GETDATE()
                WHERE MaThongBao = @MaThongBao
                  AND MaNguoiNhan = @MaNguoiDung
                  AND DaDoc = 0;";

            command.Parameters.Add(
                "@MaThongBao", SqlDbType.VarChar, 20)
                .Value = maThongBao;

            command.Parameters.Add(
                "@MaNguoiDung", SqlDbType.VarChar, 20)
                .Value = maNguoiDung;

            await command.ExecuteNonQueryAsync();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        public static async Task<int> DemChuaDocAsync(string maNguoiDung)
        {
            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT COUNT(*)
                FROM dbo.ThongBaoNguoiNhan
                WHERE MaNguoiNhan = @MaNguoiDung
                  AND DaDoc = 0;";

            command.Parameters.Add(
                "@MaNguoiDung", SqlDbType.VarChar, 20)
                .Value = maNguoiDung;

            await connection.OpenAsync();

            return Convert.ToInt32(
                await command.ExecuteScalarAsync());
        }
    }

    public sealed class ChiTietThongBaoCaNhan
    {
        public string TieuDe { get; init; } = "";
        public string NoiDung { get; init; } = "";
        public string NguoiGui { get; init; } = "";
        public DateTime NgayGui { get; init; }
    }
}