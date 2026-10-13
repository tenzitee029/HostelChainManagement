
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Owner
{
    public partial class XemHopDongHoaDonView : UserControl
    {
        public sealed record LuaChon(string? Ma, string Ten);

        public event Action<string>? XemChiTietHopDongRequested;
        public event Action<string>? XemChiTietHoaDonRequested;

        private bool _dangXemHopDong = true;
        private bool _daTaiBoLoc;
        private int _phienTaiPhong;
        private int _phienTaiKetQua;

        public XemHopDongHoaDonView()
        {
            InitializeComponent();

            ChuyenDanhMuc(true);

            cmbDayTro.SelectionChanged += DayTro_SelectionChanged;

            Loaded += XemHopDongHoaDonView_Loaded;
        }

        private async void XemHopDongHoaDonView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= XemHopDongHoaDonView_Loaded;

            btnXemKetQua.IsEnabled = false;

            try
            {
                DataTable dayTro =
                    await HopDongHoaDonChuTroService
                        .LayDanhSachDayTroAsync();

                var danhSach = new List<LuaChon>
                {
                    new LuaChon(null, "Tất cả dãy trọ")
                };

                foreach (DataRow row in dayTro.Rows)
                {
                    danhSach.Add(
                        new LuaChon(
                            Convert.ToString(row["MaDayTro"]),
                            Convert.ToString(row["TenDayTro"]) ?? ""));
                }

                NapCombo(cmbDayTro, danhSach);

                _daTaiBoLoc = true;

                await TaiDanhSachPhongAsync();
                await TaiKetQuaAsync();
            }
            catch (SqlException ex)
            {
                HienThiLoi(
                    "Không thể tải bộ lọc từ SQL Server: "
                    + ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                HienThiLoi(ex.Message);
            }
            catch (Exception ex)
            {
                HienThiLoi(
                    "Đã xảy ra lỗi khi khởi tạo màn hình: "
                    + ex.Message);
            }
            finally
            {
                btnXemKetQua.IsEnabled = true;
            }
        }

        private static void NapCombo(
            ComboBox combo,
            List<LuaChon> danhSach)
        {
            combo.ItemsSource = null;
            combo.Items.Clear();

            combo.DisplayMemberPath = "Ten";
            combo.SelectedValuePath = "Ma";
            combo.ItemsSource = danhSach;
            combo.SelectedIndex = 0;
            combo.IsEnabled = true;
        }

        private async void DayTro_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!_daTaiBoLoc)
                return;

            await TaiDanhSachPhongAsync();
        }

        private async Task TaiDanhSachPhongAsync()
        {
            int phienHienTai = ++_phienTaiPhong;
            string? maDayTro = LayMaDaChon(cmbDayTro);

            cmbPhong.IsEnabled = false;

            try
            {
                DataTable phong =
                    await HopDongHoaDonChuTroService
                        .LayDanhSachPhongAsync(maDayTro);

                if (phienHienTai != _phienTaiPhong)
                    return;

                var danhSach = new List<LuaChon>
                {
                    new LuaChon(null, "Tất cả phòng")
                };

                foreach (DataRow row in phong.Rows)
                {
                    string soPhong =
                        Convert.ToString(row["SoPhong"]) ?? "";

                    string tenDayTro =
                        Convert.ToString(row["TenDayTro"]) ?? "";

                    string tenHienThi = maDayTro == null
                        ? tenDayTro + " - Phòng " + soPhong
                        : "Phòng " + soPhong;

                    danhSach.Add(
                        new LuaChon(
                            Convert.ToString(row["MaPhong"]),
                            tenHienThi));
                }

                NapCombo(cmbPhong, danhSach);
            }
            catch (SqlException ex)
            {
                if (phienHienTai == _phienTaiPhong)
                {
                    HienThiLoi(
                        "Không thể tải danh sách phòng: "
                        + ex.Message);
                }
            }
            catch (Exception ex)
            {
                if (phienHienTai == _phienTaiPhong)
                {
                    HienThiLoi(
                        "Đã xảy ra lỗi khi tải phòng: "
                        + ex.Message);
                }
            }
        }

        private static string? LayMaDaChon(ComboBox combo)
        {
            return (combo.SelectedItem as LuaChon)?.Ma;
        }

        private void TabHopDong_Click(
            object sender,
            RoutedEventArgs e)
        {
            ChuyenDanhMuc(true);
            _ = TaiKetQuaAsync();
        }

        private void TabHoaDon_Click(
            object sender,
            RoutedEventArgs e)
        {
            ChuyenDanhMuc(false);
            _ = TaiKetQuaAsync();
        }

        private void ChuyenDanhMuc(bool xemHopDong)
        {
            _dangXemHopDong = xemHopDong;

            pnlHopDong.Visibility = xemHopDong
                ? Visibility.Visible
                : Visibility.Collapsed;

            pnlHoaDon.Visibility = xemHopDong
                ? Visibility.Collapsed
                : Visibility.Visible;

            txtNhanTrangThai.Text = xemHopDong
                ? "Trạng thái hợp đồng"
                : "Trạng thái hóa đơn";

            var mauXanh = new SolidColorBrush(
                Color.FromRgb(37, 99, 235));

            var mauXam = new SolidColorBrush(
                Color.FromRgb(107, 114, 128));

            btnTabHopDong.Foreground = xemHopDong
                ? mauXanh
                : mauXam;

            btnTabHoaDon.Foreground = xemHopDong
                ? mauXam
                : mauXanh;

            btnTabHopDong.BorderBrush = xemHopDong
                ? mauXanh
                : Brushes.Transparent;

            btnTabHoaDon.BorderBrush = xemHopDong
                ? Brushes.Transparent
                : mauXanh;

            cmbTrangThai.Items.Clear();

            cmbTrangThai.Items.Add(
                new ComboBoxItem
                {
                    Content = "Tất cả trạng thái"
                });

            string[] trangThai = xemHopDong
                ? new[]
                {
                    "Hiệu lực",
                    "Hết hạn",
                    "Đã thanh lý"
                }
                : new[]
                {
                    "Chưa thanh toán",
                    "Còn nợ",
                    "Đã thanh toán"
                };

            foreach (string giaTri in trangThai)
            {
                cmbTrangThai.Items.Add(
                    new ComboBoxItem
                    {
                        Content = giaTri
                    });
            }

            cmbTrangThai.SelectedIndex = 0;
        }

        private (DateTime? TuNgay, DateTime? DenNgay)
            LayKhoangThoiGian()
        {
            DateTime homNay = DateTime.Today;

            DateTime dauThang = new DateTime(
                homNay.Year,
                homNay.Month,
                1);

            switch (cmbThoiGian.SelectedIndex)
            {
                case 1:
                    return (
                        dauThang,
                        dauThang.AddMonths(1));

                case 2:
                    return (
                        dauThang.AddMonths(-1),
                        dauThang);

                case 3:
                    return (
                        dauThang.AddMonths(-2),
                        dauThang.AddMonths(1));

                case 4:
                    return (
                        new DateTime(homNay.Year, 1, 1),
                        new DateTime(homNay.Year + 1, 1, 1));

                default:
                    return (null, null);
            }
        }

        private string? LayTrangThaiDaChon()
        {
            if (cmbTrangThai.SelectedIndex <= 0)
                return null;

            return (cmbTrangThai.SelectedItem as ComboBoxItem)?
                .Content?.ToString();
        }

        private async void XemKetQua_Click(
            object sender,
            RoutedEventArgs e)
        {
            await TaiKetQuaAsync();
        }

        private async Task TaiKetQuaAsync()
        {
            if (!_daTaiBoLoc)
                return;

            int phienHienTai = ++_phienTaiKetQua;
            bool xemHopDong = _dangXemHopDong;

            string? maDayTro = LayMaDaChon(cmbDayTro);
            string? maPhong = LayMaDaChon(cmbPhong);
            string? trangThai = LayTrangThaiDaChon();

            var khoangThoiGian = LayKhoangThoiGian();

            btnXemKetQua.IsEnabled = false;

            if (xemHopDong)
            {
                dgvHopDong.ItemsSource = null;

                txtEmptyHopDong.Text =
                    "Đang tải danh sách hợp đồng...";

                txtEmptyHopDong.Visibility = Visibility.Visible;
            }
            else
            {
                dgvHoaDon.ItemsSource = null;

                txtEmptyHoaDon.Text =
                    "Đang tải danh sách hóa đơn...";

                txtEmptyHoaDon.Visibility = Visibility.Visible;
            }

            try
            {
                if (xemHopDong)
                {
                    DataTable table =
                        await HopDongHoaDonChuTroService
                            .TimHopDongAsync(
                                maDayTro,
                                maPhong,
                                khoangThoiGian.TuNgay,
                                khoangThoiGian.DenNgay,
                                trangThai);

                    if (phienHienTai != _phienTaiKetQua)
                        return;

                    dgvHopDong.ItemsSource = table.DefaultView;

                    if (table.Rows.Count == 0)
                    {
                        txtEmptyHopDong.Text =
                            "Không tìm thấy dữ liệu hợp đồng phù hợp "
                            + "với điều kiện tìm kiếm.";

                        txtEmptyHopDong.Visibility =
                            Visibility.Visible;
                    }
                    else
                    {
                        txtEmptyHopDong.Visibility =
                            Visibility.Collapsed;
                    }
                }
                else
                {
                    DataTable table =
                        await HopDongHoaDonChuTroService
                            .TimHoaDonAsync(
                                maDayTro,
                                maPhong,
                                khoangThoiGian.TuNgay,
                                khoangThoiGian.DenNgay,
                                trangThai);

                    if (phienHienTai != _phienTaiKetQua)
                        return;

                    dgvHoaDon.ItemsSource = table.DefaultView;

                    if (table.Rows.Count == 0)
                    {
                        txtEmptyHoaDon.Text =
                            "Không tìm thấy dữ liệu hóa đơn phù hợp "
                            + "với điều kiện tìm kiếm.";

                        txtEmptyHoaDon.Visibility =
                            Visibility.Visible;
                    }
                    else
                    {
                        txtEmptyHoaDon.Visibility =
                            Visibility.Collapsed;
                    }
                }
            }
            catch (SqlException ex)
            {
                if (phienHienTai == _phienTaiKetQua)
                {
                    HienThiLoi(
                        "Không thể truy vấn SQL Server: "
                        + ex.Message);
                }
            }
            catch (InvalidOperationException ex)
            {
                if (phienHienTai == _phienTaiKetQua)
                {
                    HienThiLoi(ex.Message);
                }
            }
            catch (Exception ex)
            {
                if (phienHienTai == _phienTaiKetQua)
                {
                    HienThiLoi(
                        "Đã xảy ra lỗi khi tra cứu: "
                        + ex.Message);
                }
            }
            finally
            {
                if (phienHienTai == _phienTaiKetQua)
                {
                    btnXemKetQua.IsEnabled = true;
                }
            }
        }

        private void HienThiLoi(string noiDung)
        {
            if (_dangXemHopDong)
            {
                dgvHopDong.ItemsSource = null;

                txtEmptyHopDong.Text = noiDung;
                txtEmptyHopDong.Visibility = Visibility.Visible;
            }
            else
            {
                dgvHoaDon.ItemsSource = null;

                txtEmptyHoaDon.Text = noiDung;
                txtEmptyHoaDon.Visibility = Visibility.Visible;
            }
        }

        private void XemHopDong_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button
                || button.DataContext is not DataRowView hopDong)
            {
                return;
            }

            string maHopDong =
                Convert.ToString(hopDong["MaHopDong"]) ?? "";

            if (string.IsNullOrWhiteSpace(maHopDong))
            {
                MessageBox.Show(
                    "Không xác định được mã hợp đồng.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            XemChiTietHopDongRequested?.Invoke(maHopDong);
        }

        private void XemHoaDon_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button
                || button.DataContext is not DataRowView hoaDon)
            {
                return;
            }

            string maHoaDon =
                Convert.ToString(hoaDon["MaHoaDon"]) ?? "";

            if (string.IsNullOrWhiteSpace(maHoaDon))
            {
                MessageBox.Show(
                    "Không xác định được mã hóa đơn.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            XemChiTietHoaDonRequested?.Invoke(maHoaDon);
        }
    }
}
