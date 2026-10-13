using System;
using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class KhachThueFormView : UserControl
    {
        private const string VaiTroChu = "Người đứng tên hợp đồng";
        private const string VaiTroThanhVien = "Thành viên ở cùng";

        private readonly bool _dangSua;
        private readonly string _maKhach;
        private readonly string _maThanhVienMoi;
        private readonly string _tenDangNhapMoi;
        private readonly string _matKhauMoi;

        private bool _dangTai;
        private bool _dangTaiPhong;
        private bool _dangLuu;
        private bool _sanSang;
        private int _lanTaiPhong;

        public event Action? QuayLai;
        public event Action? DaLuu;

        private bool ThemThanhVien =>
            !_dangSua &&
            Convert.ToString(cboVaiTro.SelectedItem) == VaiTroThanhVien;

        // Alias k phải là bảng KhachThue.
        private const string DieuKienQuyen = @"
            (
                EXISTS (
                    SELECT 1
                    FROM dbo.NhanVien nv
                    WHERE nv.MaDayTro = k.MaDayTro
                      AND nv.MaNguoiDung = @NguoiQuanLy
                      AND nv.ChucVu = N'Quản lý'
                      AND nv.TrangThaiLamViec = N'Đang làm'
                )
                OR EXISTS (
                    SELECT 1
                    FROM dbo.HopDongThue h
                    JOIN dbo.PhongTro p ON p.MaPhong = h.MaPhong
                    JOIN dbo.NhanVien nv ON nv.MaDayTro = p.MaDayTro
                    WHERE h.MaKhachThue = k.MaKhachThue
                      AND nv.MaNguoiDung = @NguoiQuanLy
                      AND nv.ChucVu = N'Quản lý'
                      AND nv.TrangThaiLamViec = N'Đang làm'
                )
            )";

        public KhachThueFormView(DataRowView? khach = null)
        {
            InitializeComponent();

            _dangSua = khach != null;

            _maKhach = khach == null
                ? TaoMa("KT")
                : Convert.ToString(khach["MaKhachThue"]) ?? "";

            _maThanhVienMoi = TaoMa("TV");
            _tenDangNhapMoi = _maKhach.ToLowerInvariant();

            _matKhauMoi = _dangSua
                ? ""
                : Convert.ToBase64String(
                    RandomNumberGenerator.GetBytes(12));

            txtMaKhach.Text = _maKhach;
            txtTieuDe.Text = _dangSua
                ? "Cập nhật khách thuê"
                : "Thêm khách thuê";

            cboVaiTro.Items.Add(VaiTroChu);

            if (!_dangSua)
                cboVaiTro.Items.Add(VaiTroThanhVien);

            cboVaiTro.SelectedIndex = 0;

            HienThiPhongChu(
                "Chưa có phòng đang thuê",
                "Chưa có hợp đồng");

            txtHuongDan.Text =
                "Người đứng tên được tạo tài khoản tự động. "
                + "Phòng ở được cập nhật khi lập hợp đồng.";

            DatTrangThai();
        }

        private static string TaoMa(string tienTo)
        {
            return tienTo + Guid.NewGuid().ToString("N")
                .Substring(0, 20 - tienTo.Length)
                .ToUpperInvariant();
        }

        private void HienThiPhongChu(string tenPhong, string trangThai)
        {
            var table = new DataTable();
            table.Columns.Add("MaHopDong", typeof(string));
            table.Columns.Add("TenHienThi", typeof(string));
            table.Rows.Add("", tenPhong);

            cboPhong.ItemsSource = table.DefaultView;
            cboPhong.SelectedIndex = 0;
            txtTrangThai.Text = trangThai;
        }

        private void DatTrangThai()
        {
            if (pnlThongTin == null || btnLuu == null)
                return;

            bool choPhep =
                _sanSang && !_dangTai &&
                !_dangTaiPhong && !_dangLuu;

            pnlThongTin.IsEnabled = choPhep;

            cboDayTro.IsEnabled = !_dangSua;
            cboVaiTro.IsEnabled = !_dangSua;
            cboPhong.IsEnabled = ThemThanhVien;

            btnLuu.IsEnabled =
                choPhep &&
                (!ThemThanhVien ||
                 cboPhong.SelectedItem is DataRowView);

            btnQuayLai.IsEnabled = !_dangLuu;
            btnLuu.Content = _dangLuu ? "Đang lưu..." : "Lưu hồ sơ";
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai || _sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền quản lý khách thuê.";
                return;
            }

            _dangTai = true;
            DatTrangThai();
            txtThongBao.Text = "Đang tải thông tin...";

            try
            {
                using var connection = Database.CreateConnection();
                await connection.OpenAsync();

                var dayTro = new DataTable();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT d.MaDayTro, d.TenDayTro
                        FROM dbo.DayTro d
                        WHERE EXISTS (
                            SELECT 1
                            FROM dbo.NhanVien nv
                            WHERE nv.MaDayTro = d.MaDayTro
                              AND nv.MaNguoiDung = @NguoiQuanLy
                              AND nv.ChucVu = N'Quản lý'
                              AND nv.TrangThaiLamViec = N'Đang làm'
                        )
                        ORDER BY d.TenDayTro;";

                    command.Parameters.Add(
                        "@NguoiQuanLy", SqlDbType.VarChar, 20)
                        .Value = user.MaNguoiDung;

                    using var reader = await command.ExecuteReaderAsync();
                    dayTro.Load(reader);
                }

                if (!IsLoaded)
                    return;

                cboDayTro.ItemsSource = dayTro.DefaultView;

                if (dayTro.Rows.Count == 0)
                {
                    txtThongBao.Text = "Bạn chưa được phân công dãy trọ.";
                    return;
                }

                if (_dangSua)
                {
                    using var command = connection.CreateCommand();

                    command.CommandText = @"
                        SELECT
                            n.HoTen, n.CCCD, n.SoDienThoai, n.DiaChi,
                            COALESCE(k.MaDayTro, hd.MaDayTro) AS MaDayTro,
                            hd.MaHopDong,
                            hd.TenPhong,
                            hd.DangHieuLuc
                        FROM dbo.KhachThue k
                        JOIN dbo.NguoiDung n
                          ON n.MaNguoiDung = k.MaNguoiDung
                        OUTER APPLY (
                            SELECT TOP (1)
                                h.MaHopDong,
                                p.MaDayTro,
                                d.TenDayTro + N' - ' + p.SoPhong AS TenPhong,
                                CASE
                                    WHEN h.TrangThai = N'Hiệu lực'
                                     AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                                     AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                                    THEN 1 ELSE 0
                                END AS DangHieuLuc
                            FROM dbo.HopDongThue h
                            JOIN dbo.PhongTro p ON p.MaPhong = h.MaPhong
                            JOIN dbo.DayTro d ON d.MaDayTro = p.MaDayTro
                            WHERE h.MaKhachThue = k.MaKhachThue
                              AND EXISTS (
                                  SELECT 1
                                  FROM dbo.NhanVien nv
                                  WHERE nv.MaDayTro = p.MaDayTro
                                    AND nv.MaNguoiDung = @NguoiQuanLy
                                    AND nv.ChucVu = N'Quản lý'
                                    AND nv.TrangThaiLamViec = N'Đang làm'
                              )
                            ORDER BY
                                CASE
                                    WHEN h.TrangThai = N'Hiệu lực'
                                     AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                                     AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                                    THEN 0 ELSE 1
                                END,
                                h.NgayBatDau DESC,
                                h.MaHopDong
                        ) hd
                        WHERE k.MaKhachThue = @MaKhach
                          AND " + DieuKienQuyen + ";";

                    command.Parameters.Add(
                        "@MaKhach", SqlDbType.VarChar, 20)
                        .Value = _maKhach;

                    command.Parameters.Add(
                        "@NguoiQuanLy", SqlDbType.VarChar, 20)
                        .Value = user.MaNguoiDung;

                    using var reader = await command.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        txtThongBao.Text =
                            "Hồ sơ không tồn tại hoặc bạn không có quyền.";
                        return;
                    }

                    if (!IsLoaded)
                        return;

                    txtHoTen.Text = Convert.ToString(reader["HoTen"]) ?? "";
                    txtCCCD.Text = Convert.ToString(reader["CCCD"]) ?? "";
                    txtSoDienThoai.Text =
                        Convert.ToString(reader["SoDienThoai"]) ?? "";
                    txtDiaChi.Text = Convert.ToString(reader["DiaChi"]) ?? "";

                    cboDayTro.SelectedValue =
                        Convert.ToString(reader["MaDayTro"]);

                    bool dangThue =
                        reader["DangHieuLuc"] != DBNull.Value &&
                        Convert.ToInt32(reader["DangHieuLuc"]) == 1;

                    bool coHopDong = reader["MaHopDong"] != DBNull.Value;

                    HienThiPhongChu(
                        dangThue
                            ? Convert.ToString(reader["TenPhong"]) ?? ""
                            : "Chưa có phòng đang thuê",
                        dangThue
                            ? "Đang thuê"
                            : coHopDong
                                ? "Không có hợp đồng hiệu lực"
                                : "Chưa có hợp đồng");
                }
                else
                {
                    cboDayTro.SelectedIndex = 0;
                }

                _sanSang = true;
                txtThongBao.Text = "";
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

        private async void VaiTro_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            if (!_sanSang || _dangSua)
                return;

            txtThongBao.Text = "";

            pnlDiaChi.Visibility = ThemThanhVien
                ? Visibility.Collapsed
                : Visibility.Visible;

            txtMaKhach.Text = ThemThanhVien
                ? _maThanhVienMoi
                : _maKhach;

            if (ThemThanhVien)
            {
                txtHuongDan.Text =
                    "Chọn phòng đôi đang có người đứng tên hợp đồng. "
                    + "Mỗi phòng tối đa 2 người, gồm người đứng tên "
                    + "và 1 thành viên ở cùng. "
                    + "Thành viên được lưu vào hợp đồng đang có, "
                    + "không tạo hợp đồng hoặc tài khoản mới.";

                await TaiPhongOChungAsync();
            }
            else
            {
                _lanTaiPhong++;
                _dangTaiPhong = false;

                HienThiPhongChu(
                    "Phòng được chọn khi lập hợp đồng",
                    "Chưa có hợp đồng");

                txtHuongDan.Text =
                    "Người đứng tên được tạo tài khoản tự động. "
                    + "Phòng ở được cập nhật khi lập hợp đồng.";
            }

            DatTrangThai();
        }

        private async void DayTro_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            if (_sanSang && ThemThanhVien)
                await TaiPhongOChungAsync();
        }

        private void Phong_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            if (!_sanSang || !ThemThanhVien)
                return;

            txtTrangThai.Text = cboPhong.SelectedItem is DataRowView
                ? "Đang ở cùng"
                : "Chưa chọn phòng";

            DatTrangThai();
        }

        private async Task TaiPhongOChungAsync()
        {
            int lanTai = ++_lanTaiPhong;

            _dangTaiPhong = true;
            cboPhong.ItemsSource = null;
            txtTrangThai.Text = "Chưa chọn phòng";
            txtThongBao.Text = "Đang tải phòng đôi còn chỗ...";
            DatTrangThai();

            try
            {
                var user = UserSession.CurrentUser;

                if (user == null || user.VaiTro != "Nhân viên quản lý")
                    throw new InvalidOperationException(
                        "Bạn không có quyền quản lý.");

                string maDay = cboDayTro.SelectedValue?.ToString() ?? "";

                using var connection = Database.CreateConnection();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT
                        h.MaHopDong,
                        p.MaPhong,
                        d.TenDayTro + N' - ' + p.SoPhong
                        + N' | Người đứng tên: ' + n.HoTen AS TenHienThi
                    FROM dbo.HopDongThue h
                    JOIN dbo.PhongTro p ON p.MaPhong = h.MaPhong
                    JOIN dbo.DayTro d ON d.MaDayTro = p.MaDayTro
                    JOIN dbo.KhachThue k ON k.MaKhachThue = h.MaKhachThue
                    JOIN dbo.NguoiDung n ON n.MaNguoiDung = k.MaNguoiDung
                    WHERE p.MaDayTro = @MaDay
                      AND LTRIM(RTRIM(p.LoaiPhong)) = N'Phòng đôi'
                      AND h.TrangThai = N'Hiệu lực'
                      AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                      AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                      AND NOT EXISTS (
                          SELECT 1
                          FROM dbo.ThanhVienO tv
                          JOIN dbo.HopDongThue h2
                            ON h2.MaHopDong = tv.MaHopDong
                          WHERE h2.MaPhong = p.MaPhong
                            AND tv.TrangThai = N'Đang ở'
                            AND h2.TrangThai = N'Hiệu lực'
                            AND h2.NgayBatDau <= CAST(GETDATE() AS DATE)
                            AND h2.NgayKetThuc >= CAST(GETDATE() AS DATE)
                      )
                      AND EXISTS (
                          SELECT 1
                          FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @NguoiQuanLy
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      )
                    ORDER BY p.SoPhong;";

                command.Parameters.Add(
                    "@MaDay", SqlDbType.VarChar, 20).Value = maDay;

                command.Parameters.Add(
                    "@NguoiQuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;

                await connection.OpenAsync();

                using var reader = await command.ExecuteReaderAsync();

                var table = new DataTable();
                table.Load(reader);

                if (lanTai != _lanTaiPhong || !ThemThanhVien || !IsLoaded)
                    return;

                cboPhong.ItemsSource = table.DefaultView;

                if (table.Rows.Count > 0)
                {
                    cboPhong.SelectedIndex = 0;
                    txtThongBao.Text = "";
                }
                else
                {
                    txtThongBao.Text =
                        "Không có phòng đôi còn chỗ trong dãy này. "
                        + "Phòng phải có hợp đồng đang hiệu lực "
                        + "và chưa có thành viên ở cùng.";
                }
            }
            catch (Exception ex)
            {
                if (lanTai == _lanTaiPhong)
                    txtThongBao.Text =
                        "Không tải được phòng: " + ex.Message;
            }
            finally
            {
                if (lanTai == _lanTaiPhong)
                {
                    _dangTaiPhong = false;
                    DatTrangThai();
                }
            }
        }

        private bool KiemTraThongTin()
        {
            string hoTen = txtHoTen.Text.Trim();
            string cccd = txtCCCD.Text.Trim();
            string sdt = txtSoDienThoai.Text.Trim();

            if (hoTen.Length == 0 || hoTen.Length > 100)
            {
                txtThongBao.Text = "Họ tên phải có từ 1 đến 100 ký tự.";
                return false;
            }

            if (!Regex.IsMatch(cccd, @"^[0-9]{12}$"))
            {
                txtThongBao.Text = "CCCD phải gồm đúng 12 chữ số.";
                return false;
            }

            if (!Regex.IsMatch(sdt, @"^0[0-9]{9}$"))
            {
                txtThongBao.Text =
                    "Số điện thoại gồm 10 chữ số, bắt đầu bằng 0.";
                return false;
            }

            if (!ThemThanhVien)
            {
                string diaChi = txtDiaChi.Text.Trim();

                if (diaChi.Length == 0 || diaChi.Length > 255)
                {
                    txtThongBao.Text =
                        "Địa chỉ phải có từ 1 đến 255 ký tự.";
                    return false;
                }
            }

            if (!_dangSua && cboDayTro.SelectedValue == null)
            {
                txtThongBao.Text = "Vui lòng chọn dãy trọ.";
                return false;
            }

            if (ThemThanhVien &&
                cboPhong.SelectedItem is not DataRowView)
            {
                txtThongBao.Text = "Vui lòng chọn phòng đôi còn chỗ.";
                return false;
            }

            return true;
        }

        private async void Luu_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_sanSang || _dangTai || _dangTaiPhong || _dangLuu)
                return;

            if (!KiemTraThongTin())
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền lưu hồ sơ.";
                return;
            }

            bool laThanhVien = ThemThanhVien;
            bool thanhCong = false;

            _dangLuu = true;
            DatTrangThai();
            txtThongBao.Text = "Đang lưu hồ sơ...";

            try
            {
                if (laThanhVien)
                    await LuuThanhVienAsync(user.MaNguoiDung);
                else
                    await LuuKhachAsync(user.MaNguoiDung);

                thanhCong = true;
                _sanSang = false;
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                txtThongBao.Text =
                    "CCCD, số điện thoại hoặc tài khoản bị trùng.";
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                txtThongBao.Text =
                    "Dữ liệu đang được người khác cập nhật. "
                    + "Vui lòng thử lưu lại.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text = "Không lưu được hồ sơ: " + ex.Message;
            }
            finally
            {
                _dangLuu = false;
                DatTrangThai();
            }

            if (!thanhCong)
                return;

            string thongBao;

            if (laThanhVien)
            {
                thongBao =
                    "Đã thêm thành viên vào phòng đôi thành công.";
            }
            else if (_dangSua)
            {
                thongBao = "Cập nhật khách thuê thành công.";
            }
            else
            {
                thongBao =
                    "Thêm khách thuê thành công.\n\n"
                    + "Tên đăng nhập: " + _tenDangNhapMoi + "\n"
                    + "Mật khẩu: " + _matKhauMoi + "\n\n"
                    + "Hãy ghi lại thông tin tài khoản.";
            }

            MessageBox.Show(
                thongBao,
                "Thông báo",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DaLuu?.Invoke();
        }

        private async Task LuuKhachAsync(string maQuanLy)
        {
            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            if (_dangSua)
            {
                command.CommandText = @"
                    UPDATE n
                    SET n.HoTen = @HoTen,
                        n.CCCD = @CCCD,
                        n.SoDienThoai = @SDT,
                        n.DiaChi = @DiaChi
                    OUTPUT INSERTED.MaNguoiDung
                    FROM dbo.NguoiDung n
                    JOIN dbo.KhachThue k
                      ON k.MaNguoiDung = n.MaNguoiDung
                    WHERE k.MaKhachThue = @MaKhach
                      AND " + DieuKienQuyen + ";";
            }
            else
            {
                command.CommandText = @"
                    IF NOT EXISTS (
                        SELECT 1
                        FROM dbo.NhanVien nv
                        WHERE nv.MaDayTro = @MaDay
                          AND nv.MaNguoiDung = @NguoiQuanLy
                          AND nv.ChucVu = N'Quản lý'
                          AND nv.TrangThaiLamViec = N'Đang làm'
                    )
                        THROW 50001, N'Bạn không có quyền với dãy trọ này.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM dbo.ThanhVienO tv
                        JOIN dbo.HopDongThue h
                          ON h.MaHopDong = tv.MaHopDong
                        WHERE tv.CCCD = @CCCD
                          AND tv.TrangThai = N'Đang ở'
                          AND h.TrangThai = N'Hiệu lực'
                          AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                          AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                    )
                        THROW 50002,
                            N'Người này đang là thành viên ở cùng. Không tạo hồ sơ trùng.', 1;

                    INSERT INTO dbo.NguoiDung
                        (MaNguoiDung, TenDangNhap, MatKhau, HoTen,
                         CCCD, SoDienThoai, DiaChi, VaiTro)
                    VALUES
                        (@MaNguoiDung, @TenDangNhap, @MatKhau, @HoTen,
                         @CCCD, @SDT, @DiaChi, N'Khách thuê');

                    INSERT INTO dbo.KhachThue
                        (MaKhachThue, MaNguoiDung, MaDayTro)
                    VALUES
                        (@MaKhach, @MaNguoiDung, @MaDay);

                    SELECT @MaNguoiDung;";
            }

            command.Parameters.Add(
                "@NguoiQuanLy", SqlDbType.VarChar, 20).Value = maQuanLy;
            command.Parameters.Add(
                "@MaKhach", SqlDbType.VarChar, 20).Value = _maKhach;
            command.Parameters.Add(
                "@HoTen", SqlDbType.NVarChar, 100)
                .Value = txtHoTen.Text.Trim();
            command.Parameters.Add(
                "@CCCD", SqlDbType.VarChar, 12)
                .Value = txtCCCD.Text.Trim();
            command.Parameters.Add(
                "@SDT", SqlDbType.VarChar, 15)
                .Value = txtSoDienThoai.Text.Trim();
            command.Parameters.Add(
                "@DiaChi", SqlDbType.NVarChar, 255)
                .Value = txtDiaChi.Text.Trim();

            if (!_dangSua)
            {
                command.Parameters.Add(
                    "@MaNguoiDung", SqlDbType.VarChar, 20)
                    .Value = TaoMa("ND");
                command.Parameters.Add(
                    "@MaDay", SqlDbType.VarChar, 20)
                    .Value = cboDayTro.SelectedValue!.ToString()!;
                command.Parameters.Add(
                    "@TenDangNhap", SqlDbType.VarChar, 50)
                    .Value = _tenDangNhapMoi;
                command.Parameters.Add(
                    "@MatKhau", SqlDbType.VarChar, 255)
                    .Value = _matKhauMoi;
            }

            object? ketQua = await command.ExecuteScalarAsync();

            if (ketQua == null || ketQua == DBNull.Value)
                throw new InvalidOperationException(
                    "Hồ sơ không tồn tại hoặc bạn không còn quyền quản lý.");

            transaction.Commit();
        }

        private async Task LuuThanhVienAsync(string maQuanLy)
        {
            if (cboPhong.SelectedItem is not DataRowView phong)
                throw new InvalidOperationException("Chưa chọn phòng.");

            string maPhong = Convert.ToString(phong["MaPhong"]) ?? "";
            string maHopDong = Convert.ToString(phong["MaHopDong"]) ?? "";

            using var connection = Database.CreateConnection();
            await connection.OpenAsync();

            // Kiểm tra và thêm trong cùng giao dịch để tránh hai người
            // cùng thêm thành viên cuối cùng vào một phòng.
            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = @"
                IF NOT EXISTS (
                    SELECT 1
                    FROM dbo.PhongTro p WITH (UPDLOCK, HOLDLOCK)
                    WHERE p.MaPhong = @MaPhong
                      AND LTRIM(RTRIM(p.LoaiPhong)) = N'Phòng đôi'
                      AND EXISTS (
                          SELECT 1
                          FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @NguoiQuanLy
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      )
                )
                    THROW 50001,
                        N'Phòng không phải phòng đôi hoặc bạn không còn quyền quản lý.', 1;

                IF NOT EXISTS (
                    SELECT 1
                    FROM dbo.HopDongThue h WITH (UPDLOCK, HOLDLOCK)
                    WHERE h.MaHopDong = @MaHopDong
                      AND h.MaPhong = @MaPhong
                      AND h.TrangThai = N'Hiệu lực'
                      AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                      AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                )
                    THROW 50002, N'Hợp đồng không còn hiệu lực.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM dbo.ThanhVienO tv WITH (UPDLOCK, HOLDLOCK)
                    JOIN dbo.HopDongThue h ON h.MaHopDong = tv.MaHopDong
                    WHERE h.MaPhong = @MaPhong
                      AND tv.TrangThai = N'Đang ở'
                      AND h.TrangThai = N'Hiệu lực'
                      AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                      AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                )
                    THROW 50003,
                        N'Phòng đã đủ 2 người, không thể thêm thành viên.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM dbo.HopDongThue h
                    JOIN dbo.KhachThue k ON k.MaKhachThue = h.MaKhachThue
                    JOIN dbo.NguoiDung n ON n.MaNguoiDung = k.MaNguoiDung
                    WHERE n.CCCD = @CCCD
                      AND h.TrangThai = N'Hiệu lực'
                      AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                      AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                )
                    THROW 50004,
                        N'Người này đang đứng tên hợp đồng hiệu lực, không thể thêm trùng.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM dbo.ThanhVienO tv
                    JOIN dbo.HopDongThue h ON h.MaHopDong = tv.MaHopDong
                    WHERE tv.CCCD = @CCCD
                      AND tv.TrangThai = N'Đang ở'
                      AND h.TrangThai = N'Hiệu lực'
                      AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                      AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                )
                    THROW 50005,
                        N'Người này đã được ghi nhận đang ở trong một phòng.', 1;

                INSERT INTO dbo.ThanhVienO
                    (MaThanhVien, MaHopDong, HoTen, CCCD,
                     SoDienThoai, QuanHe, TrangThai)
                VALUES
                    (@MaThanhVien, @MaHopDong, @HoTen, @CCCD,
                     @SDT, N'Người ở cùng', N'Đang ở');";

            command.Parameters.Add(
                "@NguoiQuanLy", SqlDbType.VarChar, 20).Value = maQuanLy;
            command.Parameters.Add(
                "@MaPhong", SqlDbType.VarChar, 20).Value = maPhong;
            command.Parameters.Add(
                "@MaHopDong", SqlDbType.VarChar, 20).Value = maHopDong;
            command.Parameters.Add(
                "@MaThanhVien", SqlDbType.VarChar, 20)
                .Value = _maThanhVienMoi;
            command.Parameters.Add(
                "@HoTen", SqlDbType.NVarChar, 100)
                .Value = txtHoTen.Text.Trim();
            command.Parameters.Add(
                "@CCCD", SqlDbType.VarChar, 12)
                .Value = txtCCCD.Text.Trim();
            command.Parameters.Add(
                "@SDT", SqlDbType.VarChar, 15)
                .Value = txtSoDienThoai.Text.Trim();

            await command.ExecuteNonQueryAsync();
            transaction.Commit();
        }

        private void QuayLai_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangLuu)
                return;

            _lanTaiPhong++;
            QuayLai?.Invoke();
        }
    }
}