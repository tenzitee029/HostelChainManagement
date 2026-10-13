
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services
{
    public static class NhacNoChuTroService
    {
        public sealed record KetQuaGuiNhacNo(
            string MaThongBao,
            string TenKhach,
            int SoHoaDon,
            decimal TongNo);

        public static async Task<DataTable> LayDanhSachNoQuaHanAsync()
        {
            string maChuTro = LayMaChuTro();

            const string sql = @"
                SELECT
                    hd.MaHoaDon,
                    hd.MaPhong,
                    d.TenDayTro,
                    p.SoPhong,
                    nd.HoTen AS HoTenKhach,
                    nd.MaNguoiDung AS MaNguoiNhan,
                    hd.HanThanhToan,
                    hd.TongTien,
                    ISNULL(daTra.SoTienDaTra, 0) AS DaTra,
                    hd.TongTien -
                        ISNULL(daTra.SoTienDaTra, 0) AS ConNo
                FROM dbo.HoaDon AS hd
                INNER JOIN dbo.HopDongThue AS h
                    ON h.MaHopDong = hd.MaHopDong
                   AND h.MaPhong = hd.MaPhong
                INNER JOIN dbo.PhongTro AS p
                    ON p.MaPhong = hd.MaPhong
                INNER JOIN dbo.DayTro AS d
                    ON d.MaDayTro = p.MaDayTro
                INNER JOIN dbo.KhachThue AS k
                    ON k.MaKhachThue = h.MaKhachThue
                INNER JOIN dbo.NguoiDung AS nd
                    ON nd.MaNguoiDung = k.MaNguoiDung
                OUTER APPLY
                (
                    SELECT SUM(tt.SoTien) AS SoTienDaTra
                    FROM dbo.ThanhToan AS tt
                    WHERE tt.MaHoaDon = hd.MaHoaDon
                ) AS daTra
                WHERE d.MaChuTro = @MaChuTro
                  AND hd.HanThanhToan < @HomNay
                  AND hd.TrangThai <> N'Đã thanh toán'
                  AND hd.TongTien >
                      ISNULL(daTra.SoTienDaTra, 0)
                  AND nd.VaiTro = N'Khách thuê'
                ORDER BY
                    hd.HanThanhToan,
                    d.TenDayTro,
                    p.SoPhong,
                    hd.MaHoaDon;";

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;

            command.Parameters.Add(
                "@MaChuTro",
                SqlDbType.VarChar,
                20).Value = maChuTro;

            command.Parameters.Add(
                "@HomNay",
                SqlDbType.Date).Value = DateTime.Today;

            await connection.OpenAsync();

            using var reader =
                await command.ExecuteReaderAsync();

            var table = new DataTable();

            table.Load(reader);

            return table;
        }

        public static async Task<KetQuaGuiNhacNo> GuiNhacNoAsync(
            IReadOnlyCollection<string> danhSachMaHoaDon,
            decimal tongNoDuKien,
            DateTime hanChot,
            string noiDung)
        {
            string maChuTro = LayMaChuTro();

            if (danhSachMaHoaDon == null
                || danhSachMaHoaDon.Count == 0)
            {
                throw new InvalidOperationException(
                    "Vui lòng chọn ít nhất một hóa đơn quá hạn.");
            }

            string[] cacMaHoaDon = danhSachMaHoaDon
                .Select(x => x?.Trim() ?? "")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (cacMaHoaDon.Length > 100)
            {
                throw new InvalidOperationException(
                    "Mỗi lần chỉ được nhắc tối đa 100 hóa đơn.");
            }

            if (cacMaHoaDon.Any(
                    x => x.Length == 0 || x.Length > 20))
            {
                throw new InvalidOperationException(
                    "Danh sách mã hóa đơn không hợp lệ.");
            }

            if (tongNoDuKien <= 0)
            {
                throw new InvalidOperationException(
                    "Số tiền cần nhắc nợ không hợp lệ.");
            }

            if (hanChot.Date < DateTime.Today)
            {
                throw new InvalidOperationException(
                    "Hạn chót đề nghị thanh toán không được nằm trong quá khứ.");
            }

            noiDung = noiDung?.Trim() ?? "";

            if (noiDung.Length < 10)
            {
                throw new InvalidOperationException(
                    "Nội dung nhắc nợ phải có ít nhất 10 ký tự.");
            }

            if (noiDung.Length > 4000)
            {
                throw new InvalidOperationException(
                    "Nội dung nhắc nợ không được quá 4000 ký tự.");
            }

            using var connection = Database.CreateConnection();

            await connection.OpenAsync();

            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            try
            {
                using (var kiemTraChuTro = connection.CreateCommand())
                {
                    kiemTraChuTro.Transaction = transaction;

                    kiemTraChuTro.CommandText = @"
                        SELECT COUNT(*)
                        FROM dbo.NguoiDung WITH (UPDLOCK, HOLDLOCK)
                        WHERE MaNguoiDung = @MaChuTro
                          AND VaiTro = N'Chủ nhà trọ'
                          AND TrangThai = N'Hoạt động';";

                    kiemTraChuTro.Parameters.Add(
                        "@MaChuTro",
                        SqlDbType.VarChar,
                        20).Value = maChuTro;

                    int soChuTro = Convert.ToInt32(
                        await kiemTraChuTro.ExecuteScalarAsync());

                    if (soChuTro != 1)
                    {
                        throw new InvalidOperationException(
                            "Tài khoản Chủ nhà trọ không còn hoạt động.");
                    }
                }

                string? maNguoiNhan = null;
                string tenKhach = "";
                decimal tongNoThucTe = 0;

                foreach (string maHoaDon in cacMaHoaDon)
                {
                    using var command = connection.CreateCommand();

                    command.Transaction = transaction;

                    command.CommandText = @"
                        SELECT TOP (1)
                            nd.MaNguoiDung,
                            nd.HoTen,
                            hd.TongTien -
                                ISNULL(daTra.SoTienDaTra, 0) AS ConNo
                        FROM dbo.HoaDon AS hd WITH (UPDLOCK, HOLDLOCK)
                        INNER JOIN dbo.HopDongThue AS h
                            ON h.MaHopDong = hd.MaHopDong
                           AND h.MaPhong = hd.MaPhong
                        INNER JOIN dbo.PhongTro AS p
                            ON p.MaPhong = hd.MaPhong
                        INNER JOIN dbo.DayTro AS d
                            ON d.MaDayTro = p.MaDayTro
                        INNER JOIN dbo.KhachThue AS k
                            ON k.MaKhachThue = h.MaKhachThue
                        INNER JOIN dbo.NguoiDung AS nd
                            ON nd.MaNguoiDung = k.MaNguoiDung
                        OUTER APPLY
                        (
                            SELECT
                                SUM(tt.SoTien) AS SoTienDaTra
                            FROM dbo.ThanhToan AS tt WITH (HOLDLOCK)
                            WHERE tt.MaHoaDon = hd.MaHoaDon
                        ) AS daTra
                        WHERE hd.MaHoaDon = @MaHoaDon
                          AND d.MaChuTro = @MaChuTro
                          AND nd.VaiTro = N'Khách thuê'
                          AND hd.HanThanhToan < @HomNay
                          AND hd.TrangThai <> N'Đã thanh toán'
                          AND hd.TongTien >
                              ISNULL(daTra.SoTienDaTra, 0);";

                    command.Parameters.Add(
                        "@MaHoaDon",
                        SqlDbType.VarChar,
                        20).Value = maHoaDon;

                    command.Parameters.Add(
                        "@MaChuTro",
                        SqlDbType.VarChar,
                        20).Value = maChuTro;

                    command.Parameters.Add(
                        "@HomNay",
                        SqlDbType.Date).Value = DateTime.Today;

                    using var reader =
                        await command.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        throw new InvalidOperationException(
                            "Hóa đơn " + maHoaDon
                            + " không còn đủ điều kiện nhắc nợ. "
                            + "Vui lòng tải lại danh sách.");
                    }

                    string nguoiNhanHienTai = reader.GetString(0);
                    string tenKhachHienTai = reader.GetString(1);
                    decimal conNo = reader.GetDecimal(2);

                    if (maNguoiNhan == null)
                    {
                        maNguoiNhan = nguoiNhanHienTai;
                        tenKhach = tenKhachHienTai;
                    }
                    else if (!string.Equals(
                        maNguoiNhan,
                        nguoiNhanHienTai,
                        StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Các hóa đơn đã chọn thuộc nhiều khách thuê. "
                            + "Hãy gửi thông báo riêng cho từng khách.");
                    }

                    tongNoThucTe += conNo;
                }

                if (maNguoiNhan == null)
                {
                    throw new InvalidOperationException(
                        "Không tìm thấy khách thuê nhận thông báo.");
                }

                if (tongNoThucTe != tongNoDuKien)
                {
                    throw new InvalidOperationException(
                        "Số tiền công nợ đã thay đổi do có giao dịch mới. "
                        + "Vui lòng tải lại danh sách trước khi gửi.");
                }

                string maThongBao =
                    "TB" + Guid.NewGuid().ToString("N")[..18];

                string danhSachMa =
                    string.Join(", ", cacMaHoaDon);

                string noiDungLuu =
                    "Các hóa đơn được nhắc: "
                    + danhSachMa
                    + Environment.NewLine
                    + "Hạn đề nghị thanh toán: "
                    + hanChot.ToString("dd/MM/yyyy")
                    + Environment.NewLine
                    + Environment.NewLine
                    + noiDung;

                using (var insertThongBao = connection.CreateCommand())
                {
                    insertThongBao.Transaction = transaction;

                    insertThongBao.CommandText = @"
                        INSERT INTO dbo.ThongBao
                        (
                            MaThongBao,
                            MaNguoiGui,
                            TieuDe,
                            NoiDung
                        )
                        VALUES
                        (
                            @MaThongBao,
                            @MaNguoiGui,
                            @TieuDe,
                            @NoiDung
                        );";

                    insertThongBao.Parameters.Add(
                        "@MaThongBao",
                        SqlDbType.VarChar,
                        20).Value = maThongBao;

                    insertThongBao.Parameters.Add(
                        "@MaNguoiGui",
                        SqlDbType.VarChar,
                        20).Value = maChuTro;

                    insertThongBao.Parameters.Add(
                        "@TieuDe",
                        SqlDbType.NVarChar,
                        200).Value = "Nhắc nợ hóa đơn quá hạn";

                    insertThongBao.Parameters.Add(
                        "@NoiDung",
                        SqlDbType.NVarChar,
                        -1).Value = noiDungLuu;

                    int soDong =
                        await insertThongBao.ExecuteNonQueryAsync();

                    if (soDong != 1)
                    {
                        throw new InvalidOperationException(
                            "Không thể tạo thông báo nhắc nợ.");
                    }
                }

                using (var insertNguoiNhan = connection.CreateCommand())
                {
                    insertNguoiNhan.Transaction = transaction;

                    insertNguoiNhan.CommandText = @"
                        INSERT INTO dbo.ThongBaoNguoiNhan
                        (
                            MaThongBao,
                            MaNguoiNhan,
                            DaDoc
                        )
                        VALUES
                        (
                            @MaThongBao,
                            @MaNguoiNhan,
                            0
                        );";

                    insertNguoiNhan.Parameters.Add(
                        "@MaThongBao",
                        SqlDbType.VarChar,
                        20).Value = maThongBao;

                    insertNguoiNhan.Parameters.Add(
                        "@MaNguoiNhan",
                        SqlDbType.VarChar,
                        20).Value = maNguoiNhan;

                    int soDong =
                        await insertNguoiNhan.ExecuteNonQueryAsync();

                    if (soDong != 1)
                    {
                        throw new InvalidOperationException(
                            "Không thể xác định người nhận thông báo.");
                    }
                }

                transaction.Commit();

                return new KetQuaGuiNhacNo(
                    maThongBao,
                    tenKhach,
                    cacMaHoaDon.Length,
                    tongNoThucTe);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static string LayMaChuTro()
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException(
                    "Bạn chưa đăng nhập.");

            if (user.VaiTro != "Chủ nhà trọ")
            {
                throw new InvalidOperationException(
                    "Chức năng chỉ dành cho Chủ nhà trọ.");
            }

            return user.MaNguoiDung;
        }
    }
}
