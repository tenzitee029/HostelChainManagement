using System;
using System.Data;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class QuanLyHoaDonView : UserControl
    {
        public event Action<string>? MoThanhToan;
        public event Action? MoTaoHoaDon;

        private DataTable? _duLieu;
        private bool _dangXuLy;

        private const string DieuKienQuyen = @"
            EXISTS (
                SELECT 1
                FROM dbo.NhanVien nv
                WHERE nv.MaDayTro = p.MaDayTro
                  AND nv.MaNguoiDung = @QuanLy
                  AND nv.ChucVu = N'Quản lý'
                  AND nv.TrangThaiLamViec = N'Đang làm'
            )";

        public QuanLyHoaDonView()
        {
            InitializeComponent();

            // Bỏ đăng ký cùng hàm trước khi nối để tránh chạy hai lần.
            Loaded -= View_Loaded;
            Loaded += View_Loaded;

            btnTimKiem.Click -= TimKiem_Click;
            btnTimKiem.Click += TimKiem_Click;

            dgHoaDon.SelectionChanged -= ChonHoaDon_Changed;
            dgHoaDon.SelectionChanged += ChonHoaDon_Changed;

            btnXemChiTiet.Click -= XemChiTiet_Click;
            btnXemChiTiet.Click += XemChiTiet_Click;

            btnXacNhanThanhToan.Click -= ThanhToan_Click;
            btnXacNhanThanhToan.Click += ThanhToan_Click;

            btnTaoHoaDon.Click -= TaoHoaDon_Click;
            btnTaoHoaDon.Click += TaoHoaDon_Click;

            CapNhatNut();
        }

        private static string Tien(decimal soTien)
        {
            return soTien.ToString(
                "N0",
                CultureInfo.GetCultureInfo("vi-VN")) + " VNĐ";
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            await TaiDanhSachAsync();
        }

        private async Task TaiDanhSachAsync()
        {
            if (_dangXuLy)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                HienThiLoi("Bạn không có quyền xem hóa đơn.");
                return;
            }

            _dangXuLy = true;
            _duLieu = null;
            dgHoaDon.ItemsSource = null;

            txtDanhSachTrong.Text = "Đang tải hóa đơn...";
            txtDanhSachTrong.Visibility = Visibility.Visible;
            txtTrangThai.Text = "";

            CapNhatNut();

            try
            {
                using var connection = Database.CreateConnection();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT
                        hd.MaHoaDon,
                        hd.MaHopDong,
                        CAST(hd.Thang AS VARCHAR(2)) + '/'
                            + CAST(hd.Nam AS VARCHAR(4)) AS KyHoaDon,
                        d.TenDayTro + N' - ' + p.SoPhong AS TenPhong,
                        n.HoTen AS TenKhachThue,
                        hd.TongTien,
                        COALESCE(tt.DaTra, 0) AS DaTra,
                        hd.TongTien - COALESCE(tt.DaTra, 0) AS ConNo,
                        CASE
                            WHEN COALESCE(tt.DaTra, 0) > 0
                                THEN N'Còn nợ'
                            ELSE N'Chưa thanh toán'
                        END AS TrangThai
                    FROM dbo.HoaDon hd
                    JOIN dbo.HopDongThue h
                        ON h.MaHopDong = hd.MaHopDong
                    JOIN dbo.KhachThue k
                        ON k.MaKhachThue = h.MaKhachThue
                    JOIN dbo.NguoiDung n
                        ON n.MaNguoiDung = k.MaNguoiDung
                    JOIN dbo.PhongTro p
                        ON p.MaPhong = hd.MaPhong
                    JOIN dbo.DayTro d
                        ON d.MaDayTro = p.MaDayTro
                    OUTER APPLY (
                        SELECT SUM(t.SoTien) AS DaTra
                        FROM dbo.ThanhToan t
                        WHERE t.MaHoaDon = hd.MaHoaDon
                    ) tt
                    WHERE hd.TongTien > COALESCE(tt.DaTra, 0)
                      AND " + DieuKienQuyen + @"
                    ORDER BY hd.Nam DESC, hd.Thang DESC, hd.MaHoaDon;";

                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;

                await connection.OpenAsync();

                using var reader =
                    await command.ExecuteReaderAsync();

                var table = new DataTable();
                table.Load(reader);

                if (!IsLoaded)
                    return;

                _duLieu = table;
                HienThiKetQua();
            }
            catch (Exception ex)
            {
                HienThiLoi("Không tải được danh sách hóa đơn.");

                MessageBox.Show(
                    ex.Message,
                    "Lỗi tải hóa đơn",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _dangXuLy = false;
                CapNhatNut();
            }
        }

        private void HienThiLoi(string thongBao)
        {
            _duLieu = null;
            dgHoaDon.ItemsSource = null;

            txtDanhSachTrong.Text = thongBao;
            txtDanhSachTrong.Visibility = Visibility.Visible;
            txtTrangThai.Text = "Chưa tải được dữ liệu.";

            CapNhatNut();
        }

        private void CapNhatNut()
        {
            // Tránh lỗi nếu sự kiện XAML chạy trong lúc khởi tạo.
            if (btnTaoHoaDon == null ||
                btnTimKiem == null ||
                btnXemChiTiet == null ||
                btnXacNhanThanhToan == null)
            {
                return;
            }

            bool coQuyen =
                UserSession.CurrentUser?.VaiTro == "Nhân viên quản lý";

            bool sanSang =
                coQuyen && !_dangXuLy && _duLieu != null;

            bool daChon =
                sanSang && dgHoaDon.SelectedItem is DataRowView;

            btnTaoHoaDon.IsEnabled = coQuyen && !_dangXuLy;
            txtTimKiem.IsEnabled = sanSang;
            btnTimKiem.IsEnabled = sanSang;

            btnXemChiTiet.IsEnabled = daChon;
            btnXacNhanThanhToan.IsEnabled = daChon;
        }

        private void ChonHoaDon_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            CapNhatNut();
        }

        private void TimKiem_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_dangXuLy)
                HienThiKetQua();
        }

        private void HienThiKetQua()
        {
            if (_duLieu == null)
                return;

            string tuKhoa = txtTimKiem.Text.Trim();
            var ketQua = _duLieu.Clone();

            foreach (DataRow dong in _duLieu.Rows)
            {
                bool phuHop = tuKhoa.Length == 0;

                foreach (string cot in new[]
                {
                    "MaHoaDon",
                    "TenPhong",
                    "TenKhachThue",
                    "KyHoaDon"
                })
                {
                    string noiDung =
                        Convert.ToString(dong[cot]) ?? "";

                    if (noiDung.IndexOf(
                        tuKhoa,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        phuHop = true;
                        break;
                    }
                }

                if (phuHop)
                    ketQua.ImportRow(dong);
            }

            dgHoaDon.ItemsSource = ketQua.DefaultView;

            txtDanhSachTrong.Text = _duLieu.Rows.Count == 0
                ? "Không có hóa đơn còn nợ trong phạm vi quản lý."
                : "Không có hóa đơn phù hợp.";

            txtDanhSachTrong.Visibility = ketQua.Rows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            txtTrangThai.Text =
                $"Hiển thị {ketQua.Rows.Count} hóa đơn còn nợ.";

            CapNhatNut();
        }

        // Mở form tạo hóa đơn.
        private void TaoHoaDon_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangXuLy ||
                UserSession.CurrentUser?.VaiTro != "Nhân viên quản lý")
            {
                return;
            }

            MoTaoHoaDon?.Invoke();
        }

        // Mở form xác nhận thanh toán thay cho hộp thoại cũ.
        private void ThanhToan_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangXuLy ||
                UserSession.CurrentUser?.VaiTro != "Nhân viên quản lý")
            {
                return;
            }

            if (dgHoaDon.SelectedItem is DataRowView hoaDon)
            {
                string maHoaDon =
                    Convert.ToString(hoaDon["MaHoaDon"]) ?? "";

                if (maHoaDon.Length > 0)
                    MoThanhToan?.Invoke(maHoaDon);
            }
        }

        // Đọc lại thông tin từ SQL trước khi xem chi tiết.
        private async void XemChiTiet_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangXuLy ||
                dgHoaDon.SelectedItem is not DataRowView dong)
            {
                return;
            }

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
                return;

            string maHoaDon =
                Convert.ToString(dong["MaHoaDon"]) ?? "";

            _dangXuLy = true;
            CapNhatNut();

            try
            {
                using var connection = Database.CreateConnection();
                await connection.OpenAsync();

                var noiDung = new StringBuilder();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT
                            n.HoTen,
                            d.TenDayTro + N' - ' + p.SoPhong AS TenPhong,
                            hd.Thang,
                            hd.Nam,
                            hd.HanThanhToan,
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
                        JOIN dbo.DayTro d
                          ON d.MaDayTro = p.MaDayTro
                        OUTER APPLY (
                            SELECT SUM(t.SoTien) AS DaTra
                            FROM dbo.ThanhToan t
                            WHERE t.MaHoaDon = hd.MaHoaDon
                        ) tt
                        WHERE hd.MaHoaDon = @MaHoaDon
                          AND " + DieuKienQuyen + ";";

                    command.Parameters.Add(
                        "@MaHoaDon", SqlDbType.VarChar, 20)
                        .Value = maHoaDon;

                    command.Parameters.Add(
                        "@QuanLy", SqlDbType.VarChar, 20)
                        .Value = user.MaNguoiDung;

                    using var reader =
                        await command.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        MessageBox.Show(
                            "Hóa đơn không tồn tại hoặc bạn không còn quyền xem.");
                        return;
                    }

                    decimal tong = Convert.ToDecimal(reader["TongTien"]);
                    decimal daTra = Convert.ToDecimal(reader["DaTra"]);
                    decimal conNo = Math.Max(0, tong - daTra);

                    noiDung.AppendLine("Hóa đơn: " + maHoaDon);
                    noiDung.AppendLine("Khách: " + reader["HoTen"]);
                    noiDung.AppendLine("Phòng: " + reader["TenPhong"]);
                    noiDung.AppendLine(
                        "Kỳ: " + reader["Thang"] + "/" + reader["Nam"]);
                    noiDung.AppendLine(
                        "Hạn thanh toán: "
                        + Convert.ToDateTime(reader["HanThanhToan"])
                            .ToString("dd/MM/yyyy"));

                    noiDung.AppendLine();
                    noiDung.AppendLine("Tổng tiền: " + Tien(tong));
                    noiDung.AppendLine("Đã trả: " + Tien(daTra));
                    noiDung.AppendLine("Còn nợ: " + Tien(conNo));
                    noiDung.AppendLine();
                    noiDung.AppendLine("CÁC KHOẢN THU:");
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT ct.LoaiKhoanThu,
                               ct.SoLuong,
                               ct.DonGia,
                               ct.ThanhTien,
                               ct.GhiChu
                        FROM dbo.ChiTietHoaDon ct
                        JOIN dbo.HoaDon hd
                          ON hd.MaHoaDon = ct.MaHoaDon
                        JOIN dbo.PhongTro p
                          ON p.MaPhong = hd.MaPhong
                        WHERE hd.MaHoaDon = @MaHoaDon
                          AND " + DieuKienQuyen + @"
                        ORDER BY ct.LoaiKhoanThu, ct.MaChiTietHoaDon;";

                    command.Parameters.Add(
                        "@MaHoaDon", SqlDbType.VarChar, 20)
                        .Value = maHoaDon;

                    command.Parameters.Add(
                        "@QuanLy", SqlDbType.VarChar, 20)
                        .Value = user.MaNguoiDung;

                    using var reader =
                        await command.ExecuteReaderAsync();

                    bool coChiTiet = false;

                    while (await reader.ReadAsync())
                    {
                        coChiTiet = true;

                        string ghiChu =
                            Convert.ToString(reader["GhiChu"]) ?? "";

                        string tenKhoan =
                            Convert.ToString(reader["LoaiKhoanThu"]) ?? "";

                        if (ghiChu.Length > 0)
                            tenKhoan += " (" + ghiChu + ")";

                        noiDung.AppendLine(
                            tenKhoan
                            + ": " + reader["SoLuong"]
                            + " × " + Tien(
                                Convert.ToDecimal(reader["DonGia"]))
                            + " = " + Tien(
                                Convert.ToDecimal(reader["ThanhTien"])));
                    }

                    if (!coChiTiet)
                        noiDung.AppendLine("Chưa có khoản thu chi tiết.");
                }

                var cuaSo = new Window
                {
                    Title = "Chi tiết hóa đơn",
                    Width = 650,
                    Height = 520,
                    Owner = Window.GetWindow(this),
                    WindowStartupLocation =
                        WindowStartupLocation.CenterOwner,

                    Content = new TextBox
                    {
                        Text = noiDung.ToString(),
                        IsReadOnly = true,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalScrollBarVisibility =
                            ScrollBarVisibility.Auto,
                        Margin = new Thickness(16),
                        Padding = new Thickness(12)
                    }
                };

                cuaSo.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Lỗi xem hóa đơn",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _dangXuLy = false;
                CapNhatNut();
            }
        }
    }
}