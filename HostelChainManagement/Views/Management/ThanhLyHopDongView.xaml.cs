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
    public partial class ThanhLyHopDongView : UserControl
    {
        private readonly string _maHopDong;
        private string _maPhong = "";
        private string _maKhach = "";

        private decimal _tienCoc;
        private decimal _tienNo;
        private bool _dangTai;
        private bool _dangLuu;
        private bool _sanSang;

        public event Action? QuayLai;
        public event Action? DaLuu;

        public ThanhLyHopDongView(string maHopDong)
        {
            InitializeComponent();

            _maHopDong = maHopDong;
            txtMaHopDong.Text = maHopDong;
        }

        private static string DinhDangTien(decimal tien)
        {
            return tien.ToString(
                "N0", CultureInfo.GetCultureInfo("vi-VN")) + " VNĐ";
        }

        private bool DocKhauTru(out decimal soTien)
        {
            return decimal.TryParse(
                       txtKhauTru.Text.Trim(),
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out soTien)
                   && soTien >= 0
                   && soTien <= _tienCoc;
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai || _sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền thanh lý hợp đồng.";
                return;
            }

            _dangTai = true;
            DatTrangThai();
            txtThongBao.Text = "Đang tải thông tin thanh lý...";

            try
            {
                using var connection = Database.CreateConnection();
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT h.MaPhong, h.MaKhachThue,
                               h.TienCoc, h.TrangThai,
                               h.NgayBatDau, n.HoTen,
                               d.TenDayTro + N' - ' + p.SoPhong AS TenPhong,
                               CAST(GETDATE() AS DATE) AS HomNay
                        FROM dbo.HopDongThue h
                        JOIN dbo.KhachThue k
                          ON k.MaKhachThue = h.MaKhachThue
                        JOIN dbo.NguoiDung n
                          ON n.MaNguoiDung = k.MaNguoiDung
                        JOIN dbo.PhongTro p ON p.MaPhong = h.MaPhong
                        JOIN dbo.DayTro d ON d.MaDayTro = p.MaDayTro
                        WHERE h.MaHopDong = @MaHopDong
                          AND EXISTS (
                              SELECT 1 FROM dbo.NhanVien nv
                              WHERE nv.MaDayTro = p.MaDayTro
                                AND nv.MaNguoiDung = @QuanLy
                                AND nv.ChucVu = N'Quản lý'
                                AND nv.TrangThaiLamViec = N'Đang làm'
                          );";

                    command.Parameters.Add(
                        "@MaHopDong", SqlDbType.VarChar, 20)
                        .Value = _maHopDong;
                    command.Parameters.Add(
                        "@QuanLy", SqlDbType.VarChar, 20)
                        .Value = user.MaNguoiDung;

                    using var reader = await command.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        txtThongBao.Text =
                            "Hợp đồng không tồn tại hoặc bạn không có quyền.";
                        return;
                    }

                    if (!IsLoaded)
                        return;

                    _maPhong = Convert.ToString(reader["MaPhong"]) ?? "";
                    _maKhach = Convert.ToString(reader["MaKhachThue"]) ?? "";
                    _tienCoc = Convert.ToDecimal(reader["TienCoc"]);

                    string trangThai =
                        Convert.ToString(reader["TrangThai"]) ?? "";

                    txtKhach.Text = Convert.ToString(reader["HoTen"]);
                    txtPhong.Text = Convert.ToString(reader["TenPhong"]);
                    txtTrangThai.Text = trangThai;
                    txtTienCoc.Text = DinhDangTien(_tienCoc);

                    if (trangThai != "Hiệu lực" && trangThai != "Hết hạn")
                    {
                        txtThongBao.Text = "Hợp đồng đã thanh lý.";
                        return;
                    }

                    if (Convert.ToDateTime(reader["NgayBatDau"]).Date >
                        Convert.ToDateTime(reader["HomNay"]).Date)
                    {
                        txtThongBao.Text =
                            "Hợp đồng chưa bắt đầu. Cần xử lý hủy hợp đồng riêng.";
                        return;
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT COALESCE(SUM(
                            CASE WHEN hd.TongTien > COALESCE(tt.DaTra, 0)
                                 THEN hd.TongTien - COALESCE(tt.DaTra, 0)
                                 ELSE 0 END
                        ), 0)
                        FROM dbo.HoaDon hd
                        OUTER APPLY (
                            SELECT SUM(t.SoTien) AS DaTra
                            FROM dbo.ThanhToan t
                            WHERE t.MaHoaDon = hd.MaHoaDon
                        ) tt
                        WHERE hd.MaHopDong = @MaHopDong;";

                    command.Parameters.Add(
                        "@MaHopDong", SqlDbType.VarChar, 20)
                        .Value = _maHopDong;

                    _tienNo = Convert.ToDecimal(
                        await command.ExecuteScalarAsync());

                    txtNo.Text = DinhDangTien(_tienNo);
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT t.TenThietBi, p.SoLuong,
                               p.TinhTrang, p.GhiChu
                        FROM dbo.PhongThietBi p
                        JOIN dbo.TrangThietBi t
                          ON t.MaThietBi = p.MaThietBi
                        WHERE p.MaPhong = @MaPhong
                        ORDER BY t.TenThietBi;";

                    command.Parameters.Add(
                        "@MaPhong", SqlDbType.VarChar, 20)
                        .Value = _maPhong;

                    using var reader = await command.ExecuteReaderAsync();

                    var table = new DataTable();
                    table.Load(reader);
                    dgThietBi.ItemsSource = table.DefaultView;
                }

                _sanSang = true;
                TinhHoanCoc();

                txtThongBao.Text = _tienNo > 0
                    ? "Hợp đồng còn nợ hóa đơn. Thanh toán xong rồi mở lại form."
                    : "";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không tải được thông tin: " + ex.Message;
            }
            finally
            {
                _dangTai = false;
                DatTrangThai();
            }
        }

        private void KhauTru_Changed(
            object sender, TextChangedEventArgs e)
        {
            if (txtHoanCoc == null || !_sanSang)
                return;

            TinhHoanCoc();
            DatTrangThai();
        }

        private void TinhHoanCoc()
        {
            txtHoanCoc.Text = DocKhauTru(out decimal khauTru)
                ? DinhDangTien(_tienCoc - khauTru)
                : "Khấu trừ phải từ 0 đến tiền cọc";
        }

        private async void Luu_Click(object sender, RoutedEventArgs e)
        {
            if (_dangLuu || !_sanSang || _tienNo > 0)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền thanh lý.";
                return;
            }

            if (!DocKhauTru(out decimal khauTru))
            {
                txtThongBao.Text =
                    "Khấu trừ phải là số nguyên từ 0 đến tiền cọc.";
                return;
            }

            string ghiChu = txtGhiChu.Text.Trim();

            if (ghiChu.Length > 500)
            {
                txtThongBao.Text = "Ghi chú tối đa 500 ký tự.";
                return;
            }

            if (khauTru > 0 && ghiChu.Length == 0)
            {
                txtThongBao.Text = "Vui lòng ghi rõ lý do khấu trừ tiền cọc.";
                return;
            }

            var xacNhan = MessageBox.Show(
                $"Thanh lý hợp đồng {_maHopDong}?\n"
                + $"Khấu trừ: {DinhDangTien(khauTru)}\n"
                + $"Tiền cần hoàn: {DinhDangTien(_tienCoc - khauTru)}",
                "Xác nhận thanh lý",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (xacNhan != MessageBoxResult.Yes)
                return;

            _dangLuu = true;
            DatTrangThai();
            txtThongBao.Text = "Đang thanh lý hợp đồng...";

            bool thanhCong = false;

            try
            {
                using var connection = Database.CreateConnection();
                await connection.OpenAsync();

                using var transaction =
                    connection.BeginTransaction(IsolationLevel.Serializable);

                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                command.CommandText = @"
                    DECLARE @Khoa VARCHAR(20);
                    DECLARE @Coc DECIMAL(18,0);
                    DECLARE @No DECIMAL(38,0);
                    DECLARE @HomNay DATE = CAST(GETDATE() AS DATE);

                    SELECT @Khoa = MaPhong
                    FROM dbo.PhongTro WITH (UPDLOCK, HOLDLOCK)
                    WHERE MaPhong = @MaPhong;

                    SET @Khoa = NULL;

                    SELECT @Khoa = MaKhachThue
                    FROM dbo.KhachThue WITH (UPDLOCK, HOLDLOCK)
                    WHERE MaKhachThue = @MaKhach;

                    SELECT @Coc = h.TienCoc
                    FROM dbo.HopDongThue h WITH (UPDLOCK, HOLDLOCK)
                    WHERE h.MaHopDong = @MaHopDong
                      AND h.MaPhong = @MaPhong
                      AND h.MaKhachThue = @MaKhach
                      AND h.TrangThai IN (N'Hiệu lực', N'Hết hạn')
                      AND h.NgayThanhLy IS NULL
                      AND h.NgayBatDau <= @HomNay;

                    IF @Coc IS NULL
                        THROW 50001,
                            N'Hợp đồng đã thay đổi hoặc không đủ điều kiện thanh lý.', 1;

                    IF @Coc <> @CocCu
                        THROW 50002,
                            N'Tiền cọc đã thay đổi. Hãy mở lại form.', 1;

                    IF NOT EXISTS (
                        SELECT 1 FROM dbo.PhongTro p
                        JOIN dbo.NhanVien nv ON nv.MaDayTro = p.MaDayTro
                        WHERE p.MaPhong = @MaPhong
                          AND nv.MaNguoiDung = @QuanLy
                          AND nv.ChucVu = N'Quản lý'
                          AND nv.TrangThaiLamViec = N'Đang làm'
                    )
                        THROW 50003,
                            N'Bạn không còn quyền quản lý hợp đồng.', 1;

                    SELECT @No = COALESCE(SUM(
                        CASE WHEN hd.TongTien > COALESCE(tt.DaTra, 0)
                             THEN hd.TongTien - COALESCE(tt.DaTra, 0)
                             ELSE 0 END
                    ), 0)
                    FROM dbo.HoaDon hd WITH (UPDLOCK, HOLDLOCK)
                    OUTER APPLY (
                        SELECT SUM(t.SoTien) AS DaTra
                        FROM dbo.ThanhToan t WITH (HOLDLOCK)
                        WHERE t.MaHoaDon = hd.MaHoaDon
                    ) tt
                    WHERE hd.MaHopDong = @MaHopDong;

                    IF @No > 0
                        THROW 50004,
                            N'Hợp đồng còn nợ hóa đơn. Cần thanh toán trước.', 1;

                    IF @KhauTru < 0 OR @KhauTru > @Coc
                        THROW 50005,
                            N'Số tiền khấu trừ không hợp lệ.', 1;

                    UPDATE dbo.HopDongThue
                    SET TrangThai = N'Đã thanh lý',
                        NgayThanhLy = @HomNay,
                        TienKhauTruCoc = @KhauTru,
                        TienHoanCoc = @Coc - @KhauTru,
                        GhiChuThanhLy = @GhiChu
                    WHERE MaHopDong = @MaHopDong;

                    UPDATE dbo.ThanhVienO
                    SET TrangThai = N'Đã rời đi'
                    WHERE MaHopDong = @MaHopDong
                      AND TrangThai = N'Đang ở';

                    UPDATE dbo.YeuCauTraPhong
                    SET TrangThai = N'Đã hoàn tất',
                        GhiChuXuLy = N'Đã thanh lý hợp đồng'
                    WHERE MaHopDong = @MaHopDong
                      AND TrangThai IN (N'Chờ xử lý', N'Đã duyệt');

                    -- Không giải phóng phòng có hợp đồng khác đang thuê.
                    -- Giữ nguyên phòng đang sửa chữa.
                    UPDATE p
                    SET p.TrangThai = N'Trống'
                    FROM dbo.PhongTro p
                    WHERE p.MaPhong = @MaPhong
                      AND p.TrangThai = N'Đang thuê'
                      AND NOT EXISTS (
                          SELECT 1 FROM dbo.HopDongThue h
                          WHERE h.MaPhong = p.MaPhong
                            AND h.TrangThai = N'Hiệu lực'
                            AND h.NgayBatDau <= @HomNay
                            AND h.NgayKetThuc >= @HomNay
                      );

                    UPDATE dbo.KhachThue
                    SET TrangThai = N'Đã rời đi'
                    WHERE MaKhachThue = @MaKhach
                      AND TrangThai = N'Đang thuê'
                      AND NOT EXISTS (
                          SELECT 1 FROM dbo.HopDongThue h
                          WHERE h.MaKhachThue = @MaKhach
                            AND h.TrangThai = N'Hiệu lực'
                            AND h.NgayBatDau <= @HomNay
                            AND h.NgayKetThuc >= @HomNay
                      );";

                command.Parameters.Add(
                    "@MaHopDong", SqlDbType.VarChar, 20)
                    .Value = _maHopDong;
                command.Parameters.Add(
                    "@MaPhong", SqlDbType.VarChar, 20)
                    .Value = _maPhong;
                command.Parameters.Add(
                    "@MaKhach", SqlDbType.VarChar, 20)
                    .Value = _maKhach;
                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;

                var coc = command.Parameters.Add(
                    "@CocCu", SqlDbType.Decimal);
                coc.Precision = 18;
                coc.Scale = 0;
                coc.Value = _tienCoc;

                var tru = command.Parameters.Add(
                    "@KhauTru", SqlDbType.Decimal);
                tru.Precision = 18;
                tru.Scale = 0;
                tru.Value = khauTru;

                command.Parameters.Add(
                    "@GhiChu", SqlDbType.NVarChar, 500)
                    .Value = ghiChu.Length == 0
                        ? (object)DBNull.Value
                        : ghiChu;

                await command.ExecuteNonQueryAsync();
                transaction.Commit();

                thanhCong = true;
                _sanSang = false;
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                txtThongBao.Text =
                    "Dữ liệu đang được người khác cập nhật. Vui lòng thử lại.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text = "Không thanh lý được: " + ex.Message;
            }
            finally
            {
                _dangLuu = false;
                DatTrangThai();
            }

            if (thanhCong)
            {
                MessageBox.Show(
                    "Thanh lý hợp đồng thành công.\n"
                    + "Tiền cọc cần hoàn: "
                    + DinhDangTien(_tienCoc - khauTru),
                    "Thông báo");

                DaLuu?.Invoke();
            }
        }

        private void DatTrangThai()
        {
            bool choPhep = _sanSang && !_dangTai && !_dangLuu;

            txtKhauTru.IsEnabled = choPhep;
            txtGhiChu.IsEnabled = choPhep;
            btnQuayLai.IsEnabled = !_dangLuu;

            btnLuu.IsEnabled =
                choPhep && _tienNo == 0 && DocKhauTru(out _);

            btnLuu.Content = _dangLuu
                ? "Đang lưu..."
                : "Xác nhận thanh lý";
        }

        private void QuayLai_Click(object sender, RoutedEventArgs e)
        {
            if (!_dangLuu)
                QuayLai?.Invoke();
        }
    }
}