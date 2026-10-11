using System;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class ThemPhongWindow : Window
    {
        private bool _dangLuu;
        private bool _coDayTro;

        private readonly string? _maPhongCanSua;
        private string? _maDayTroCanSua;
        private bool _phongDangThue;

        private bool LaCheDoSua => _maPhongCanSua != null;

        // Mở cửa sổ thêm phòng.
        public ThemPhongWindow()
        {
            InitializeComponent();

            pnlThongTin.IsEnabled = false;
            btnSave.IsEnabled = false;

            Loaded += Window_Loaded;
            Closing += Window_Closing;
        }

        // Mở cửa sổ sửa phòng.
        public ThemPhongWindow(DataRowView phong) : this()
        {
            _maPhongCanSua =
                Convert.ToString(phong["MaPhong"]);

            Title = "Sửa phòng trọ";
            txtTieuDe.Text = "Sửa phòng trọ";
            txtMoTa.Text = "Cập nhật thông tin phòng đã chọn.";
            btnSave.Content = "Lưu thay đổi";

            txtMaPhong.Text = _maPhongCanSua;

            // Khi sửa, giữ nguyên mã, số phòng và dãy trọ.
            txtMaPhong.IsReadOnly = true;
            txtSoPhong.IsReadOnly = true;
            cboDayTro.IsEnabled = false;
        }

        private async void Window_Loaded(
            object sender, RoutedEventArgs e)
        {
            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtStatus.Text =
                    "Bạn không có quyền quản lý phòng.";
                return;
            }

            txtStatus.Text = "Đang tải thông tin...";

            try
            {
                using var connection = Database.CreateConnection();
                await connection.OpenAsync();

                var table = new DataTable();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT d.MaDayTro, d.TenDayTro
                        FROM dbo.DayTro AS d
                        WHERE EXISTS (
                            SELECT 1
                            FROM dbo.NhanVien AS nv
                            WHERE nv.MaDayTro = d.MaDayTro
                              AND nv.MaNguoiDung = @MaNguoiDung
                              AND nv.ChucVu = N'Quản lý'
                              AND nv.TrangThaiLamViec = N'Đang làm'
                        )
                        ORDER BY d.TenDayTro;";

                    command.Parameters.Add(
                        "@MaNguoiDung", SqlDbType.VarChar, 20)
                        .Value = user.MaNguoiDung;

                    using var reader =
                        await command.ExecuteReaderAsync();

                    table.Load(reader);
                }

                if (!IsVisible)
                    return;

                cboDayTro.ItemsSource = table.DefaultView;

                if (table.Rows.Count == 0)
                {
                    txtStatus.Text =
                        "Tài khoản chưa được phân công quản lý dãy trọ.";
                    return;
                }

                if (LaCheDoSua)
                {
                    // Đọc lại phòng từ SQL để lấy thông tin hiện tại.
                    using var command = connection.CreateCommand();

                    command.CommandText = @"
                        SELECT p.*
                        FROM dbo.PhongTro AS p
                        WHERE p.MaPhong = @MaPhong
                          AND EXISTS (
                              SELECT 1
                              FROM dbo.NhanVien AS nv
                              WHERE nv.MaDayTro = p.MaDayTro
                                AND nv.MaNguoiDung = @MaNguoiDung
                                AND nv.ChucVu = N'Quản lý'
                                AND nv.TrangThaiLamViec = N'Đang làm'
                          );";

                    command.Parameters.Add(
                        "@MaPhong", SqlDbType.VarChar, 20)
                        .Value = _maPhongCanSua!;

                    command.Parameters.Add(
                        "@MaNguoiDung", SqlDbType.VarChar, 20)
                        .Value = user.MaNguoiDung;

                    using var reader =
                        await command.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        txtStatus.Text =
                            "Phòng không còn tồn tại hoặc bạn không có quyền sửa.";
                        return;
                    }

                    if (!IsVisible)
                        return;

                    _maDayTroCanSua =
                        Convert.ToString(reader["MaDayTro"]);

                    cboDayTro.SelectedValue = _maDayTroCanSua;

                    txtSoPhong.Text =
                        Convert.ToString(reader["SoPhong"]);

                    cboLoaiPhong.Text =
                        Convert.ToString(reader["LoaiPhong"]);

                    txtDienTich.Text =
                        Convert.ToDecimal(reader["DienTich"])
                        .ToString("0.##", CultureInfo.InvariantCulture);

                    txtGiaThue.Text =
                        Convert.ToDecimal(reader["GiaThue"])
                        .ToString("0.##", CultureInfo.InvariantCulture);

                    string trangThai =
                        Convert.ToString(reader["TrangThai"]) ?? "";

                    _phongDangThue = trangThai == "Đang thuê";

                    if (_phongDangThue)
                    {
                        cboTrangThai.Items.Add(
                            new ComboBoxItem
                            {
                                Content = "Đang thuê"
                            });
                    }

                    cboTrangThai.SelectedIndex = -1;

                    foreach (ComboBoxItem item in cboTrangThai.Items)
                    {
                        if (item.Content?.ToString() == trangThai)
                        {
                            cboTrangThai.SelectedItem = item;
                            break;
                        }
                    }

                    cboTrangThai.IsEnabled = !_phongDangThue;
                }
                else
                {
                    cboDayTro.SelectedIndex = 0;
                }

                _coDayTro = cboDayTro.SelectedValue != null;

                DatTrangThaiLuu(false);

                txtStatus.Text = _coDayTro
                    ? ""
                    : "Không xác định được dãy trọ của phòng.";

                if (LaCheDoSua)
                    txtDienTich.Focus();
                else
                    txtMaPhong.Focus();
            }
            catch (Exception)
            {
                _coDayTro = false;
                DatTrangThaiLuu(false);

                if (IsVisible)
                {
                    txtStatus.Text =
                        "Không tải được thông tin. Kiểm tra SQL Server.";
                }
            }
        }

        private async void Save_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangLuu || !_coDayTro)
                return;

            txtStatus.Text = "";

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtStatus.Text =
                    "Bạn không có quyền quản lý phòng.";
                return;
            }

            string maDayTro = LaCheDoSua
                ? _maDayTroCanSua ?? ""
                : cboDayTro.SelectedValue?.ToString() ?? "";

            string maPhong = LaCheDoSua
                ? _maPhongCanSua!
                : txtMaPhong.Text.Trim();

            string soPhong = txtSoPhong.Text.Trim();
            string loaiPhong = cboLoaiPhong.Text.Trim();

            string trangThai =
                (cboTrangThai.SelectedItem as ComboBoxItem)
                ?.Content?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(maDayTro))
            {
                txtStatus.Text = "Vui lòng chọn dãy trọ.";
                return;
            }

            if (string.IsNullOrWhiteSpace(maPhong) ||
                maPhong.Length > 20)
            {
                txtStatus.Text =
                    "Mã phòng phải có từ 1 đến 20 ký tự.";
                return;
            }

            // Quy tắc mã áp dụng khi thêm phòng mới.
            if (!LaCheDoSua)
            {
                foreach (char kyTu in maPhong)
                {
                    bool hopLe =
                        (kyTu >= 'A' && kyTu <= 'Z') ||
                        (kyTu >= 'a' && kyTu <= 'z') ||
                        (kyTu >= '0' && kyTu <= '9') ||
                        kyTu == '_' || kyTu == '-';

                    if (!hopLe)
                    {
                        txtStatus.Text =
                            "Mã phòng chỉ gồm chữ không dấu, số, dấu _ hoặc -.";
                        return;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(soPhong) ||
                soPhong.Length > 20)
            {
                txtStatus.Text =
                    "Số phòng phải có từ 1 đến 20 ký tự.";
                return;
            }

            if (loaiPhong.Length > 50)
            {
                txtStatus.Text =
                    "Loại phòng không được quá 50 ký tự.";
                return;
            }

            string chuoiDienTich =
                txtDienTich.Text.Trim().Replace(',', '.');

            if (!decimal.TryParse(
                    chuoiDienTich,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out decimal dienTich)
                || dienTich <= 0
                || dienTich > 99999999.99m
                || decimal.Round(dienTich, 2) != dienTich)
            {
                txtStatus.Text =
                    "Diện tích phải lớn hơn 0, tối đa 2 số thập phân. "
                    + "Ví dụ: 25 hoặc 25,5.";

                txtDienTich.Focus();
                return;
            }

            string chuoiGiaThue =
                txtGiaThue.Text.Trim().Replace(',', '.');

            if (!decimal.TryParse(
                    chuoiGiaThue,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out decimal giaThue)
                || giaThue <= 0
                || giaThue > 9999999999999999.99m
                || decimal.Round(giaThue, 2) != giaThue)
            {
                txtStatus.Text =
                    "Giá thuê phải lớn hơn 0, tối đa 2 số thập phân. "
                    + "Không nhập dấu phân cách hàng nghìn. Ví dụ: 3000000.";

                txtGiaThue.Focus();
                return;
            }

            bool trangThaiHopLe =
                trangThai == "Trống"
                || trangThai == "Đang sửa chữa"
                || (LaCheDoSua
                    && _phongDangThue
                    && trangThai == "Đang thuê");

            if (!trangThaiHopLe)
            {
                txtStatus.Text =
                    "Vui lòng chọn trạng thái hợp lệ.";
                return;
            }

            DatTrangThaiLuu(true);
            txtStatus.Text = "Đang lưu thông tin phòng...";

            bool luuThanhCong = false;

            try
            {
                using var connection = Database.CreateConnection();
                using var command = connection.CreateCommand();

                if (LaCheDoSua)
                {
                    command.CommandText = @"
                        UPDATE p
                        SET p.LoaiPhong = @LoaiPhong,
                            p.DienTich = @DienTich,
                            p.GiaThue = @GiaThue,
                            p.TrangThai = CASE
                                WHEN p.TrangThai = N'Đang thuê'
                                     OR @TrangThai = N'Đang thuê'
                                    THEN p.TrangThai
                                ELSE @TrangThai
                            END
                        FROM dbo.PhongTro AS p
                        WHERE p.MaPhong = @MaPhong
                          AND p.MaDayTro = @MaDayTro
                          AND EXISTS (
                              SELECT 1
                              FROM dbo.NhanVien AS nv
                              WHERE nv.MaDayTro = p.MaDayTro
                                AND nv.MaNguoiDung = @MaNguoiDung
                                AND nv.ChucVu = N'Quản lý'
                                AND nv.TrangThaiLamViec = N'Đang làm'
                          );";
                }
                else
                {
                    command.CommandText = @"
                        INSERT INTO dbo.PhongTro
                            (MaPhong, MaDayTro, SoPhong, LoaiPhong,
                             DienTich, GiaThue, TrangThai)
                        SELECT
                            @MaPhong, @MaDayTro, @SoPhong, @LoaiPhong,
                            @DienTich, @GiaThue, @TrangThai
                        WHERE EXISTS (
                            SELECT 1
                            FROM dbo.NhanVien AS nv
                            WHERE nv.MaNguoiDung = @MaNguoiDung
                              AND nv.MaDayTro = @MaDayTro
                              AND nv.ChucVu = N'Quản lý'
                              AND nv.TrangThaiLamViec = N'Đang làm'
                        );";
                }

                command.Parameters.Add(
                    "@MaNguoiDung", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;

                command.Parameters.Add(
                    "@MaPhong", SqlDbType.VarChar, 20)
                    .Value = maPhong;

                command.Parameters.Add(
                    "@MaDayTro", SqlDbType.VarChar, 20)
                    .Value = maDayTro;

                command.Parameters.Add(
                    "@SoPhong", SqlDbType.NVarChar, 20)
                    .Value = soPhong;

                command.Parameters.Add(
                    "@LoaiPhong", SqlDbType.NVarChar, 50)
                    .Value = string.IsNullOrWhiteSpace(loaiPhong)
                        ? (object)DBNull.Value
                        : loaiPhong;

                var thamSoDienTich = command.Parameters.Add(
                    "@DienTich", SqlDbType.Decimal);

                thamSoDienTich.Precision = 10;
                thamSoDienTich.Scale = 2;
                thamSoDienTich.Value = dienTich;

                var thamSoGiaThue = command.Parameters.Add(
                    "@GiaThue", SqlDbType.Decimal);

                thamSoGiaThue.Precision = 18;
                thamSoGiaThue.Scale = 2;
                thamSoGiaThue.Value = giaThue;

                command.Parameters.Add(
                    "@TrangThai", SqlDbType.NVarChar, 50)
                    .Value = trangThai;

                await connection.OpenAsync();

                int soDong =
                    await command.ExecuteNonQueryAsync();

                luuThanhCong = soDong == 1;

                if (!luuThanhCong)
                {
                    txtStatus.Text =
                        "Phòng đã bị xóa, đổi dãy hoặc bạn không còn quyền quản lý.";
                }
            }
            catch (SqlException ex)
                when (ex.Number is 2601 or 2627)
            {
                txtStatus.Text =
                    "Mã phòng đã tồn tại hoặc số phòng bị trùng trong dãy.";
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không lưu được phòng. Kiểm tra kết nối và cấu trúc database.";
            }
            catch (Exception)
            {
                txtStatus.Text =
                    "Có lỗi khi lưu phòng. Vui lòng thử lại.";
            }
            finally
            {
                DatTrangThaiLuu(false);
            }

            if (luuThanhCong)
            {
                MessageBox.Show(
                    this,
                    LaCheDoSua
                        ? "Cập nhật phòng trọ thành công!"
                        : "Thêm phòng trọ thành công!",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
            }
        }

        private void DatTrangThaiLuu(bool dangLuu)
        {
            _dangLuu = dangLuu;

            pnlThongTin.IsEnabled = !dangLuu && _coDayTro;
            btnSave.IsEnabled = !dangLuu && _coDayTro;
            btnHuy.IsEnabled = !dangLuu;

            btnSave.Content = dangLuu
                ? "Đang lưu..."
                : (LaCheDoSua ? "Lưu thay đổi" : "Lưu phòng");
        }

        private void Huy_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_dangLuu)
                DialogResult = false;
        }

        private void Window_Closing(
            object? sender, CancelEventArgs e)
        {
            if (_dangLuu)
                e.Cancel = true;
        }
    }
}