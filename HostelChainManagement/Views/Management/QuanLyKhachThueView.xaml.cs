using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class QuanLyKhachThueView : UserControl
    {
        public event Action<DataRowView?>? MoForm;

        private DataTable? _duLieu;
        private bool _dangTai;
        private bool _dangXoa;
        private bool _coQuyen;

        public QuanLyKhachThueView()
        {
            InitializeComponent();

            // Nối sự kiện; bỏ đăng ký cùng hàm trước để tránh chạy hai lần.
            Loaded -= View_Loaded;
            Loaded += View_Loaded;

            btnThemKhach.Click -= Them_Click;
            btnThemKhach.Click += Them_Click;

            btnCapNhat.Click -= CapNhat_Click;
            btnCapNhat.Click += CapNhat_Click;

            btnXoaKhach.Click -= Xoa_Click;
            btnXoaKhach.Click += Xoa_Click;

            btnTimKiem.Click -= TimKiem_Click;
            btnTimKiem.Click += TimKiem_Click;

            txtTimKiem.KeyDown -= TimKiem_KeyDown;
            txtTimKiem.KeyDown += TimKiem_KeyDown;

            dgKhachThue.SelectionChanged -= ChonKhach_Changed;
            dgKhachThue.SelectionChanged += ChonKhach_Changed;

            CapNhatNut();
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            await TaiDanhSachAsync();
        }

        private async Task TaiDanhSachAsync()
        {
            if (_dangTai || _dangXoa)
                return;

            var user = UserSession.CurrentUser;

            _coQuyen =
                user != null &&
                user.VaiTro == "Nhân viên quản lý";

            if (!_coQuyen)
            {
                HienThiLoi("Bạn không có quyền xem khách thuê.");
                CapNhatNut();
                return;
            }

            _dangTai = true;
            _duLieu = null;
            dgKhachThue.ItemsSource = null;

            txtDanhSachTrong.Text = "Đang tải danh sách khách thuê...";
            txtDanhSachTrong.Visibility = Visibility.Visible;
            txtTrangThai.Text = "";

            CapNhatNut();

            try
            {
                using var connection = Database.CreateConnection();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT
                        k.MaKhachThue,
                        k.MaNguoiDung,
                        k.MaDayTro,
                        n.HoTen,
                        n.CCCD,
                        n.SoDienThoai,
                        n.DiaChi,
                        hd.MaPhong,
                        hd.MaHopDong,

                        CASE
                            WHEN hd.DangHieuLuc = 1
                                THEN hd.TenDayTro + N' - ' + hd.SoPhong
                            ELSE N'—'
                        END AS PhongDangThue,

                        CASE
                            WHEN hd.DangHieuLuc = 1
                                THEN N'Đang thuê'
                            WHEN hd.MaHopDong IS NULL
                                THEN N'Chưa có hợp đồng'
                            ELSE N'Không có hợp đồng hiệu lực'
                        END AS TrangThai

                    FROM dbo.KhachThue k
                    JOIN dbo.NguoiDung n
                        ON n.MaNguoiDung = k.MaNguoiDung

                    OUTER APPLY (
                        SELECT TOP (1)
                            h.MaHopDong,
                            p.MaPhong,
                            p.SoPhong,
                            d.TenDayTro,

                            CASE
                                WHEN h.TrangThai = N'Hiệu lực'
                                 AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                                 AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                                    THEN 1
                                ELSE 0
                            END AS DangHieuLuc

                        FROM dbo.HopDongThue h
                        JOIN dbo.PhongTro p
                            ON p.MaPhong = h.MaPhong
                        JOIN dbo.DayTro d
                            ON d.MaDayTro = p.MaDayTro

                        WHERE h.MaKhachThue = k.MaKhachThue
                          AND EXISTS (
                              SELECT 1
                              FROM dbo.NhanVien nv
                              WHERE nv.MaDayTro = p.MaDayTro
                                AND nv.MaNguoiDung = @MaNguoiDung
                                AND nv.ChucVu = N'Quản lý'
                                AND nv.TrangThaiLamViec = N'Đang làm'
                          )

                        ORDER BY
                            CASE
                                WHEN h.TrangThai = N'Hiệu lực'
                                 AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                                 AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                                    THEN 0
                                ELSE 1
                            END,
                            h.NgayBatDau DESC,
                            h.MaHopDong
                    ) hd

                    WHERE hd.MaHopDong IS NOT NULL
                       OR EXISTS (
                           SELECT 1
                           FROM dbo.NhanVien nv
                           WHERE nv.MaDayTro = k.MaDayTro
                             AND nv.MaNguoiDung = @MaNguoiDung
                             AND nv.ChucVu = N'Quản lý'
                             AND nv.TrangThaiLamViec = N'Đang làm'
                       )

                    ORDER BY n.HoTen, k.MaKhachThue;";

                command.Parameters.Add(
                    "@MaNguoiDung", SqlDbType.VarChar, 20)
                    .Value = user!.MaNguoiDung;

                await connection.OpenAsync();

                using var reader = await command.ExecuteReaderAsync();

                var table = new DataTable();
                table.Load(reader);

                _duLieu = table;
                HienThiKetQua();
            }
            catch (Exception ex)
            {
                HienThiLoi("Không tải được danh sách khách thuê.");

                MessageBox.Show(
                    "Chi tiết lỗi:\n" + ex.Message,
                    "Lỗi tải khách thuê",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _dangTai = false;
                CapNhatNut();
            }
        }

        private void CapNhatNut()
        {
            // Có thể được gọi lúc XAML còn đang khởi tạo.
            if (btnCapNhat == null || btnXoaKhach == null ||
                btnThemKhach == null || btnTimKiem == null)
            {
                return;
            }

            bool sanSang =
                _coQuyen &&
                !_dangTai &&
                !_dangXoa &&
                _duLieu != null;

            bool daChon =
                sanSang &&
                dgKhachThue.SelectedItem is DataRowView;

            btnThemKhach.IsEnabled = sanSang;
            btnTimKiem.IsEnabled = sanSang;
            txtTimKiem.IsEnabled = sanSang;

            btnCapNhat.IsEnabled = daChon;
            btnXoaKhach.IsEnabled = daChon;
        }

        private void Them_Click(object sender, RoutedEventArgs e)
        {
            if (!_coQuyen || _dangTai || _dangXoa || _duLieu == null)
                return;

            MoForm?.Invoke(null);
        }

        private void ChonKhach_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            CapNhatNut();
        }

        private void CapNhat_Click(object sender, RoutedEventArgs e)
        {
            if (!_coQuyen || _dangTai || _dangXoa)
                return;

            if (dgKhachThue.SelectedItem is DataRowView khach)
            {
                MoForm?.Invoke(khach);
            }
        }

        private async void Xoa_Click(object sender, RoutedEventArgs e)
        {
            if (!_coQuyen || _dangTai || _dangXoa)
                return;

            if (dgKhachThue.SelectedItem is not DataRowView khach)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                MessageBox.Show("Bạn không có quyền xóa hồ sơ.");
                return;
            }

            string maKhach =
                Convert.ToString(khach["MaKhachThue"]) ?? "";

            string hoTen =
                Convert.ToString(khach["HoTen"]) ?? "";

            var xacNhan = MessageBox.Show(
                $"Bạn có chắc muốn xóa hồ sơ khách thuê “{hoTen}”?\n\n"
                + "Chỉ xóa khách chưa có hợp đồng. "
                + "Tài khoản đăng nhập được giữ lại.",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (xacNhan != MessageBoxResult.Yes)
                return;

            _dangXoa = true;
            IsEnabled = false;
            CapNhatNut();

            bool daXoa = false;

            try
            {
                using var connection = Database.CreateConnection();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    DECLARE @DaXoa TABLE (MaKhachThue VARCHAR(20));

                    DELETE k
                    OUTPUT DELETED.MaKhachThue INTO @DaXoa
                    FROM dbo.KhachThue k
                    WHERE k.MaKhachThue = @MaKhach
                      AND NOT EXISTS (
                          SELECT 1
                          FROM dbo.HopDongThue h
                          WHERE h.MaKhachThue = k.MaKhachThue
                      )
                      AND EXISTS (
                          SELECT 1
                          FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = k.MaDayTro
                            AND nv.MaNguoiDung = @NguoiQuanLy
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      );

                    SELECT COUNT(*) FROM @DaXoa;";

                command.Parameters.Add(
                    "@MaKhach", SqlDbType.VarChar, 20)
                    .Value = maKhach;

                command.Parameters.Add(
                    "@NguoiQuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;

                await connection.OpenAsync();

                object? ketQua = await command.ExecuteScalarAsync();

                daXoa = ketQua != null &&
                    Convert.ToInt32(ketQua) == 1;

                MessageBox.Show(
                    daXoa
                        ? "Xóa hồ sơ khách thuê thành công."
                        : "Không thể xóa: khách đã có hợp đồng, "
                          + "hồ sơ đã bị xóa hoặc bạn không còn quyền quản lý.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    daXoa
                        ? MessageBoxImage.Information
                        : MessageBoxImage.Warning);
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                MessageBox.Show(
                    "Không thể xóa hồ sơ vì còn dữ liệu liên quan.",
                    "Không thể xóa",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không xóa được hồ sơ:\n" + ex.Message,
                    "Lỗi xóa",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _dangXoa = false;
                IsEnabled = true;
                CapNhatNut();
            }

            if (daXoa)
            {
                await TaiDanhSachAsync();
            }
        }

        private void HienThiLoi(string thongBao)
        {
            _duLieu = null;
            dgKhachThue.ItemsSource = null;

            txtDanhSachTrong.Text = thongBao;
            txtDanhSachTrong.Visibility = Visibility.Visible;
            txtTrangThai.Text = "Chưa tải được dữ liệu.";

            CapNhatNut();
        }

        private void TimKiem_Click(object sender, RoutedEventArgs e)
        {
            if (_coQuyen && !_dangTai && !_dangXoa)
                HienThiKetQua();
        }

        private void TimKiem_KeyDown(
            object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter &&
                _coQuyen && !_dangTai && !_dangXoa)
            {
                HienThiKetQua();
                e.Handled = true;
            }
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
                    "HoTen", "CCCD", "SoDienThoai"
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

            dgKhachThue.ItemsSource = ketQua.DefaultView;

            txtDanhSachTrong.Text = _duLieu.Rows.Count == 0
                ? "Chưa có khách thuê thuộc phạm vi quản lý."
                : "Không có khách thuê phù hợp với nội dung tìm kiếm.";

            txtDanhSachTrong.Visibility = ketQua.Rows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            txtTrangThai.Text =
                $"Hiển thị {ketQua.Rows.Count} / {_duLieu.Rows.Count} khách thuê.";

            CapNhatNut();
        }
    }
}