using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class QuanLySuaChuaView : UserControl
    {
        public event Action<string>? MoChiTiet;

        private DataTable? _duLieu;
        private bool _dangXuLy;
        private string _boLoc = "";

        private const string DieuKienQuyen = @"
            EXISTS (
                SELECT 1
                FROM dbo.NhanVien nv
                WHERE nv.MaDayTro = p.MaDayTro
                  AND nv.MaNguoiDung = @QuanLy
                  AND nv.ChucVu = N'Quản lý'
                  AND nv.TrangThaiLamViec = N'Đang làm'
            )";

        public QuanLySuaChuaView()
        {
            InitializeComponent();

            Loaded -= View_Loaded;
            Loaded += View_Loaded;

            dgSuCo.SelectionChanged -= ChonSuCo_Changed;
            dgSuCo.SelectionChanged += ChonSuCo_Changed;

            btnXemChiTiet.Click -= XemChiTiet_Click;
            btnXemChiTiet.Click += XemChiTiet_Click;

            btnTaiLai.Click -= TaiLai_Click;
            btnTaiLai.Click += TaiLai_Click;

            btnTatCa.Click += (_, _) =>
                LocTrangThai("");

            btnChoTiepNhan.Click += (_, _) =>
                LocTrangThai("Chờ tiếp nhận");

            btnDaTiepNhan.Click += (_, _) =>
                LocTrangThai("Đã tiếp nhận");

            btnDaPhanCong.Click += (_, _) =>
                LocTrangThai("Đã phân công");

            btnDangXuLy.Click += (_, _) =>
                LocTrangThai("Đang xử lý");

            btnHoanThanh.Click += (_, _) =>
                LocTrangThai("Đã hoàn thành");

            btnTuChoi.Click += (_, _) =>
                LocTrangThai("Từ chối");

            CapNhatNut();
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            await TaiDanhSachAsync();
        }

        private async void TaiLai_Click(
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
                HienThiLoi("Bạn không có quyền xem sự cố.");
                return;
            }

            _dangXuLy = true;
            _duLieu = null;
            dgSuCo.ItemsSource = null;

            txtDanhSachTrong.Text = "Đang tải yêu cầu sửa chữa...";
            txtDanhSachTrong.Visibility = Visibility.Visible;
            txtTrangThai.Text = "";

            CapNhatNut();

            try
            {
                using var connection = Database.CreateConnection();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT
                        s.MaSuCo,
                        s.MaPhong,
                        d.TenDayTro + N' - ' + p.SoPhong AS TenPhong,
                        n.HoTen AS TenNguoiGui,
                        s.NgayGui,
                        s.NoiDungSuCo,
                        s.MucDoUuTien,
                        s.TrangThai
                    FROM dbo.PhieuSuCo s
                    JOIN dbo.PhongTro p
                        ON p.MaPhong = s.MaPhong
                    JOIN dbo.DayTro d
                        ON d.MaDayTro = p.MaDayTro
                    JOIN dbo.NguoiDung n
                        ON n.MaNguoiDung = s.MaNguoiGui
                    WHERE " + DieuKienQuyen + @"
                    ORDER BY
                        CASE
                            WHEN s.MucDoUuTien = N'Khẩn cấp'
                                THEN 0
                            ELSE 1
                        END,
                        s.NgayGui DESC,
                        s.MaSuCo;";

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
                HienThiLoi("Không tải được yêu cầu sửa chữa.");

                MessageBox.Show(
                    ex.Message,
                    "Lỗi tải sự cố",
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
            dgSuCo.ItemsSource = null;

            txtDanhSachTrong.Text = thongBao;
            txtDanhSachTrong.Visibility = Visibility.Visible;
            txtTrangThai.Text = "Chưa tải được dữ liệu.";

            CapNhatNut();
        }

        private void LocTrangThai(string trangThai)
        {
            if (_dangXuLy || _duLieu == null)
                return;

            _boLoc = trangThai;
            HienThiKetQua();
        }

        private void HienThiKetQua()
        {
            if (_duLieu == null)
                return;

            var ketQua = _duLieu.Clone();

            foreach (DataRow dong in _duLieu.Rows)
            {
                if (_boLoc.Length == 0 ||
                    Convert.ToString(dong["TrangThai"]) == _boLoc)
                {
                    ketQua.ImportRow(dong);
                }
            }

            dgSuCo.ItemsSource = ketQua.DefaultView;

            txtDanhSachTrong.Text = _duLieu.Rows.Count == 0
                ? "Chưa có yêu cầu sửa chữa trong phạm vi quản lý."
                : "Không có yêu cầu thuộc trạng thái đã chọn.";

            txtDanhSachTrong.Visibility = ketQua.Rows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            txtTrangThai.Text =
                $"Hiển thị {ketQua.Rows.Count} yêu cầu — "
                + (_boLoc.Length == 0
                    ? "Tất cả trạng thái"
                    : _boLoc);

            CapNhatNut();
        }

        private void ChonSuCo_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            CapNhatNut();
        }

        private void CapNhatNut()
        {
            // Tránh truy cập control chưa tạo xong trong InitializeComponent.
            if (btnXemChiTiet == null || btnTaiLai == null ||
                btnTatCa == null || btnChoTiepNhan == null ||
                btnDaTiepNhan == null || btnDaPhanCong == null ||
                btnDangXuLy == null || btnHoanThanh == null ||
                btnTuChoi == null)
            {
                return;
            }

            bool coQuyen =
                UserSession.CurrentUser?.VaiTro == "Nhân viên quản lý";

            bool choPhep = coQuyen && !_dangXuLy;
            bool choLoc = choPhep && _duLieu != null;

            btnTaiLai.IsEnabled = choPhep;

            btnXemChiTiet.IsEnabled =
                choLoc && dgSuCo.SelectedItem is DataRowView;

            btnTatCa.IsEnabled = choLoc;
            btnChoTiepNhan.IsEnabled = choLoc;
            btnDaTiepNhan.IsEnabled = choLoc;
            btnDaPhanCong.IsEnabled = choLoc;
            btnDangXuLy.IsEnabled = choLoc;
            btnHoanThanh.IsEnabled = choLoc;
            btnTuChoi.IsEnabled = choLoc;
        }

        // Gửi mã sự cố cho QuanLyWindow mở form chi tiết.
        private void XemChiTiet_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangXuLy ||
                UserSession.CurrentUser?.VaiTro != "Nhân viên quản lý")
            {
                return;
            }

            if (dgSuCo.SelectedItem is DataRowView dong)
            {
                string maSuCo =
                    Convert.ToString(dong["MaSuCo"]) ?? "";

                if (maSuCo.Length > 0)
                {
                    MoChiTiet?.Invoke(maSuCo);
                }
            }
        }
    }
}