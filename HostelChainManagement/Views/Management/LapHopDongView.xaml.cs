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
    public partial class LapHopDongView : UserControl
    {
        private readonly string _maHopDong;
        private bool _dangTai;
        private bool _dangLuu;
        private bool _sanSang;

        public event Action? QuayLai;
        public event Action? DaLuu;

        public LapHopDongView()
        {
            InitializeComponent();

            _maHopDong = "HD" + Guid.NewGuid().ToString("N")
                .Substring(0, 18).ToUpperInvariant();

            txtMaHopDong.Text = _maHopDong;
            dpBatDau.SelectedDate = DateTime.Today;
            dpKetThuc.SelectedDate = DateTime.Today.AddYears(1);
            txtDaiDien.Text = UserSession.CurrentUser?.HoTen ?? "";

            DatTrangThai();
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai || _sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền lập hợp đồng.";
                return;
            }

            _dangTai = true;
            DatTrangThai();
            txtThongBao.Text = "Đang tải phòng và khách thuê...";

            try
            {
                using var connection = Database.CreateConnection();
                await connection.OpenAsync();

                var phong = new DataTable();
                var khach = new DataTable();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT p.MaPhong, p.GiaThue, d.TenDayTro,
                               d.TenDayTro + N' - Phòng ' + p.SoPhong
                                   AS TenHienThi
                        FROM dbo.PhongTro p
                        JOIN dbo.DayTro d ON d.MaDayTro = p.MaDayTro
                        WHERE p.TrangThai = N'Trống'
                          AND EXISTS (
                              SELECT 1 FROM dbo.NhanVien nv
                              WHERE nv.MaDayTro = p.MaDayTro
                                AND nv.MaNguoiDung = @QuanLy
                                AND nv.ChucVu = N'Quản lý'
                                AND nv.TrangThaiLamViec = N'Đang làm'
                          )
                        ORDER BY d.TenDayTro, p.SoPhong;";

                    command.Parameters.Add(
                        "@QuanLy", SqlDbType.VarChar, 20)
                        .Value = user.MaNguoiDung;

                    using var reader = await command.ExecuteReaderAsync();
                    phong.Load(reader);
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT k.MaKhachThue,
                               n.HoTen + N' - ' + n.CCCD AS TenHienThi
                        FROM dbo.KhachThue k
                        JOIN dbo.NguoiDung n
                          ON n.MaNguoiDung = k.MaNguoiDung
                        WHERE n.VaiTro = N'Khách thuê'
                          AND n.TrangThai = N'Hoạt động'
                          AND (
                              EXISTS (
                                  SELECT 1 FROM dbo.NhanVien nv
                                  WHERE nv.MaDayTro = k.MaDayTro
                                    AND nv.MaNguoiDung = @QuanLy
                                    AND nv.ChucVu = N'Quản lý'
                                    AND nv.TrangThaiLamViec = N'Đang làm'
                              )
                              OR EXISTS (
                                  SELECT 1
                                  FROM dbo.HopDongThue h
                                  JOIN dbo.PhongTro p ON p.MaPhong = h.MaPhong
                                  JOIN dbo.NhanVien nv
                                    ON nv.MaDayTro = p.MaDayTro
                                  WHERE h.MaKhachThue = k.MaKhachThue
                                    AND nv.MaNguoiDung = @QuanLy
                                    AND nv.ChucVu = N'Quản lý'
                                    AND nv.TrangThaiLamViec = N'Đang làm'
                              )
                          )
                        ORDER BY n.HoTen;";

                    command.Parameters.Add(
                        "@QuanLy", SqlDbType.VarChar, 20)
                        .Value = user.MaNguoiDung;

                    using var reader = await command.ExecuteReaderAsync();
                    khach.Load(reader);
                }

                if (!IsLoaded)
                    return;

                cboPhong.ItemsSource = phong.DefaultView;
                cboKhach.ItemsSource = khach.DefaultView;

                _sanSang = phong.Rows.Count > 0 && khach.Rows.Count > 0;

                txtThongBao.Text = _sanSang
                    ? ""
                    : "Cần có phòng trống và khách thuê thuộc phạm vi quản lý.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text = "Không tải được dữ liệu: " + ex.Message;
            }
            finally
            {
                _dangTai = false;
                DatTrangThai();
            }
        }

        private void Phong_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            if (txtDayTro == null || txtGiaThue == null)
                return;

            if (cboPhong.SelectedItem is DataRowView phong)
            {
                txtDayTro.Text = Convert.ToString(phong["TenDayTro"]);
                txtGiaThue.Text = Convert.ToDecimal(phong["GiaThue"])
                    .ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
            }
            else
            {
                txtDayTro.Clear();
                txtGiaThue.Clear();
            }
        }

        private async void Luu_Click(object sender, RoutedEventArgs e)
        {
            if (_dangLuu || !_sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền lập hợp đồng.";
                return;
            }

            string maPhong = cboPhong.SelectedValue?.ToString() ?? "";
            string maKhach = cboKhach.SelectedValue?.ToString() ?? "";
            string daiDien = txtDaiDien.Text.Trim();

            if (maPhong.Length == 0 || maKhach.Length == 0)
            {
                txtThongBao.Text = "Vui lòng chọn phòng và khách thuê.";
                return;
            }

            if (!dpBatDau.SelectedDate.HasValue ||
                !dpKetThuc.SelectedDate.HasValue)
            {
                txtThongBao.Text = "Vui lòng chọn đầy đủ ngày thuê.";
                return;
            }

            DateTime batDau = dpBatDau.SelectedDate.Value.Date;
            DateTime ketThuc = dpKetThuc.SelectedDate.Value.Date;

            if (ketThuc <= batDau || ketThuc < DateTime.Today)
            {
                txtThongBao.Text =
                    "Ngày kết thúc phải sau ngày bắt đầu và không trước hôm nay.";
                return;
            }

            if (!decimal.TryParse(
                    txtTienCoc.Text.Trim().Replace(',', '.'),
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out decimal tienCoc)
                || tienCoc < 0
                || tienCoc > 9999999999999999.99m
                || decimal.Round(tienCoc, 2) != tienCoc)
            {
                txtThongBao.Text =
                    "Tiền cọc phải từ 0 trở lên, tối đa 2 số thập phân. "
                    + "Không nhập dấu phân cách hàng nghìn.";
                return;
            }

            if (!int.TryParse(txtKyThanhToan.Text.Trim(), out int ky)
                || ky < 1 || ky > 31)
            {
                txtThongBao.Text = "Ngày thanh toán phải từ 1 đến 31.";
                return;
            }

            if (daiDien.Length == 0 || daiDien.Length > 100)
            {
                txtThongBao.Text =
                    "Đại diện bên cho thuê phải có từ 1 đến 100 ký tự.";
                return;
            }

            _dangLuu = true;
            DatTrangThai();
            txtThongBao.Text = "Đang tạo hợp đồng...";

            bool thanhCong = false;

            try
            {
                using var connection = Database.CreateConnection();
                await connection.OpenAsync();

                // Khóa kiểm tra và ghi trong cùng transaction.
                using var transaction =
                    connection.BeginTransaction(IsolationLevel.Serializable);

                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                command.CommandText = @"
                    DECLARE @MaDay VARCHAR(20);
                    DECLARE @GiaThue DECIMAL(18,2);
                    DECLARE @TrangThaiPhong NVARCHAR(50);
                    DECLARE @TaiKhoanKhach VARCHAR(20);

                    SELECT @MaDay = p.MaDayTro,
                           @GiaThue = p.GiaThue,
                           @TrangThaiPhong = p.TrangThai
                    FROM dbo.PhongTro p WITH (UPDLOCK, HOLDLOCK)
                    WHERE p.MaPhong = @MaPhong;

                    IF @MaDay IS NULL OR @TrangThaiPhong <> N'Trống'
                        THROW 50001, N'Phòng không còn trống.', 1;

                    IF @GiaThue IS NULL OR @GiaThue <= 0
                        THROW 50002, N'Giá thuê phòng không hợp lệ.', 1;

                    IF NOT EXISTS (
                        SELECT 1 FROM dbo.NhanVien nv
                        WHERE nv.MaDayTro = @MaDay
                          AND nv.MaNguoiDung = @QuanLy
                          AND nv.ChucVu = N'Quản lý'
                          AND nv.TrangThaiLamViec = N'Đang làm'
                    )
                        THROW 50003, N'Bạn không có quyền quản lý phòng này.', 1;

                    SELECT @TaiKhoanKhach = k.MaNguoiDung
                    FROM dbo.KhachThue k WITH (UPDLOCK, HOLDLOCK)
                    WHERE k.MaKhachThue = @MaKhach;

                    IF @TaiKhoanKhach IS NULL
                        THROW 50004, N'Khách thuê không còn tồn tại.', 1;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM dbo.KhachThue k
                        JOIN dbo.NguoiDung n
                          ON n.MaNguoiDung = k.MaNguoiDung
                        WHERE k.MaKhachThue = @MaKhach
                          AND n.VaiTro = N'Khách thuê'
                          AND n.TrangThai = N'Hoạt động'
                          AND (
                              EXISTS (
                                  SELECT 1 FROM dbo.NhanVien nv
                                  WHERE nv.MaDayTro = k.MaDayTro
                                    AND nv.MaNguoiDung = @QuanLy
                                    AND nv.ChucVu = N'Quản lý'
                                    AND nv.TrangThaiLamViec = N'Đang làm'
                              )
                              OR EXISTS (
                                  SELECT 1
                                  FROM dbo.HopDongThue h
                                  JOIN dbo.PhongTro p ON p.MaPhong = h.MaPhong
                                  JOIN dbo.NhanVien nv
                                    ON nv.MaDayTro = p.MaDayTro
                                  WHERE h.MaKhachThue = k.MaKhachThue
                                    AND nv.MaNguoiDung = @QuanLy
                                    AND nv.ChucVu = N'Quản lý'
                                    AND nv.TrangThaiLamViec = N'Đang làm'
                              )
                          )
                    )
                        THROW 50005, N'Bạn không có quyền với khách thuê này.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM dbo.HopDongThue h WITH (UPDLOCK, HOLDLOCK)
                        WHERE h.TrangThai IN (N'Hiệu lực', N'Yêu cầu trả phòng')
                          AND (h.MaPhong = @MaPhong
                               OR h.MaKhachThue = @MaKhach)
                          AND h.NgayBatDau <= @KetThuc
                          AND h.NgayKetThuc >= @BatDau
                    )
                        THROW 50006,
                            N'Phòng hoặc khách thuê đã có hợp đồng trùng thời gian.', 1;

                    INSERT INTO dbo.HopDongThue
                        (MaHopDong, MaPhong, MaKhachThue,
                         DaiDienBenChoThue, NgayBatDau, NgayKetThuc,
                         TienCoc, GiaThue, KyThanhToan, DieuKhoan, TrangThai)
                    VALUES
                        (@MaHopDong, @MaPhong, @MaKhach,
                         @DaiDien, @BatDau, @KetThuc,
                         @TienCoc, @GiaThue, @Ky, @DieuKhoan, N'Hiệu lực');

                    IF @BatDau <= CAST(GETDATE() AS DATE)
                       AND @KetThuc >= CAST(GETDATE() AS DATE)
                    BEGIN
                        UPDATE dbo.PhongTro
                        SET TrangThai = N'Đang thuê'
                        WHERE MaPhong = @MaPhong;
                    END;";

                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;
                command.Parameters.Add(
                    "@MaHopDong", SqlDbType.VarChar, 20)
                    .Value = _maHopDong;
                command.Parameters.Add(
                    "@MaPhong", SqlDbType.VarChar, 20).Value = maPhong;
                command.Parameters.Add(
                    "@MaKhach", SqlDbType.VarChar, 20).Value = maKhach;
                command.Parameters.Add(
                    "@DaiDien", SqlDbType.NVarChar, 100).Value = daiDien;
                command.Parameters.Add(
                    "@BatDau", SqlDbType.Date).Value = batDau;
                command.Parameters.Add(
                    "@KetThuc", SqlDbType.Date).Value = ketThuc;
                command.Parameters.Add(
                    "@Ky", SqlDbType.Int).Value = ky;

                var thamSoCoc = command.Parameters.Add(
                    "@TienCoc", SqlDbType.Decimal);
                thamSoCoc.Precision = 18;
                thamSoCoc.Scale = 2;
                thamSoCoc.Value = tienCoc;

                command.Parameters.Add(
                    "@DieuKhoan", SqlDbType.NVarChar, -1)
                    .Value = string.IsNullOrWhiteSpace(txtDieuKhoan.Text)
                        ? (object)DBNull.Value
                        : txtDieuKhoan.Text.Trim();

                await command.ExecuteNonQueryAsync();
                transaction.Commit();
                thanhCong = true;
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                txtThongBao.Text =
                    "Dữ liệu đang được người khác cập nhật. Vui lòng thử lại.";
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                txtThongBao.Text =
                    "Mã hợp đồng hoặc dữ liệu bị trùng. "
                    + "Quay lại và mở form mới.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text = "Không tạo được hợp đồng: " + ex.Message;
            }
            finally
            {
                _dangLuu = false;
                DatTrangThai();
            }

            if (thanhCong)
            {
                MessageBox.Show(
                    "Tạo hợp đồng thuê thành công.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DaLuu?.Invoke();
            }
        }

        private void DatTrangThai()
        {
            bool choPhep = _sanSang && !_dangTai && !_dangLuu;

            pnlThongTin.IsEnabled = choPhep;
            btnLuu.IsEnabled = choPhep;
            btnQuayLai.IsEnabled = !_dangLuu;
            btnLuu.Content = _dangLuu
                ? "Đang lưu..."
                : "Xác nhận tạo hợp đồng";
        }

        private void QuayLai_Click(object sender, RoutedEventArgs e)
        {
            if (!_dangLuu)
                QuayLai?.Invoke();
        }
    }
}