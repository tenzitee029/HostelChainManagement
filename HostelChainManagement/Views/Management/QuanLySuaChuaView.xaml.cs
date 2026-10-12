using System;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class QuanLySuaChuaView : UserControl
    {
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

            Loaded += View_Loaded;
            dgSuCo.SelectionChanged += ChonSuCo_Changed;
            btnXemChiTiet.Click += XemChiTiet_Click;
            btnTaiLai.Click += TaiLai_Click;

            btnChoTiepNhan.Click += (_, _) => LocTrangThai("Chờ tiếp nhận");
            btnDaTiepNhan.Click += (_, _) => LocTrangThai("Đã tiếp nhận");
            btnDaPhanCong.Click += (_, _) => LocTrangThai("Đã phân công");


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
                _duLieu = null;
                dgSuCo.ItemsSource = null;
                txtDanhSachTrong.Text = "Bạn không có quyền xem sự cố.";
                txtDanhSachTrong.Visibility = Visibility.Visible;
                CapNhatNut();
                return;
            }

            _dangXuLy = true;
            CapNhatNut();

            _duLieu = null;
            dgSuCo.ItemsSource = null;

            txtDanhSachTrong.Text = "Đang tải yêu cầu sửa chữa...";
            txtDanhSachTrong.Visibility = Visibility.Visible;
            txtTrangThai.Text = "";

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
                    JOIN dbo.PhongTro p ON p.MaPhong = s.MaPhong
                    JOIN dbo.DayTro d ON d.MaDayTro = p.MaDayTro
                    JOIN dbo.NguoiDung n ON n.MaNguoiDung = s.MaNguoiGui
                    WHERE " + DieuKienQuyen + @"
                    ORDER BY
                        CASE WHEN s.MucDoUuTien = N'Khẩn cấp'
                            THEN 0 ELSE 1 END,
                        s.NgayGui DESC,
                        s.MaSuCo;";

                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;

                await connection.OpenAsync();

                using var reader = await command.ExecuteReaderAsync();

                var table = new DataTable();
                table.Load(reader);

                _duLieu = table;
                HienThiKetQua();
            }
            catch (Exception ex)
            {
                txtDanhSachTrong.Text = "Không tải được yêu cầu sửa chữa.";
                txtDanhSachTrong.Visibility = Visibility.Visible;
                txtTrangThai.Text = "Lỗi tải dữ liệu.";

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

        private void LocTrangThai(string trangThai)
        {
            if (_dangXuLy)
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
                + (_boLoc.Length == 0 ? "Tất cả trạng thái" : _boLoc);

            CapNhatNut();
        }

        private void ChonSuCo_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            CapNhatNut();
        }

        private void CapNhatNut()
        {
            bool coQuyen =
                UserSession.CurrentUser?.VaiTro == "Nhân viên quản lý";

            bool choPhep = coQuyen && !_dangXuLy;

            btnTaiLai.IsEnabled = choPhep;

            btnXemChiTiet.IsEnabled =
                choPhep &&
                _duLieu != null &&
                dgSuCo.SelectedItem is DataRowView;

            bool choLoc = choPhep && _duLieu != null;


            btnChoTiepNhan.IsEnabled = choLoc;
            btnDaTiepNhan.IsEnabled = choLoc;
            btnDaPhanCong.IsEnabled = choLoc;

        }

        private async void XemChiTiet_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangXuLy ||
                dgSuCo.SelectedItem is not DataRowView dong)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
                return;

            string maSuCo = Convert.ToString(dong["MaSuCo"]) ?? "";

            _dangXuLy = true;
            CapNhatNut();

            try
            {
                using var connection = Database.CreateConnection();
                await connection.OpenAsync();

                var noiDung = new StringBuilder();

                // Đọc lại thông tin và kiểm tra quyền tại thời điểm xem.
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT
                            s.MaSuCo, s.NgayGui, s.NoiDungSuCo,
                            s.MucDoUuTien, s.TrangThai,
                            s.ThoiGianMongMuon, s.NgayTiepNhan,
                            s.LyDoTuChoi,
                            n.HoTen,
                            d.TenDayTro + N' - ' + p.SoPhong AS TenPhong
                        FROM dbo.PhieuSuCo s
                        JOIN dbo.PhongTro p ON p.MaPhong = s.MaPhong
                        JOIN dbo.DayTro d ON d.MaDayTro = p.MaDayTro
                        JOIN dbo.NguoiDung n
                          ON n.MaNguoiDung = s.MaNguoiGui
                        WHERE s.MaSuCo = @MaSuCo
                          AND " + DieuKienQuyen + ";";

                    command.Parameters.Add(
                        "@MaSuCo", SqlDbType.VarChar, 20)
                        .Value = maSuCo;
                    command.Parameters.Add(
                        "@QuanLy", SqlDbType.VarChar, 20)
                        .Value = user.MaNguoiDung;

                    using var reader = await command.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        MessageBox.Show(
                            "Yêu cầu không còn tồn tại hoặc bạn không có quyền xem.");
                        return;
                    }

                    noiDung.AppendLine("Yêu cầu: " + maSuCo);
                    noiDung.AppendLine("Phòng: " + reader["TenPhong"]);
                    noiDung.AppendLine("Người gửi: " + reader["HoTen"]);
                    noiDung.AppendLine(
                        "Ngày gửi: " + Convert.ToDateTime(reader["NgayGui"])
                            .ToString("dd/MM/yyyy HH:mm"));
                    noiDung.AppendLine("Ưu tiên: " + reader["MucDoUuTien"]);
                    noiDung.AppendLine("Trạng thái: " + reader["TrangThai"]);
                    noiDung.AppendLine(
                        "Thời gian mong muốn: " + reader["ThoiGianMongMuon"]);

                    if (reader["NgayTiepNhan"] != DBNull.Value)
                    {
                        noiDung.AppendLine(
                            "Tiếp nhận: "
                            + Convert.ToDateTime(reader["NgayTiepNhan"])
                                .ToString("dd/MM/yyyy HH:mm"));
                    }

                    noiDung.AppendLine();
                    noiDung.AppendLine("Nội dung:");
                    noiDung.AppendLine(
                        Convert.ToString(reader["NoiDungSuCo"]));

                    if (reader["LyDoTuChoi"] != DBNull.Value)
                    {
                        noiDung.AppendLine(
                            "Lý do từ chối: " + reader["LyDoTuChoi"]);
                    }
                }

                // Các lần phân công/sửa chữa của yêu cầu.
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT n.HoTen, ct.TienDo, ct.TrangThai,
                               ct.NoiDungXuLy, ct.ChiPhiVatTu
                        FROM dbo.ChiTietSuaChua ct
                        JOIN dbo.NhanVien nv ON nv.MaNhanVien = ct.MaNhanVien
                        JOIN dbo.NguoiDung n ON n.MaNguoiDung = nv.MaNguoiDung
                        JOIN dbo.PhieuSuCo s ON s.MaSuCo = ct.MaSuCo
                        JOIN dbo.PhongTro p ON p.MaPhong = s.MaPhong
                        WHERE ct.MaSuCo = @MaSuCo
                          AND " + DieuKienQuyen + @"
                        ORDER BY ct.NgayPhanCong, ct.MaChiTietSuaChua;";

                    command.Parameters.Add(
                        "@MaSuCo", SqlDbType.VarChar, 20)
                        .Value = maSuCo;
                    command.Parameters.Add(
                        "@QuanLy", SqlDbType.VarChar, 20)
                        .Value = user.MaNguoiDung;

                    using var reader = await command.ExecuteReaderAsync();

                    noiDung.AppendLine();
                    noiDung.AppendLine("Thông tin phân công:");

                    bool coPhanCong = false;

                    while (await reader.ReadAsync())
                    {
                        coPhanCong = true;

                        noiDung.AppendLine(
                            "- " + reader["HoTen"]
                            + " | " + reader["TienDo"]
                            + " | " + reader["TrangThai"]);

                        noiDung.AppendLine(
                            "  Xử lý: " + reader["NoiDungXuLy"]);

                        noiDung.AppendLine(
                            "  Chi phí vật tư: "
                            + Convert.ToDecimal(reader["ChiPhiVatTu"])
                                .ToString("N0") + " VNĐ");
                    }

                    if (!coPhanCong)
                        noiDung.AppendLine("Chưa có phân công.");
                }

                // Cửa sổ đọc chi tiết, có thanh cuộn.
                var cuaSo = new Window
                {
                    Title = "Chi tiết yêu cầu sửa chữa",
                    Width = 650,
                    Height = 550,
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
                    "Lỗi xem chi tiết",
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