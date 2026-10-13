using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class ChiTietHopDongView : UserControl
    {
        private readonly string _maHopDong;
        private bool _dangTai;
        private bool _daTai;

        public event Action? QuayLai;

        public ChiTietHopDongView(string maHopDong)
        {
            InitializeComponent();

            _maHopDong = maHopDong;
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai || _daTai)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền xem hợp đồng.";
                return;
            }

            _dangTai = true;
            txtThongBao.Text = "Đang tải chi tiết hợp đồng...";
            pnlChiTiet.Visibility = Visibility.Collapsed;

            try
            {
                using var connection = Database.CreateConnection();
                await connection.OpenAsync();

                var hopDong = new DataTable();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT
                            h.MaHopDong,
                            n.HoTen AS TenKhachThue,
                            n.CCCD,
                            n.SoDienThoai,
                            d.TenDayTro + N' - ' + p.SoPhong AS TenPhong,
                            p.LoaiPhong,
                            h.DaiDienBenChoThue,
                            h.NgayKy,
                            h.NgayBatDau,
                            h.NgayKetThuc,
                            h.GiaThue,
                            h.TienCoc,
                            h.KyThanhToan,
                            h.DieuKhoan,
                            h.TrangThai
                        FROM dbo.HopDongThue h
                        JOIN dbo.KhachThue k
                          ON k.MaKhachThue = h.MaKhachThue
                        JOIN dbo.NguoiDung n
                          ON n.MaNguoiDung = k.MaNguoiDung
                        JOIN dbo.PhongTro p
                          ON p.MaPhong = h.MaPhong
                        JOIN dbo.DayTro d
                          ON d.MaDayTro = p.MaDayTro
                        WHERE h.MaHopDong = @MaHopDong
                          AND EXISTS (
                              SELECT 1
                              FROM dbo.NhanVien nv
                              WHERE nv.MaDayTro = p.MaDayTro
                                AND nv.MaNguoiDung = @NguoiQuanLy
                                AND nv.ChucVu = N'Quản lý'
                                AND nv.TrangThaiLamViec = N'Đang làm'
                          );";

                    command.Parameters.Add(
                        "@MaHopDong", SqlDbType.VarChar, 20)
                        .Value = _maHopDong;

                    command.Parameters.Add(
                        "@NguoiQuanLy", SqlDbType.VarChar, 20)
                        .Value = user.MaNguoiDung;

                    using var reader = await command.ExecuteReaderAsync();
                    hopDong.Load(reader);
                }

                if (hopDong.Rows.Count == 0)
                {
                    txtThongBao.Text =
                        "Hợp đồng không tồn tại hoặc bạn không có quyền xem.";
                    return;
                }

                var thanhVien = new DataTable();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT
                            tv.HoTen,
                            tv.CCCD,
                            tv.SoDienThoai,
                            tv.QuanHe,
                            tv.TrangThai
                        FROM dbo.ThanhVienO tv
                        JOIN dbo.HopDongThue h
                          ON h.MaHopDong = tv.MaHopDong
                        JOIN dbo.PhongTro p
                          ON p.MaPhong = h.MaPhong
                        WHERE tv.MaHopDong = @MaHopDong
                          AND EXISTS (
                              SELECT 1
                              FROM dbo.NhanVien nv
                              WHERE nv.MaDayTro = p.MaDayTro
                                AND nv.MaNguoiDung = @NguoiQuanLy
                                AND nv.ChucVu = N'Quản lý'
                                AND nv.TrangThaiLamViec = N'Đang làm'
                          )
                        ORDER BY tv.HoTen, tv.MaThanhVien;";

                    command.Parameters.Add(
                        "@MaHopDong", SqlDbType.VarChar, 20)
                        .Value = _maHopDong;

                    command.Parameters.Add(
                        "@NguoiQuanLy", SqlDbType.VarChar, 20)
                        .Value = user.MaNguoiDung;

                    using var reader = await command.ExecuteReaderAsync();
                    thanhVien.Load(reader);
                }

                if (!IsLoaded)
                    return;

                pnlChiTiet.DataContext = hopDong.DefaultView[0];
                dgThanhVien.ItemsSource = thanhVien.DefaultView;

                txtThanhVienTrong.Visibility =
                    thanhVien.Rows.Count == 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                pnlChiTiet.Visibility = Visibility.Visible;
                txtThongBao.Text = "";
                _daTai = true;
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không tải được chi tiết hợp đồng: " + ex.Message;
            }
            finally
            {
                _dangTai = false;
            }
        }

        private void QuayLai_Click(
            object sender, RoutedEventArgs e)
        {
            QuayLai?.Invoke();
        }
    }
}