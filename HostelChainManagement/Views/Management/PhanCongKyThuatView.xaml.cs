using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class PhanCongKyThuatView : UserControl
    {
        private readonly string _maSuCo;
        private string _maDayDaTai = "";

        private bool _dangTai;
        private bool _dangLuu;
        private bool _sanSang;
        private bool _daLuu;

        public event Action? QuayLai;
        public event Action? DaLuu;

        public PhanCongKyThuatView(string maSuCo)
        {
            InitializeComponent();

            _maSuCo = maSuCo;
            txtMaYeuCau.Text = maSuCo;
            dpHan.DisplayDateStart = DateTime.Today;

            // Nối sự kiện, tránh đăng ký cùng hàm hai lần.
            Loaded -= View_Loaded;
            Loaded += View_Loaded;

            dgNhanVien.SelectionChanged -= NhanVien_Changed;
            dgNhanVien.SelectionChanged += NhanVien_Changed;

            dpHan.SelectedDateChanged -= Han_Changed;
            dpHan.SelectedDateChanged += Han_Changed;

            btnLuu.Click -= Luu_Click;
            btnLuu.Click += Luu_Click;

            btnQuayLai.Click -= QuayLai_Click;
            btnQuayLai.Click += QuayLai_Click;

            CapNhatNut();
        }

        private static string TaoMa(string tienTo)
        {
            return tienTo + Guid.NewGuid().ToString("N")
                .Substring(0, 20 - tienTo.Length)
                .ToUpperInvariant();
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai || _sanSang || _daLuu)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền phân công.";
                CapNhatNut();
                return;
            }

            _dangTai = true;
            _sanSang = false;
            dgNhanVien.ItemsSource = null;
            txtNhanVien.Clear();

            CapNhatNut();
            txtThongBao.Text = "Đang tải yêu cầu và kỹ thuật viên...";

            try
            {
                using var connection = Database.CreateConnection();
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT
                            s.TrangThai,
                            s.NoiDungSuCo,
                            s.MucDoUuTien,
                            p.MaDayTro,
                            d.TenDayTro + N' - ' + p.SoPhong AS TenPhong,
                            CASE WHEN EXISTS (
                                SELECT 1
                                FROM dbo.ChiTietSuaChua ct
                                WHERE ct.MaSuCo = s.MaSuCo
                            ) THEN 1 ELSE 0 END AS DaPhanCong
                        FROM dbo.PhieuSuCo s
                        JOIN dbo.PhongTro p ON p.MaPhong = s.MaPhong
                        JOIN dbo.DayTro d ON d.MaDayTro = p.MaDayTro
                        WHERE s.MaSuCo = @MaSuCo
                          AND EXISTS (
                              SELECT 1
                              FROM dbo.NhanVien nv
                              WHERE nv.MaDayTro = p.MaDayTro
                                AND nv.MaNguoiDung = @QuanLy
                                AND nv.ChucVu = N'Quản lý'
                                AND nv.TrangThaiLamViec = N'Đang làm'
                          );";

                    command.Parameters.Add(
                        "@MaSuCo", SqlDbType.VarChar, 20)
                        .Value = _maSuCo;

                    command.Parameters.Add(
                        "@QuanLy", SqlDbType.VarChar, 20)
                        .Value = user.MaNguoiDung;

                    using var reader =
                        await command.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        txtThongBao.Text =
                            "Yêu cầu không tồn tại hoặc bạn không có quyền.";
                        return;
                    }

                    if (!IsLoaded)
                        return;

                    string trangThai =
                        Convert.ToString(reader["TrangThai"]) ?? "";

                    txtTrangThai.Text = trangThai;
                    txtPhong.Text = Convert.ToString(reader["TenPhong"]);
                    txtNoiDung.Text =
                        Convert.ToString(reader["NoiDungSuCo"]);

                    _maDayDaTai =
                        Convert.ToString(reader["MaDayTro"]) ?? "";

                    bool khanCap =
                        Convert.ToString(reader["MucDoUuTien"]) == "Khẩn cấp";

                    rbKhanCap.IsChecked = khanCap;
                    rbBinhThuong.IsChecked = !khanCap;

                    if (trangThai != "Đã tiếp nhận")
                    {
                        txtThongBao.Text =
                            "Chỉ phân công yêu cầu có trạng thái Đã tiếp nhận.";
                        return;
                    }

                    if (Convert.ToInt32(reader["DaPhanCong"]) == 1)
                    {
                        txtThongBao.Text =
                            "Yêu cầu đã có phân công. Hãy quay lại danh sách.";
                        return;
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT
                            nv.MaNhanVien,
                            n.HoTen,
                            (
                                SELECT COUNT(*)
                                FROM dbo.ChiTietSuaChua ct
                                JOIN dbo.NhanVien nv2
                                  ON nv2.MaNhanVien = ct.MaNhanVien
                                WHERE nv2.MaNguoiDung = nv.MaNguoiDung
                                  AND ct.TrangThai IN
                                      (N'Được phân công', N'Đang xử lý')
                            ) AS SoCongViec
                        FROM dbo.NhanVien nv
                        JOIN dbo.NguoiDung n
                          ON n.MaNguoiDung = nv.MaNguoiDung
                        WHERE nv.MaDayTro = @MaDay
                          AND nv.ChucVu = N'Kỹ thuật'
                          AND nv.TrangThaiLamViec = N'Đang làm'
                          AND n.VaiTro = N'Nhân viên kỹ thuật'
                          AND n.TrangThai = N'Hoạt động'
                        ORDER BY SoCongViec, n.HoTen, nv.MaNhanVien;";

                    command.Parameters.Add(
                        "@MaDay", SqlDbType.VarChar, 20)
                        .Value = _maDayDaTai;

                    using var reader =
                        await command.ExecuteReaderAsync();

                    var table = new DataTable();
                    table.Load(reader);

                    if (!IsLoaded)
                        return;

                    dgNhanVien.ItemsSource = table.DefaultView;
                    _sanSang = table.Rows.Count > 0;

                    txtThongBao.Text = _sanSang
                        ? "Chọn kỹ thuật viên và thời hạn hoàn thành dự kiến."
                        : "Dãy trọ chưa có kỹ thuật viên phù hợp.";
                }
            }
            catch (Exception ex)
            {
                _sanSang = false;
                txtThongBao.Text =
                    "Không tải được dữ liệu: " + ex.Message;
            }
            finally
            {
                _dangTai = false;
                CapNhatNut();
            }
        }

        private void NhanVien_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            if (txtNhanVien == null)
                return;

            txtNhanVien.Text =
                dgNhanVien.SelectedItem is DataRowView nv
                    ? Convert.ToString(nv["HoTen"])
                    : "";

            CapNhatNut();
        }

        private void Han_Changed(
            object? sender, SelectionChangedEventArgs e)
        {
            CapNhatNut();
        }

        private async void Luu_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai || _dangLuu || !_sanSang || _daLuu ||
                dgNhanVien.SelectedItem is not DataRowView nv)
            {
                return;
            }

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền phân công.";
                return;
            }

            if (!dpHan.SelectedDate.HasValue ||
                dpHan.SelectedDate.Value.Date < DateTime.Today)
            {
                txtThongBao.Text =
                    "Chọn hạn dự kiến từ hôm nay trở đi.";
                return;
            }

            DateTime han = dpHan.SelectedDate.Value.Date;

            string maNhanVien =
                Convert.ToString(nv["MaNhanVien"]) ?? "";

            string uuTien = rbKhanCap.IsChecked == true
                ? "Khẩn cấp"
                : rbBinhThuong.IsChecked == true
                    ? "Bình thường"
                    : "";

            if (maNhanVien.Length == 0 || uuTien.Length == 0)
            {
                txtThongBao.Text =
                    "Vui lòng chọn kỹ thuật viên và mức độ ưu tiên.";
                return;
            }

            var xacNhan = MessageBox.Show(
                $"Phân công yêu cầu {_maSuCo} cho {nv["HoTen"]}?\n"
                + $"Ưu tiên: {uuTien}\n"
                + $"Hạn dự kiến: {han:dd/MM/yyyy}",
                "Xác nhận phân công",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (xacNhan != MessageBoxResult.Yes)
                return;

            _dangLuu = true;
            CapNhatNut();
            txtThongBao.Text = "Đang lưu phân công...";

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
                    DECLARE @MaDay VARCHAR(20);
                    DECLARE @TrangThai NVARCHAR(30);
                    DECLARE @NguoiNhan VARCHAR(20);

                    SELECT
                        @MaDay = p.MaDayTro,
                        @TrangThai = s.TrangThai
                    FROM dbo.PhieuSuCo s WITH (UPDLOCK, HOLDLOCK)
                    JOIN dbo.PhongTro p ON p.MaPhong = s.MaPhong
                    WHERE s.MaSuCo = @MaSuCo;

                    IF @MaDay IS NULL
                        THROW 50001,
                            N'Yêu cầu không còn tồn tại.', 1;

                    IF @MaDay <> @MaDayCu
                        THROW 50002,
                            N'Phòng đã đổi dãy. Hãy mở lại form phân công.', 1;

                    IF @TrangThai <> N'Đã tiếp nhận'
                        THROW 50003,
                            N'Yêu cầu không còn ở trạng thái Đã tiếp nhận.', 1;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM dbo.NhanVien ql
                        WHERE ql.MaDayTro = @MaDay
                          AND ql.MaNguoiDung = @QuanLy
                          AND ql.ChucVu = N'Quản lý'
                          AND ql.TrangThaiLamViec = N'Đang làm'
                    )
                        THROW 50004,
                            N'Bạn không còn quyền quản lý yêu cầu.', 1;

                    SELECT @NguoiNhan = n.MaNguoiDung
                    FROM dbo.NhanVien nv WITH (HOLDLOCK)
                    JOIN dbo.NguoiDung n WITH (HOLDLOCK)
                      ON n.MaNguoiDung = nv.MaNguoiDung
                    WHERE nv.MaNhanVien = @MaNhanVien
                      AND nv.MaDayTro = @MaDay
                      AND nv.ChucVu = N'Kỹ thuật'
                      AND nv.TrangThaiLamViec = N'Đang làm'
                      AND n.VaiTro = N'Nhân viên kỹ thuật'
                      AND n.TrangThai = N'Hoạt động';

                    IF @NguoiNhan IS NULL
                        THROW 50005,
                            N'Kỹ thuật viên không còn phù hợp với dãy trọ.', 1;

                    IF @Han < CAST(GETDATE() AS DATE)
                        THROW 50006,
                            N'Hạn dự kiến không được trong quá khứ.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM dbo.ChiTietSuaChua WITH (UPDLOCK, HOLDLOCK)
                        WHERE MaSuCo = @MaSuCo
                    )
                        THROW 50007,
                            N'Yêu cầu đã có phân công. Hãy tải lại danh sách.', 1;

                    INSERT INTO dbo.ChiTietSuaChua
                        (MaChiTietSuaChua, MaSuCo, MaNhanVien,
                         TienDo, TrangThai)
                    VALUES
                        (@MaChiTiet, @MaSuCo, @MaNhanVien,
                         N'Đang kiểm tra', N'Được phân công');

                    INSERT INTO dbo.LichSuTienDo
                        (MaChiTietSuaChua, TienDo, GhiChu)
                    VALUES
                        (@MaChiTiet, N'Đang kiểm tra',
                         N'Phân công mới. Hạn hoàn thành dự kiến: '
                         + CONVERT(NVARCHAR(10), @Han, 103));

                    UPDATE dbo.PhieuSuCo
                    SET TrangThai = N'Đã phân công',
                        MucDoUuTien = @UuTien
                    WHERE MaSuCo = @MaSuCo;

                    INSERT INTO dbo.ThongBao
                        (MaThongBao, MaNguoiGui, TieuDe, NoiDung)
                    VALUES
                        (@MaThongBao, @QuanLy,
                         N'Phân công xử lý yêu cầu sửa chữa',
                         N'Bạn được phân công yêu cầu ' + @MaSuCo
                         + N'. Ưu tiên: ' + @UuTien
                         + N'. Hạn dự kiến: '
                         + CONVERT(NVARCHAR(10), @Han, 103));

                    INSERT INTO dbo.ThongBaoNguoiNhan
                        (MaThongBao, MaNguoiNhan, DaDoc)
                    VALUES
                        (@MaThongBao, @NguoiNhan, 0);";

                command.Parameters.Add(
                    "@MaSuCo", SqlDbType.VarChar, 20)
                    .Value = _maSuCo;

                command.Parameters.Add(
                    "@MaDayCu", SqlDbType.VarChar, 20)
                    .Value = _maDayDaTai;

                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;

                command.Parameters.Add(
                    "@MaNhanVien", SqlDbType.VarChar, 20)
                    .Value = maNhanVien;

                command.Parameters.Add(
                    "@MaChiTiet", SqlDbType.VarChar, 20)
                    .Value = TaoMa("SC");

                command.Parameters.Add(
                    "@MaThongBao", SqlDbType.VarChar, 20)
                    .Value = TaoMa("TB");

                command.Parameters.Add(
                    "@UuTien", SqlDbType.NVarChar, 30)
                    .Value = uuTien;

                command.Parameters.Add(
                    "@Han", SqlDbType.Date)
                    .Value = han;

                await command.ExecuteNonQueryAsync();
                transaction.Commit();

                thanhCong = true;
                _daLuu = true;
                _sanSang = false;

                txtTrangThai.Text = "Đã phân công";
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                txtThongBao.Text =
                    "Dữ liệu đang được cập nhật. Vui lòng thử lại.";
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                txtThongBao.Text =
                    "Dữ liệu phân công bị trùng. Quay lại và tải lại yêu cầu.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không lưu được phân công: " + ex.Message;
            }
            finally
            {
                _dangLuu = false;
                CapNhatNut();
            }

            if (thanhCong)
            {
                MessageBox.Show(
                    "Đã lưu phân công và thông báo cho kỹ thuật viên.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DaLuu?.Invoke();
            }
        }

        private void CapNhatNut()
        {
            if (btnLuu == null || btnQuayLai == null ||
                dgNhanVien == null || dpHan == null ||
                rbBinhThuong == null || rbKhanCap == null)
            {
                return;
            }

            bool coQuyen =
                UserSession.CurrentUser?.VaiTro == "Nhân viên quản lý";

            bool choPhep =
                coQuyen && _sanSang &&
                !_dangTai && !_dangLuu && !_daLuu;

            dgNhanVien.IsEnabled = choPhep;
            rbBinhThuong.IsEnabled = choPhep;
            rbKhanCap.IsEnabled = choPhep;
            dpHan.IsEnabled = choPhep;

            btnQuayLai.IsEnabled = !_dangLuu;

            btnLuu.IsEnabled =
                choPhep &&
                dgNhanVien.SelectedItem is DataRowView &&
                dpHan.SelectedDate.HasValue &&
                dpHan.SelectedDate.Value.Date >= DateTime.Today;

            btnLuu.Content = _dangLuu
                ? "Đang lưu..."
                : "Lưu phân công";
        }

        private void QuayLai_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_dangLuu)
                QuayLai?.Invoke();
        }
    }
}