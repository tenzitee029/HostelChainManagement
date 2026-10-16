using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services.Management;

namespace QuanLyChuoiNhaTro.Services.Management
{
    public static class TaoHoaDonService
    {
        public sealed class DuLieuPhi
        {
            public decimal DienCu;
            public decimal DienMoi;
            public decimal GiaDien;

            public decimal NuocCu;
            public decimal NuocMoi;
            public decimal GiaNuoc;

            public decimal Rac;
            public decimal Internet;
            public decimal DichVu;
            public decimal PhuPhi;

            public string MaHoaDon = TaoMa("HDON");
        }

        public sealed class KhoanThu
        {
            public string Loai = "";
            public string GhiChu = "";

            public decimal? Cu;
            public decimal? Moi;

            public decimal SoLuong;
            public decimal DonGia;

            public VatTuChoThu? NguonVatTu;

            public decimal ThanhTien => decimal.Round(
                SoLuong * DonGia,
                0,
                MidpointRounding.AwayFromZero);
        }

        public static string TaoMa(string tienTo)
        {
            return tienTo
                + Guid.NewGuid().ToString("N")
                    .Substring(0, 20 - tienTo.Length)
                    .ToUpperInvariant();
        }

        public static bool DungTaiKhoan(string userId)
        {
            var user = UserSession.CurrentUser;

            return user != null
                && user.MaNguoiDung == userId
                && user.VaiTro == "Nhân viên quản lý";
        }

        public static async Task<DataTable> LayPhongAsync(
            int thang,
            int nam,
            string userId)
        {
            var table = new DataTable();

            using (var connection = Database.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT
                        h.MaHopDong,
                        h.MaPhong,
                        h.GiaThue,
                        h.KyThanhToan,
                        p.MaDayTro,
                        n.HoTen,
                        d.TenDayTro + N' - Phòng ' + p.SoPhong
                            AS TenHienThi
                    FROM dbo.HopDongThue h
                    JOIN dbo.PhongTro p
                      ON p.MaPhong = h.MaPhong
                    JOIN dbo.DayTro d
                      ON d.MaDayTro = p.MaDayTro
                    JOIN dbo.KhachThue k
                      ON k.MaKhachThue = h.MaKhachThue
                    JOIN dbo.NguoiDung n
                      ON n.MaNguoiDung = k.MaNguoiDung
                    WHERE h.TrangThai IN
                        (N'Hiệu lực', N'Hết hạn')
                      AND h.NgayBatDau <=
                          EOMONTH(DATEFROMPARTS(@Nam, @Thang, 1))
                      AND h.NgayKetThuc >=
                          DATEFROMPARTS(@Nam, @Thang, 1)
                      AND NOT EXISTS
                      (
                          SELECT 1
                          FROM dbo.HoaDon hd
                          WHERE hd.MaHopDong = h.MaHopDong
                            AND hd.Thang = @Thang
                            AND hd.Nam = @Nam
                      )
                      AND EXISTS
                      (
                          SELECT 1
                          FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @QuanLy
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      )
                    ORDER BY
                        d.TenDayTro,
                        p.SoPhong,
                        h.MaHopDong;";

                command.Parameters.Add(
                    "@Thang",
                    SqlDbType.Int).Value = thang;

                command.Parameters.Add(
                    "@Nam",
                    SqlDbType.Int).Value = nam;

                command.Parameters.Add(
                    "@QuanLy",
                    SqlDbType.VarChar,
                    20).Value = userId;

                await connection.OpenAsync();

                using var reader =
                    await command.ExecuteReaderAsync();

                table.Load(reader);
            }

            return table;
        }

        public static async Task<List<VatTuChoThu>> LayVatTuAsync(
            string maHopDong,
            int thang,
            int nam)
        {
            return await HoaDonVatTuService.GetAsync(
                maHopDong,
                thang,
                nam);
        }

        public static List<KhoanThu> TaoKhoanThu(
            DataRow phong,
            DuLieuPhi p,
            Dictionary<string, List<VatTuChoThu>> vatTuTheoHopDong)
        {
            var result = new List<KhoanThu>
            {
                new KhoanThu
                {
                    Loai = "Tiền phòng",
                    SoLuong = 1,
                    DonGia = Convert.ToDecimal(phong["GiaThue"]),
                    GhiChu = "Tiền phòng theo hợp đồng, tính đủ tháng"
                },
                new KhoanThu
                {
                    Loai = "Điện",
                    Cu = p.DienCu,
                    Moi = p.DienMoi,
                    SoLuong = p.DienMoi - p.DienCu,
                    DonGia = p.GiaDien,
                    GhiChu = "Tiền điện"
                },
                new KhoanThu
                {
                    Loai = "Nước",
                    Cu = p.NuocCu,
                    Moi = p.NuocMoi,
                    SoLuong = p.NuocMoi - p.NuocCu,
                    DonGia = p.GiaNuoc,
                    GhiChu = "Tiền nước"
                },
                new KhoanThu
                {
                    Loai = "Dịch vụ",
                    SoLuong = 1,
                    DonGia = p.Rac,
                    GhiChu = "Phí rác"
                },
                new KhoanThu
                {
                    Loai = "Dịch vụ",
                    SoLuong = 1,
                    DonGia = p.Internet,
                    GhiChu = "Internet"
                },
                new KhoanThu
                {
                    Loai = "Dịch vụ",
                    SoLuong = 1,
                    DonGia = p.DichVu,
                    GhiChu = "Phí dịch vụ"
                },
                new KhoanThu
                {
                    Loai = "Phụ phí",
                    SoLuong = 1,
                    DonGia = p.PhuPhi,
                    GhiChu = "Phụ phí phát sinh"
                }
            };

            string maHopDong =
                Convert.ToString(phong["MaHopDong"]) ?? "";

            if (vatTuTheoHopDong.TryGetValue(
                maHopDong,
                out var materials))
            {
                foreach (var material in materials)
                {
                    result.Add(new KhoanThu
                    {
                        Loai = "Phụ phí",
                        SoLuong = material.SoLuong,
                        DonGia = material.DonGia,

                        GhiChu =
                            "Linh kiện: " + material.TenVatTu
                            + " — Sự cố: " + material.MaSuCo,

                        NguonVatTu = material
                    });
                }
            }

            return result;
        }

        public static async Task PhatHanhAsync(
            List<DataRow> danhSach,
            Func<DataRow, DuLieuPhi> layPhi,
            Dictionary<string, List<VatTuChoThu>> vatTuTheoHopDong,
            string quanLy,
            int thang,
            int nam)
        {
            using var connection = Database.CreateConnection();

            await connection.OpenAsync();

            using var transaction =
                connection.BeginTransaction(
                    IsolationLevel.Serializable);

            foreach (DataRow phong in danhSach.OrderBy(
                row => Convert.ToString(row["MaPhong"])))
            {
                if (!DungTaiKhoan(quanLy))
                {
                    throw new InvalidOperationException(
                        "Phiên đăng nhập đã thay đổi.");
                }

                await LuuMotHoaDonAsync(
                    connection,
                    transaction,
                    phong,
                    layPhi(phong),
                    quanLy,
                    thang,
                    nam,
                    vatTuTheoHopDong);
            }

            transaction.Commit();
        }

        private static async Task LuuMotHoaDonAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            DataRow phong,
            DuLieuPhi phi,
            string quanLy,
            int thang,
            int nam,
            Dictionary<string, List<VatTuChoThu>> vatTuTheoHopDong)
        {
            List<KhoanThu> khoanThu =
                TaoKhoanThu(phong, phi, vatTuTheoHopDong);

            decimal tong = khoanThu.Sum(x => x.ThanhTien);

            int ngay = Math.Min(
                Convert.ToInt32(phong["KyThanhToan"]),
                DateTime.DaysInMonth(nam, thang));

            DateTime han = new DateTime(nam, thang, ngay);

            // Lưu đầu hóa đơn.
            // Không dùng biến khoan tại đây.
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText = @"
                    DECLARE @Khoa VARCHAR(20);
                    DECLARE @Gia DECIMAL(18,0);
                    DECLARE @Ky INT;

                    SELECT @Khoa = p.MaPhong
                    FROM dbo.PhongTro p WITH (UPDLOCK, HOLDLOCK)
                    WHERE p.MaPhong = @MaPhong
                      AND EXISTS
                      (
                          SELECT 1
                          FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @QuanLy
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      );

                    IF @Khoa IS NULL
                        THROW 50001,
                            N'Bạn không còn quyền với phòng.', 1;

                    SELECT
                        @Gia = h.GiaThue,
                        @Ky = h.KyThanhToan
                    FROM dbo.HopDongThue h WITH (UPDLOCK, HOLDLOCK)
                    WHERE h.MaHopDong = @MaHopDong
                      AND h.MaPhong = @MaPhong
                      AND h.TrangThai IN
                          (N'Hiệu lực', N'Hết hạn')
                      AND h.NgayBatDau <=
                          EOMONTH(DATEFROMPARTS(@Nam, @Thang, 1))
                      AND h.NgayKetThuc >=
                          DATEFROMPARTS(@Nam, @Thang, 1);

                    IF @Gia IS NULL
                       OR @Gia <> @GiaCu
                       OR @Ky <> @KyCu
                        THROW 50002,
                            N'Hợp đồng đã thay đổi. Hãy tải lại phòng.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM dbo.HoaDon WITH (UPDLOCK, HOLDLOCK)
                        WHERE MaHopDong = @MaHopDong
                          AND Thang = @Thang
                          AND Nam = @Nam
                    )
                        THROW 50003,
                            N'Hóa đơn của kỳ này đã tồn tại.', 1;

                    INSERT INTO dbo.HoaDon
                    (
                        MaHoaDon,
                        MaHopDong,
                        MaPhong,
                        Thang,
                        Nam,
                        HanThanhToan,
                        TongTien,
                        TrangThai
                    )
                    VALUES
                    (
                        @MaHoaDon,
                        @MaHopDong,
                        @MaPhong,
                        @Thang,
                        @Nam,
                        @Han,
                        @Tong,
                        N'Chưa thanh toán'
                    );";

                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = quanLy;

                command.Parameters.Add(
                    "@MaHoaDon", SqlDbType.VarChar, 20)
                    .Value = phi.MaHoaDon;

                command.Parameters.Add(
                    "@MaHopDong", SqlDbType.VarChar, 20)
                    .Value = phong["MaHopDong"];

                command.Parameters.Add(
                    "@MaPhong", SqlDbType.VarChar, 20)
                    .Value = phong["MaPhong"];

                command.Parameters.Add(
                    "@Thang", SqlDbType.Int).Value = thang;

                command.Parameters.Add(
                    "@Nam", SqlDbType.Int).Value = nam;

                command.Parameters.Add(
                    "@KyCu", SqlDbType.Int)
                    .Value = phong["KyThanhToan"];

                command.Parameters.Add(
                    "@Han", SqlDbType.Date).Value = han;

                ThemSo(
                    command, "@GiaCu", 18, 0,
                    Convert.ToDecimal(phong["GiaThue"]));

                ThemSo(command, "@Tong", 18, 0, tong);

                await command.ExecuteNonQueryAsync();
            }

            // Lưu từng khoản thu và liên kết vật tư.
            foreach (KhoanThu khoan in khoanThu)
            {
                string maChiTietHoaDon = TaoMa("CT");

                using var command = connection.CreateCommand();

                command.Transaction = transaction;

                command.CommandText = @"
                    INSERT INTO dbo.ChiTietHoaDon
                    (
                        MaChiTietHoaDon,
                        MaHoaDon,
                        LoaiKhoanThu,
                        ChiSoCu,
                        ChiSoMoi,
                        SoLuong,
                        DonGia,
                        ThanhTien,
                        GhiChu
                    )
                    VALUES
                    (
                        @MaCT,
                        @MaHoaDon,
                        @Loai,
                        @Cu,
                        @Moi,
                        @SoLuong,
                        @Gia,
                        @ThanhTien,
                        @GhiChu
                    );";

                command.Parameters.Add(
                    "@MaCT", SqlDbType.VarChar, 20)
                    .Value = maChiTietHoaDon;

                command.Parameters.Add(
                    "@MaHoaDon", SqlDbType.VarChar, 20)
                    .Value = phi.MaHoaDon;

                command.Parameters.Add(
                    "@Loai", SqlDbType.NVarChar, 50)
                    .Value = khoan.Loai;

                command.Parameters.Add(
                    "@GhiChu", SqlDbType.NVarChar, 255)
                    .Value = khoan.GhiChu;

                ThemSo(command, "@Cu", 10, 2, khoan.Cu);
                ThemSo(command, "@Moi", 10, 2, khoan.Moi);
                ThemSo(command, "@SoLuong", 10, 2, khoan.SoLuong);
                ThemSo(command, "@Gia", 18, 0, khoan.DonGia);
                ThemSo(command, "@ThanhTien", 18, 0, khoan.ThanhTien);

                await command.ExecuteNonQueryAsync();

                // Chỉ dòng linh kiện mới có nguồn vật tư.
                if (khoan.NguonVatTu != null)
                {
                    await HoaDonVatTuService.GanVaoHoaDonAsync(
                        connection,
                        transaction,
                        khoan.NguonVatTu,
                        Convert.ToString(phong["MaHopDong"]) ?? "",
                        maChiTietHoaDon,
                        quanLy);
                }
            }
        }

        private static void ThemSo(
            SqlCommand command,
            string ten,
            byte precision,
            byte scale,
            decimal? giaTri)
        {
            var parameter = command.Parameters.Add(
                ten,
                SqlDbType.Decimal);

            parameter.Precision = precision;
            parameter.Scale = scale;

            parameter.Value = giaTri.HasValue
                ? (object)giaTri.Value
                : DBNull.Value;
        }
    }
}