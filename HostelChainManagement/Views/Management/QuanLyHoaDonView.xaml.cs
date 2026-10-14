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
        public event Action<string>? MoChiTietHoaDon;

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

                        CASE
                            WHEN hd.TongTien > COALESCE(tt.DaTra, 0)
                                THEN hd.TongTien - COALESCE(tt.DaTra, 0)
                            ELSE 0
                        END AS ConNo,

                        CASE
                            WHEN COALESCE(tt.DaTra, 0) >= hd.TongTien
                                THEN N'Đã thanh toán'
                            WHEN COALESCE(tt.DaTra, 0) > 0
                                THEN N'Còn nợ'
                            ELSE N'Chưa thanh toán'
                        END AS TrangThai

                    FROM dbo.HoaDon hd

                    JOIN dbo.HopDongThue h
                        ON h.MaHopDong = hd.MaHopDong
                       AND h.MaPhong = hd.MaPhong

                    JOIN dbo.KhachThue k
                        ON k.MaKhachThue = h.MaKhachThue

                    JOIN dbo.NguoiDung n
                        ON n.MaNguoiDung = k.MaNguoiDung

                    JOIN dbo.PhongTro p
                        ON p.MaPhong = hd.MaPhong

                    JOIN dbo.DayTro d
                        ON d.MaDayTro = p.MaDayTro

                    OUTER APPLY
                    (
                        SELECT SUM(t.SoTien) AS DaTra
                        FROM dbo.ThanhToan t
                        WHERE t.MaHoaDon = hd.MaHoaDon
                    ) tt

                    WHERE " + DieuKienQuyen + @"

                    ORDER BY
                        hd.Nam DESC,
                        hd.Thang DESC,
                        hd.MaHoaDon;";

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
            if (btnTaoHoaDon == null
                || btnTimKiem == null
                || btnXemChiTiet == null
                || btnXacNhanThanhToan == null)
            {
                return;
            }

            bool coQuyen =
                UserSession.CurrentUser?.VaiTro == "Nhân viên quản lý";

            bool sanSang =
                coQuyen && !_dangXuLy && _duLieu != null;

            var selected =
                dgHoaDon.SelectedItem as DataRowView;

            bool daChon =
                sanSang && selected != null;

            bool conNo =
                daChon
                && selected != null
                && Convert.ToDecimal(selected["ConNo"]) > 0;

            btnTaoHoaDon.IsEnabled =
                coQuyen && !_dangXuLy;

            txtTimKiem.IsEnabled = sanSang;
            btnTimKiem.IsEnabled = sanSang;

            // Hóa đơn đã thanh toán vẫn xem được.
            btnXemChiTiet.IsEnabled = daChon;

            // Chỉ được xác nhận khi còn tiền phải trả.
            btnXacNhanThanhToan.IsEnabled = conNo;
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
                    "KyHoaDon",
                    "TrangThai"
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
                ? "Chưa có hóa đơn trong phạm vi quản lý."
                : "Không có hóa đơn phù hợp.";

            txtDanhSachTrong.Visibility = ketQua.Rows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            int daThanhToan = 0;
            int chuaThanhToanHet = 0;

            foreach (DataRow row in ketQua.Rows)
            {
                if (Convert.ToDecimal(row["ConNo"]) <= 0)
                    daThanhToan++;
                else
                    chuaThanhToanHet++;
            }

            txtTrangThai.Text =
                $"Hiển thị {ketQua.Rows.Count} hóa đơn — "
                + $"{daThanhToan} đã thanh toán, "
                + $"{chuaThanhToanHet} chưa thanh toán hết.";

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
                if (Convert.ToDecimal(hoaDon["ConNo"]) <= 0)
                {
                    MessageBox.Show(
                        Window.GetWindow(this),
                        "Hóa đơn đã được thanh toán hết. "
                        + "Bạn có thể chọn Xem chi tiết để xem lại.",
                        "Thông báo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                string maHoaDon =
                    Convert.ToString(hoaDon["MaHoaDon"]) ?? "";

                if (maHoaDon.Length > 0)
                    MoThanhToan?.Invoke(maHoaDon);
            }
        }

        private void XemChiTiet_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_dangXuLy
                || UserSession.CurrentUser?.VaiTro != "Nhân viên quản lý")
            {
                return;
            }

            if (dgHoaDon.SelectedItem is not DataRowView row)
                return;

            string maHoaDon =
                Convert.ToString(row["MaHoaDon"]) ?? "";

            if (!string.IsNullOrWhiteSpace(maHoaDon))
            {
                MoChiTietHoaDon?.Invoke(maHoaDon);
            }
        }
    }
}