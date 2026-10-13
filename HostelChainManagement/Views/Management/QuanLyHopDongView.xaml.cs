using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class QuanLyHopDongView : UserControl
    {
        public event Action<string>? MoXemHopDong;
        private void Xem_Click(object sender, RoutedEventArgs e)
        {
            var user = UserSession.CurrentUser;

            if (_dangTai || user == null ||
                user.VaiTro != "Nhân viên quản lý")
                return;

            if (dgHopDong.SelectedItem is not DataRowView hopDong)
                return;

            string maHopDong =
                Convert.ToString(hopDong["MaHopDong"]) ?? "";

            if (!string.IsNullOrWhiteSpace(maHopDong))
                MoXemHopDong?.Invoke(maHopDong);
        }
        public event Action<string>? MoThanhLy;

        private void ThanhLy_Click(object sender, RoutedEventArgs e)
        {
            if (_dangTai)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
                return;

            if (dgHopDong.SelectedItem is DataRowView hopDong)
            {
                string maHopDong =
                    Convert.ToString(hopDong["MaHopDong"]) ?? "";

                if (maHopDong.Length > 0)
                    MoThanhLy?.Invoke(maHopDong);
            }
        }
        public event Action<string>? MoGiaHan;

        private void ChonHopDong_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            CapNhatNut();
        }

        private void CapNhatNut()
        {
            // Tránh lỗi khi XAML đang khởi tạo.
            if (btnXem == null || btnGiaHan == null || btnThanhLy == null)
                return;

            var user = UserSession.CurrentUser;

            bool coQuyen =
                user != null &&
                user.VaiTro == "Nhân viên quản lý";

            bool daChon =
                coQuyen &&
                !_dangTai &&
                dgHopDong.SelectedItem is DataRowView;

            string trangThai =
                dgHopDong.SelectedItem is DataRowView dong
                    ? Convert.ToString(dong["TrangThai"]) ?? ""
                    : "";

            btnXem.IsEnabled = daChon;

            btnGiaHan.IsEnabled =
                daChon && trangThai == "Hiệu lực";

            btnThanhLy.IsEnabled =
                daChon &&
                (trangThai == "Hiệu lực" || trangThai == "Hết hạn");
        }


        private void GiaHan_Click(object sender, RoutedEventArgs e)
        {
            if (_dangTai)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
                return;

            if (dgHopDong.SelectedItem is DataRowView hopDong)
            {
                string maHopDong =
                    Convert.ToString(hopDong["MaHopDong"]) ?? "";

                if (maHopDong.Length > 0)
                    MoGiaHan?.Invoke(maHopDong);
            }
        }
        public event Action? MoLapHopDong;

        private void LapHopDong_Click(object sender, RoutedEventArgs e)
        {
            var user = UserSession.CurrentUser;

            if (_dangTai || user == null ||
                user.VaiTro != "Nhân viên quản lý")
            {
                return;
            }

            MoLapHopDong?.Invoke();
        }
        private DataTable? _duLieu;
        private bool _dangTai;

        public QuanLyHopDongView()
        {
            InitializeComponent();

            Loaded -= View_Loaded;
            Loaded += View_Loaded;

            btnTimKiem.Click -= TimKiem_Click;
            btnTimKiem.Click += TimKiem_Click;
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                _duLieu = null;
                dgHopDong.ItemsSource = null;
                btnTimKiem.IsEnabled = false;

                MessageBox.Show(
                    "Bạn không có quyền xem danh sách hợp đồng.",
                    "Thông báo");
                return;
            }

            _dangTai = true;
            btnTimKiem.IsEnabled = false;
            txtTimKiem.IsEnabled = false;

            _duLieu = null;
            dgHopDong.ItemsSource = null;

            try
            {
                using var connection = Database.CreateConnection();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT
                        h.MaHopDong,
                        h.MaKhachThue,
                        h.MaPhong,
                        n.HoTen AS TenKhachThue,
                        n.CCCD,
                        n.SoDienThoai,
                        p.SoPhong,
                        d.MaDayTro,
                        d.TenDayTro,
                        h.NgayKy,
                        h.NgayBatDau,
                        h.NgayKetThuc,
                        h.TienCoc,
                        h.GiaThue,
                        h.KyThanhToan,
                        h.DaiDienBenChoThue,
                        h.DieuKhoan,
                        h.TrangThai
                    FROM dbo.HopDongThue AS h
                    INNER JOIN dbo.KhachThue AS k
                        ON k.MaKhachThue = h.MaKhachThue
                    INNER JOIN dbo.NguoiDung AS n
                        ON n.MaNguoiDung = k.MaNguoiDung
                    INNER JOIN dbo.PhongTro AS p
                        ON p.MaPhong = h.MaPhong
                    INNER JOIN dbo.DayTro AS d
                        ON d.MaDayTro = p.MaDayTro
                    WHERE EXISTS (
                        SELECT 1
                        FROM dbo.NhanVien AS nv
                        WHERE nv.MaDayTro = p.MaDayTro
                          AND nv.MaNguoiDung = @MaNguoiDung
                          AND nv.ChucVu = N'Quản lý'
                          AND nv.TrangThaiLamViec = N'Đang làm'
                    )
                    ORDER BY h.NgayBatDau DESC, h.MaHopDong;";

                command.Parameters.Add(
                    "@MaNguoiDung", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;

                await connection.OpenAsync();

                using var reader =
                    await command.ExecuteReaderAsync();

                var table = new DataTable();
                table.Load(reader);

                _duLieu = table;
                HienThiKetQua();
            }
            catch (SqlException ex)
            {
                MessageBox.Show(
                    "Không tải được hợp đồng.\n\nChi tiết SQL:\n"
                    + ex.Message,
                    "Lỗi tải hợp đồng",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không tải được hợp đồng:\n" + ex.Message,
                    "Lỗi tải dữ liệu",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _dangTai = false;
                txtTimKiem.IsEnabled = true;
                btnTimKiem.IsEnabled = _duLieu != null;

                CapNhatNut();
            }
        }

        private void TimKiem_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_dangTai)
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
                    "MaHopDong",
                    "TenKhachThue",
                    "SoPhong",
                    "TenDayTro"
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

            dgHopDong.ItemsSource = ketQua.DefaultView;
            CapNhatNut();
        }
    }
}