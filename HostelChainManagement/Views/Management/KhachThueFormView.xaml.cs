using System;
using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class KhachThueFormView : UserControl
    {
        private readonly bool _dangSua;
        private readonly string _maKhach;
        private readonly string _tenDangNhapMoi;
        private readonly string _matKhauMoi;

        private bool _dangLuu;
        private bool _dangTai;
        private bool _sanSang;

        public event Action? QuayLai;
        public event Action? DaLuu;

        // Dùng cho truy vấn có alias k = KhachThue.
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

            // Tạo thông tin tài khoản bằng biến C#, không dùng ô ẩn.
            _tenDangNhapMoi = _maKhach.ToLowerInvariant();
            _matKhauMoi = _dangSua
                ? ""
                : Convert.ToBase64String(
                    RandomNumberGenerator.GetBytes(12));

            txtMaKhach.Text = _maKhach;

            if (_dangSua)
                txtTieuDe.Text = "Cập nhật khách thuê";

            HienThiPhong("Chưa có phòng đang thuê", "Chưa có hợp đồng");
            DatTrangThai();
        }

        private static string TaoMa(string tienTo)
        {
            return tienTo + Guid.NewGuid().ToString("N")
                .Substring(0, 20 - tienTo.Length)
                .ToUpperInvariant();
        }

        private void HienThiPhong(string phong, string trangThai)
        {
            cboPhong.Items.Clear();
            cboPhong.Items.Add(phong);
            cboPhong.SelectedIndex = 0;

            cboVaiTro.Items.Clear();
            cboVaiTro.Items.Add(
                trangThai == "Đang thuê"
                    ? "Người đứng tên hợp đồng"
                    : "Chưa có vai trò ở hiện tại");
            cboVaiTro.SelectedIndex = 0;

            txtTrangThai.Text = trangThai;
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

                var table = new DataTable();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT d.MaDayTro, d.TenDayTro
                        FROM dbo.DayTro d
                        WHERE EXISTS (
                            SELECT 1 FROM dbo.NhanVien nv
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
                    table.Load(reader);
                }

                if (!IsLoaded)
                    return;

                cboDayTro.ItemsSource = table.DefaultView;

                if (table.Rows.Count == 0)
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
                            k.MaDayTro,
                            hd.MaHopDong,
                            hd.DangHieuLuc,
                            hd.TenPhong
                        FROM dbo.KhachThue k
                        JOIN dbo.NguoiDung n
                          ON n.MaNguoiDung = k.MaNguoiDung

                        OUTER APPLY (
                            SELECT TOP (1)
                                h.MaHopDong,
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
                                  SELECT 1 FROM dbo.NhanVien nv
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
                            "Khách không tồn tại hoặc bạn không có quyền cập nhật.";
                        return;
                    }

                    if (!IsLoaded)
                        return;

                    txtHoTen.Text = Convert.ToString(reader["HoTen"]);
                    txtCCCD.Text = Convert.ToString(reader["CCCD"]);
                    txtSoDienThoai.Text =
                        Convert.ToString(reader["SoDienThoai"]);
                    txtDiaChi.Text = Convert.ToString(reader["DiaChi"]);

                    cboDayTro.SelectedValue =
                        Convert.ToString(reader["MaDayTro"]);

                    cboDayTro.IsEnabled = false;

                    bool dangThue =
                        reader["DangHieuLuc"] != DBNull.Value
                        && Convert.ToInt32(reader["DangHieuLuc"]) == 1;

                    bool coHopDong = reader["MaHopDong"] != DBNull.Value;

                    HienThiPhong(
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
                _sanSang = false;
                txtThongBao.Text =
                    "Không tải được thông tin: " + ex.Message;
            }
            finally
            {
                _dangTai = false;
                DatTrangThai();
            }
        }

        private async void Luu_Click(object sender, RoutedEventArgs e)
        {
            if (_dangLuu || !_sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền lưu hồ sơ.";
                return;
            }

            string hoTen = txtHoTen.Text.Trim();
            string cccd = txtCCCD.Text.Trim();
            string sdt = txtSoDienThoai.Text.Trim();
            string diaChi = txtDiaChi.Text.Trim();
            string maDay = cboDayTro.SelectedValue?.ToString() ?? "";

            if (hoTen.Length == 0 || hoTen.Length > 100)
            {
                txtThongBao.Text = "Họ tên phải có từ 1 đến 100 ký tự.";
                return;
            }

            if (!Regex.IsMatch(cccd, @"^[0-9]{12}$"))
            {
                txtThongBao.Text = "CCCD phải gồm đúng 12 chữ số.";
                return;
            }

            if (!Regex.IsMatch(sdt, @"^0[0-9]{9}$"))
            {
                txtThongBao.Text =
                    "Số điện thoại phải có 10 chữ số, bắt đầu bằng 0.";
                return;
            }

            if (diaChi.Length == 0 || diaChi.Length > 255)
            {
                txtThongBao.Text = "Địa chỉ phải có từ 1 đến 255 ký tự.";
                return;
            }

            if (!_dangSua && maDay.Length == 0)
            {
                txtThongBao.Text = "Vui lòng chọn dãy quản lý hồ sơ.";
                return;
            }

            _dangLuu = true;
            DatTrangThai();
            txtThongBao.Text = "Đang lưu hồ sơ...";

            bool thanhCong = false;

            try
            {
                using var connection = Database.CreateConnection();
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();
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
                            SELECT 1 FROM dbo.NhanVien nv
                            WHERE nv.MaDayTro = @MaDay
                              AND nv.MaNguoiDung = @NguoiQuanLy
                              AND nv.ChucVu = N'Quản lý'
                              AND nv.TrangThaiLamViec = N'Đang làm'
                        )
                            THROW 50001,
                                N'Bạn không có quyền với dãy trọ này.', 1;

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
                    "@NguoiQuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;
                command.Parameters.Add(
                    "@MaKhach", SqlDbType.VarChar, 20)
                    .Value = _maKhach;
                command.Parameters.Add(
                    "@HoTen", SqlDbType.NVarChar, 100)
                    .Value = hoTen;
                command.Parameters.Add(
                    "@CCCD", SqlDbType.VarChar, 12)
                    .Value = cccd;
                command.Parameters.Add(
                    "@SDT", SqlDbType.VarChar, 15)
                    .Value = sdt;
                command.Parameters.Add(
                    "@DiaChi", SqlDbType.NVarChar, 255)
                    .Value = diaChi;

                if (!_dangSua)
                {
                    command.Parameters.Add(
                        "@MaNguoiDung", SqlDbType.VarChar, 20)
                        .Value = TaoMa("ND");
                    command.Parameters.Add(
                        "@MaDay", SqlDbType.VarChar, 20)
                        .Value = maDay;
                    command.Parameters.Add(
                        "@TenDangNhap", SqlDbType.VarChar, 50)
                        .Value = _tenDangNhapMoi;
                    command.Parameters.Add(
                        "@MatKhau", SqlDbType.VarChar, 255)
                        .Value = _matKhauMoi;
                }

                object? ketQua = await command.ExecuteScalarAsync();

                if (ketQua == null || ketQua == DBNull.Value)
                {
                    transaction.Rollback();

                    txtThongBao.Text =
                        "Không cập nhật được: hồ sơ không tồn tại "
                        + "hoặc bạn không còn quyền quản lý.";
                }
                else
                {
                    transaction.Commit();
                    thanhCong = true;
                }
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                txtThongBao.Text =
                    "CCCD, số điện thoại hoặc thông tin tài khoản "
                    + "bị trùng với dữ liệu đã có.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không lưu được hồ sơ: " + ex.Message;
            }
            finally
            {
                _dangLuu = false;
                DatTrangThai();
            }

            if (thanhCong)
            {
                string thongBao = _dangSua
                    ? "Cập nhật khách thuê thành công."
                    : "Thêm khách thuê thành công.\n\n"
                      + "Tên đăng nhập: " + _tenDangNhapMoi + "\n"
                      + "Mật khẩu: " + _matKhauMoi + "\n\n"
                      + "Hãy ghi lại thông tin tài khoản này.";

                MessageBox.Show(
                    thongBao,
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

            btnLuu.Content = _dangLuu ? "Đang lưu..." : "Lưu hồ sơ";
        }

        private void QuayLai_Click(object sender, RoutedEventArgs e)
        {
            if (!_dangLuu)
                QuayLai?.Invoke();
        }
    }
}