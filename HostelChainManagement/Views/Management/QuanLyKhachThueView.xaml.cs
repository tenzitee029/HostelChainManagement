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
                        CAST(N'Người đứng tên hợp đồng' AS NVARCHAR(50))
                            AS LoaiHoSo,
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
                            WHEN hd.DangHieuLuc = 1 THEN N'Đang thuê'
                            WHEN hd.MaHopDong IS NULL THEN N'Chưa có hợp đồng'
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
                                AND nv.MaNguoiDung = @MaNguoiDung
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

                    WHERE hd.MaHopDong IS NOT NULL
                       OR EXISTS (
                           SELECT 1
                           FROM dbo.NhanVien nv
                           WHERE nv.MaDayTro = k.MaDayTro
                             AND nv.MaNguoiDung = @MaNguoiDung
                             AND nv.ChucVu = N'Quản lý'
                             AND nv.TrangThaiLamViec = N'Đang làm'
                       )

                    UNION ALL

                    SELECT
                        tv.MaThanhVien AS MaKhachThue,
                        CAST(N'Thành viên ở cùng' AS NVARCHAR(50))
                            AS LoaiHoSo,
                        CAST(NULL AS VARCHAR(20)) AS MaNguoiDung,
                        p.MaDayTro,
                        tv.HoTen,
                        tv.CCCD,
                        tv.SoDienThoai,
                        CAST(NULL AS NVARCHAR(255)) AS DiaChi,
                        p.MaPhong,
                        h.MaHopDong,

                        CASE
                            WHEN tv.TrangThai = N'Đang ở'
                             AND h.TrangThai = N'Hiệu lực'
                             AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                             AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                            THEN d.TenDayTro + N' - ' + p.SoPhong
                            ELSE N'—'
                        END AS PhongDangThue,

                        CASE
                            WHEN tv.TrangThai = N'Đang ở'
                             AND h.TrangThai = N'Hiệu lực'
                             AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                             AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                            THEN N'Đang ở cùng'
                            ELSE N'Không ở hiện tại'
                        END AS TrangThai

                    FROM dbo.ThanhVienO tv
                    JOIN dbo.HopDongThue h
                      ON h.MaHopDong = tv.MaHopDong
                    JOIN dbo.PhongTro p ON p.MaPhong = h.MaPhong
                    JOIN dbo.DayTro d ON d.MaDayTro = p.MaDayTro

                    WHERE EXISTS (
                        SELECT 1
                        FROM dbo.NhanVien nv
                        WHERE nv.MaDayTro = p.MaDayTro
                          AND nv.MaNguoiDung = @MaNguoiDung
                          AND nv.ChucVu = N'Quản lý'
                          AND nv.TrangThaiLamViec = N'Đang làm'
                    )

                    ORDER BY HoTen, MaKhachThue;";

                command.Parameters.Add(
                    "@MaNguoiDung", SqlDbType.VarChar, 20)
                    .Value = user!.MaNguoiDung;

                await connection.OpenAsync();

                using var reader = await command.ExecuteReaderAsync();

                var table = new DataTable();
                table.Load(reader);

                if (!IsLoaded)
                    return;

                _duLieu = table;
                HienThiKetQua();
            }
            catch (Exception ex)
            {
                HienThiLoi(
                    "Không tải được danh sách khách thuê: " + ex.Message);
            }
            finally
            {
                _dangTai = false;
                CapNhatNut();
            }
        }

        private static bool LaNguoiDungTen(DataRowView khach)
        {
            return Convert.ToString(khach["LoaiHoSo"])
                == "Người đứng tên hợp đồng";
        }

        private void CapNhatNut()
        {
            if (btnCapNhat == null || btnXoaKhach == null ||
                btnThemKhach == null || btnTimKiem == null)
                return;

            bool sanSang =
                _coQuyen && !_dangTai &&
                !_dangXoa && _duLieu != null;

            bool daChonHoSo =
                sanSang && dgKhachThue.SelectedItem is DataRowView;

            btnThemKhach.IsEnabled = sanSang;
            btnTimKiem.IsEnabled = sanSang;
            txtTimKiem.IsEnabled = sanSang;

            btnCapNhat.IsEnabled = daChonHoSo;
            btnXoaKhach.IsEnabled = daChonHoSo;
        }

        private void Them_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_coQuyen || _dangTai || _dangXoa || _duLieu == null)
                return;

            MoForm?.Invoke(null);
        }

        private void ChonKhach_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            CapNhatNut();

            if (_duLieu == null || _dangTai)
                return;

            txtTrangThai.Text =
                $"Tổng số hồ sơ đã tải: {_duLieu.Rows.Count}.";
        }

        private void CapNhat_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_coQuyen || _dangTai || _dangXoa)
                return;

            if (dgKhachThue.SelectedItem is DataRowView khach)
            {
                MoForm?.Invoke(khach);
            }
        }

        private async void Xoa_Click(
            object sender, RoutedEventArgs e)
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

            string maKhach = Convert.ToString(khach["MaKhachThue"]) ?? "";
            string hoTen = Convert.ToString(khach["HoTen"]) ?? "";
            bool laThanhVien = !LaNguoiDungTen(khach);

            var xacNhan = MessageBox.Show(
                $"Bạn có chắc muốn xóa hồ sơ “{hoTen}”?\n\n"
                + (laThanhVien
                    ? "Thành viên sẽ bị xóa khỏi phòng/hợp đồng. "
                      + "Tài khoản đăng nhập vẫn được giữ lại."
                    : "Chỉ xóa khách chưa có hợp đồng. "
                      + "Tài khoản đăng nhập được giữ lại."),
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
                await connection.OpenAsync();

                using var transaction =
                    connection.BeginTransaction(IsolationLevel.Serializable);

                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                if (laThanhVien)
                {
                    command.CommandText = @"
                        DECLARE @DaXoa TABLE (MaThanhVien VARCHAR(20));
                        DELETE tv
                        OUTPUT DELETED.MaThanhVien INTO @DaXoa
                        FROM dbo.ThanhVienO tv
                        JOIN dbo.HopDongThue h ON h.MaHopDong = tv.MaHopDong
                        JOIN dbo.PhongTro p ON p.MaPhong = h.MaPhong
                        WHERE tv.MaThanhVien = @MaKhach
                          AND EXISTS (
                              SELECT 1 FROM dbo.NhanVien nv
                              WHERE nv.MaDayTro = p.MaDayTro
                                AND nv.MaNguoiDung = @NguoiQuanLy
                                AND nv.ChucVu = N'Quản lý'
                                AND nv.TrangThaiLamViec = N'Đang làm'
                          );
                        SELECT COUNT(*) FROM @DaXoa;";
                }
                else
                {
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
                }

                command.Parameters.Add(
                    "@MaKhach", SqlDbType.VarChar, 20).Value = maKhach;
                command.Parameters.Add(
                    "@NguoiQuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;

                object? ketQua = await command.ExecuteScalarAsync();
                daXoa = ketQua != null && Convert.ToInt32(ketQua) == 1;

                transaction.Commit();

                MessageBox.Show(
                    daXoa
                        ? (laThanhVien
                            ? "Đã xóa thành viên khỏi phòng. Tài khoản đăng nhập vẫn được giữ lại."
                            : "Xóa hồ sơ khách thuê thành công.")
                        : (laThanhVien
                            ? "Không thể xóa thành viên: hồ sơ không tồn tại hoặc không có quyền."
                            : "Không thể xóa: khách đã có hợp đồng, "
                              + "hồ sơ không tồn tại hoặc bạn không còn quyền."),
                    "Thông báo",
                    MessageBoxButton.OK,
                    daXoa
                        ? MessageBoxImage.Information
                        : MessageBoxImage.Warning);
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                MessageBox.Show(
                    "Không thể xóa vì hồ sơ còn dữ liệu liên quan.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không xóa được hồ sơ:\n" + ex.Message);
            }
            finally
            {
                _dangXoa = false;
                IsEnabled = true;
                CapNhatNut();
            }

            if (daXoa)
                await TaiDanhSachAsync();
        }

        private void TimKiem_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_dangTai && !_dangXoa)
                HienThiKetQua();
        }

        private void TimKiem_KeyDown(
            object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !_dangTai && !_dangXoa)
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
                    "HoTen", "CCCD", "SoDienThoai",
                    "PhongDangThue", "LoaiHoSo", "TrangThai"
                })
                {
                    string noiDung = Convert.ToString(dong[cot]) ?? "";

                    if (noiDung.IndexOf(
                        tuKhoa, StringComparison.OrdinalIgnoreCase) >= 0)
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
                : "Không có hồ sơ phù hợp với nội dung tìm kiếm.";

            txtDanhSachTrong.Visibility = ketQua.Rows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            txtTrangThai.Text =
                $"Hiển thị {ketQua.Rows.Count} / {_duLieu.Rows.Count} hồ sơ.";

            CapNhatNut();
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
    }
}