using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;

namespace QuanLyChuoiNhaTro.Services
{
    public sealed class VatTuChoThu
    {
        public long MaVatTu { get; init; }

        public string TenVatTu { get; init; } = "";
        public string MaSuCo { get; init; } = "";

        public decimal SoLuong { get; init; }
        public decimal DonGia { get; init; }

        public decimal ThanhTien => decimal.Round(
            SoLuong * DonGia,
            0,
            MidpointRounding.AwayFromZero);
    }

    public static class HoaDonVatTuService
    {
        public static async Task<List<VatTuChoThu>> GetAsync(
            string maHopDong,
            int thang,
            int nam)
        {
            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException(
                    "Bạn chưa đăng nhập.");

            if (user.VaiTro != "Nhân viên quản lý")
            {
                throw new InvalidOperationException(
                    "Chức năng chỉ dành cho Quản lý.");
            }

            DateTime cutoff =
                new DateTime(nam, thang, 1).AddMonths(1);

            using var connection = Database.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    v.MaVatTu,
                    v.TenVatTu,
                    v.SoLuong,
                    v.DonGia,
                    ct.MaSuCo
                FROM dbo.VatTuSuaChua v
                JOIN dbo.ChiTietSuaChua ct
                  ON ct.MaChiTietSuaChua = v.MaChiTietSuaChua
                JOIN dbo.HopDongThue h
                  ON h.MaHopDong = v.MaHopDongTinhPhi
                JOIN dbo.PhongTro p
                  ON p.MaPhong = h.MaPhong
                WHERE v.MaHopDongTinhPhi = @MaHopDong
                  AND v.MaChiTietHoaDon IS NULL
                  AND ct.TrangThai = N'Hoàn thành'
                  AND ct.ThoiGianHoanThanh < @Cutoff
                  AND ROUND(v.ThanhTien, 0) > 0
                  AND EXISTS
                  (
                      SELECT 1
                      FROM dbo.NhanVien nv
                      WHERE nv.MaDayTro = p.MaDayTro
                        AND nv.MaNguoiDung = @QuanLy
                        AND nv.ChucVu = N'Quản lý'
                        AND nv.TrangThaiLamViec = N'Đang làm'
                  )
                ORDER BY ct.ThoiGianHoanThanh, v.MaVatTu;";

            command.Parameters.Add(
                "@MaHopDong", SqlDbType.VarChar, 20)
                .Value = maHopDong;

            command.Parameters.Add(
                "@QuanLy", SqlDbType.VarChar, 20)
                .Value = user.MaNguoiDung;

            command.Parameters.Add(
                "@Cutoff", SqlDbType.DateTime2)
                .Value = cutoff;

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            var result = new List<VatTuChoThu>();

            while (await reader.ReadAsync())
            {
                result.Add(new VatTuChoThu
                {
                    MaVatTu = reader.GetInt64(0),
                    TenVatTu = reader.GetString(1),
                    SoLuong = reader.GetDecimal(2),
                    DonGia = reader.GetDecimal(3),
                    MaSuCo = reader.GetString(4)
                });
            }

            return result;
        }

        public static async Task GanVaoHoaDonAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            VatTuChoThu material,
            string maHopDong,
            string maChiTietHoaDon,
            string quanLy)
        {
            using var command = connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText = @"
                UPDATE v
                SET MaChiTietHoaDon = @MaChiTietHoaDon
                FROM dbo.VatTuSuaChua v
                JOIN dbo.ChiTietSuaChua ct
                  ON ct.MaChiTietSuaChua = v.MaChiTietSuaChua
                JOIN dbo.HopDongThue h
                  ON h.MaHopDong = v.MaHopDongTinhPhi
                JOIN dbo.PhongTro p
                  ON p.MaPhong = h.MaPhong
                JOIN dbo.ChiTietHoaDon line
                  ON line.MaChiTietHoaDon = @MaChiTietHoaDon
                JOIN dbo.HoaDon hd
                  ON hd.MaHoaDon = line.MaHoaDon
                WHERE v.MaVatTu = @MaVatTu
                  AND v.MaHopDongTinhPhi = @MaHopDong
                  AND hd.MaHopDong = @MaHopDong
                  AND v.MaChiTietHoaDon IS NULL
                  AND v.TenVatTu = @TenVatTu
                  AND v.SoLuong = @SoLuong
                  AND v.DonGia = @DonGia
                  AND ct.TrangThai = N'Hoàn thành'
                  AND ct.ThoiGianHoanThanh <
                      DATEADD(
                          MONTH, 1,
                          DATEFROMPARTS(hd.Nam, hd.Thang, 1))
                  AND EXISTS
                  (
                      SELECT 1
                      FROM dbo.NhanVien nv
                      WHERE nv.MaDayTro = p.MaDayTro
                        AND nv.MaNguoiDung = @QuanLy
                        AND nv.ChucVu = N'Quản lý'
                        AND nv.TrangThaiLamViec = N'Đang làm'
                  );";

            command.Parameters.Add(
                "@MaVatTu", SqlDbType.BigInt)
                .Value = material.MaVatTu;

            command.Parameters.Add(
                "@MaHopDong", SqlDbType.VarChar, 20)
                .Value = maHopDong;

            command.Parameters.Add(
                "@MaChiTietHoaDon", SqlDbType.VarChar, 20)
                .Value = maChiTietHoaDon;

            command.Parameters.Add(
                "@QuanLy", SqlDbType.VarChar, 20)
                .Value = quanLy;

            command.Parameters.Add(
                "@TenVatTu", SqlDbType.NVarChar, 100)
                .Value = material.TenVatTu;

            var quantity = command.Parameters.Add(
                "@SoLuong", SqlDbType.Decimal);

            quantity.Precision = 10;
            quantity.Scale = 2;
            quantity.Value = material.SoLuong;

            var price = command.Parameters.Add(
                "@DonGia", SqlDbType.Decimal);

            price.Precision = 18;
            price.Scale = 0;
            price.Value = material.DonGia;

            int count = await command.ExecuteNonQueryAsync();

            if (count != 1)
            {
                throw new InvalidOperationException(
                    "Linh kiện đã được tính ở hóa đơn khác "
                    + "hoặc dữ liệu đã thay đổi. Hãy tải lại phòng.");
            }
        }
    }
}