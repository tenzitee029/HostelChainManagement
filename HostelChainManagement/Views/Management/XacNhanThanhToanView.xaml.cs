using System;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class XacNhanThanhToanView : UserControl
    {
        private readonly string _maHoaDon;

        private decimal _tongTien;
        private decimal _conNo;

        private bool _dangTai;
        private bool _dangLuu;
        private bool _sanSang;

        public event Action? QuayLai;
        public event Action? DaLuu;

        public XacNhanThanhToanView(string maHoaDon)
        {
            InitializeComponent();

            _maHoaDon = maHoaDon;
            txtMaHoaDon.Text = maHoaDon;

            DatTrangThai();
        }

        private static string Tien(decimal soTien)
        {
            return soTien.ToString(
                "N0", CultureInfo.GetCultureInfo("vi-VN")) + " VNĐ";
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai || _sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text =
                    "Bạn không có quyền xác nhận thanh toán.";
                return;
            }

            _dangTai = true;
            DatTrangThai();
            txtThongBao.Text = "Đang tải hóa đơn...";

            try
            {
                using var connection = Database.CreateConnection();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT
                        n.HoTen,
                        hd.TongTien,
                        COALESCE(tt.DaTra, 0) AS DaTra
                    FROM dbo.HoaDon hd
                    JOIN dbo.HopDongThue h
                      ON h.MaHopDong = hd.MaHopDong
                    JOIN dbo.KhachThue k
                      ON k.MaKhachThue = h.MaKhachThue
                    JOIN dbo.NguoiDung n
                      ON n.MaNguoiDung = k.MaNguoiDung
                    JOIN dbo.PhongTro p
                      ON p.MaPhong = hd.MaPhong
                    OUTER APPLY (
                        SELECT SUM(t.SoTien) AS DaTra
                        FROM dbo.ThanhToan t
                        WHERE t.MaHoaDon = hd.MaHoaDon
                    ) tt
                    WHERE hd.MaHoaDon = @MaHoaDon
                      AND EXISTS (
                          SELECT 1 FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @QuanLy
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      );";

                command.Parameters.Add(
                    "@MaHoaDon", SqlDbType.VarChar, 20)
                    .Value = _maHoaDon;

                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;

                await connection.OpenAsync();

                using var reader =
                    await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    txtThongBao.Text =
                        "Hóa đơn không tồn tại hoặc bạn không có quyền quản lý.";
                    return;
                }

                if (!IsLoaded)
                    return;

                _tongTien = Convert.ToDecimal(reader["TongTien"]);
                decimal daTra = Convert.ToDecimal(reader["DaTra"]);

                _conNo = Math.Max(0, _tongTien - daTra);

                txtTenKhach.Text = Convert.ToString(reader["HoTen"]);
                txtTongTien.Text = Tien(_tongTien);
                txtConNo.Text = Tien(_conNo);

                txtTrangThai.Text = _conNo == 0
                    ? "Đã thanh toán"
                    : daTra > 0
                        ? "Còn nợ"
                        : "Chưa thanh toán";

                if (_conNo == 0)
                {
                    txtThongBao.Text =
                        "Hóa đơn đã được thanh toán hết.";
                    return;
                }

                txtSoTien.Text = _conNo.ToString(
                    "0", CultureInfo.InvariantCulture);

                _sanSang = true;
                txtThongBao.Text = "";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không tải được hóa đơn: " + ex.Message;
            }
            finally
            {
                _dangTai = false;
                DatTrangThai();
            }
        }

        private async void Luu_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangLuu || !_sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text =
                    "Bạn không có quyền xác nhận thanh toán.";
                return;
            }

            if (!decimal.TryParse(
                    txtSoTien.Text.Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out decimal soTien)
                || soTien <= 0
                || soTien > _conNo)
            {
                txtThongBao.Text =
                    "Nhập số tiền nguyên dương, không vượt quá "
                    + Tien(_conNo) + ".";
                return;
            }

            string phuongThuc = rbTienMat.IsChecked == true
                ? "Tiền mặt"
                : rbChuyenKhoan.IsChecked == true
                    ? "Chuyển khoản"
                    : "";

            if (phuongThuc.Length == 0)
            {
                txtThongBao.Text =
                    "Vui lòng chọn phương thức thanh toán.";
                return;
            }

            var xacNhan = MessageBox.Show(
                $"Xác nhận đã nhận {Tien(soTien)}?\n"
                + $"Hóa đơn: {_maHoaDon}\n"
                + $"Phương thức: {phuongThuc}",
                "Xác nhận tiền đã nhận",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (xacNhan != MessageBoxResult.Yes)
                return;

            _dangLuu = true;
            DatTrangThai();
            txtThongBao.Text = "Đang ghi nhận thanh toán...";

            bool thanhCong = false;
            decimal noSauThanhToan = _conNo - soTien;

            try
            {
                using var connection = Database.CreateConnection();
                await connection.OpenAsync();

                using var transaction =
                    connection.BeginTransaction(IsolationLevel.Serializable);

                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                command.CommandText = @"
                    DECLARE @Tong DECIMAL(18,0);
                    DECLARE @DaTra DECIMAL(38,0);
                    DECLARE @No DECIMAL(38,0);

                    SELECT @Tong = hd.TongTien
                    FROM dbo.HoaDon hd WITH (UPDLOCK, HOLDLOCK)
                    JOIN dbo.PhongTro p ON p.MaPhong = hd.MaPhong
                    WHERE hd.MaHoaDon = @MaHoaDon
                      AND EXISTS (
                          SELECT 1 FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @QuanLy
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      );

                    IF @Tong IS NULL
                        THROW 50001,
                            N'Hóa đơn không tồn tại hoặc bạn không còn quyền quản lý.', 1;

                    SELECT @DaTra = COALESCE(SUM(t.SoTien), 0)
                    FROM dbo.ThanhToan t WITH (UPDLOCK, HOLDLOCK)
                    WHERE t.MaHoaDon = @MaHoaDon;

                    SET @No = @Tong - @DaTra;

                    IF @Tong <> @TongCu OR @No <> @NoCu
                        THROW 50002,
                            N'Hóa đơn hoặc số nợ đã thay đổi. Quay lại và mở form mới.', 1;

                    IF @SoTien <= 0 OR @SoTien > @No
                        THROW 50003,
                            N'Số tiền thanh toán vượt quá số nợ hiện tại.', 1;

                    INSERT INTO dbo.ThanhToan
                        (MaThanhToan, MaHoaDon, SoTien,
                         PhuongThuc, MaNguoiXacNhan, GhiChu)
                    VALUES
                        (@MaThanhToan, @MaHoaDon, @SoTien,
                         @PhuongThuc, @QuanLy,
                         N'Ghi nhận từ form xác nhận thanh toán');

                    UPDATE dbo.HoaDon
                    SET TrangThai = CASE
                            WHEN @DaTra + @SoTien >= @Tong
                                THEN N'Đã thanh toán'
                            ELSE N'Còn nợ'
                        END,
                        PhuongThucThanhToan = @PhuongThuc,
                        NgayThanhToan = GETDATE(),
                        MaGiaoDich = NULL
                    WHERE MaHoaDon = @MaHoaDon;";

                command.Parameters.Add(
                    "@MaHoaDon", SqlDbType.VarChar, 20)
                    .Value = _maHoaDon;

                command.Parameters.Add(
                    "@MaThanhToan", SqlDbType.VarChar, 20)
                    .Value = "TT" + Guid.NewGuid().ToString("N")
                        .Substring(0, 18).ToUpperInvariant();

                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;

                command.Parameters.Add(
                    "@PhuongThuc", SqlDbType.NVarChar, 50)
                    .Value = phuongThuc;

                ThemTien(command, "@SoTien", soTien);
                ThemTien(command, "@TongCu", _tongTien);
                ThemTien(command, "@NoCu", _conNo);

                await command.ExecuteNonQueryAsync();

                transaction.Commit();
                thanhCong = true;
                _sanSang = false;
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                txtThongBao.Text =
                    "Dữ liệu đang được cập nhật. Vui lòng thử lại.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không ghi nhận được thanh toán: " + ex.Message;
            }
            finally
            {
                _dangLuu = false;
                DatTrangThai();
            }

            if (thanhCong)
            {
                MessageBox.Show(
                    "Ghi nhận thanh toán thành công.\n"
                    + (noSauThanhToan == 0
                        ? "Hóa đơn đã thanh toán hết."
                        : "Còn nợ: " + Tien(noSauThanhToan)),
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DaLuu?.Invoke();
            }
        }

        private static void ThemTien(
            SqlCommand command, string ten, decimal giaTri)
        {
            var parameter =
                command.Parameters.Add(ten, SqlDbType.Decimal);

            parameter.Precision = 18;
            parameter.Scale = 0;
            parameter.Value = giaTri;
        }

        private void DatTrangThai()
        {
            bool choPhep =
                _sanSang && !_dangTai && !_dangLuu;

            pnlThanhToan.IsEnabled = choPhep;
            btnLuu.IsEnabled = choPhep;
            btnQuayLai.IsEnabled = !_dangLuu;

            btnLuu.Content = _dangLuu
                ? "Đang lưu..."
                : "Xác nhận hoàn tất";
        }

        private void QuayLai_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_dangLuu)
                QuayLai?.Invoke();
        }
    }
}