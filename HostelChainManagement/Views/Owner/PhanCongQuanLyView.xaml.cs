
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services.Owner;

namespace QuanLyChuoiNhaTro.Views.Owner
{
    public partial class PhanCongQuanLyView : UserControl
    {
        private const int SoNhanVienMoiTrang = 10;

        private DataTable? _nhanVienData;
        private bool _dangTai;
        private bool _dangDoiBoLoc;
        private int _trangHienTai = 1;
        private int _tongSoTrang = 1;

        public PhanCongQuanLyView()
        {
            InitializeComponent();

            Loaded += PhanCongQuanLyView_Loaded;
        }

        private async void PhanCongQuanLyView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= PhanCongQuanLyView_Loaded;

            await TaiDuLieuAsync();
        }

        private async Task TaiDuLieuAsync()
        {
            if (_dangTai)
                return;

            string maDayDangChon = "";

            if (dgvDayTro.SelectedItem is DataRowView dayTroDaChon)
            {
                maDayDangChon = LayGiaTri(
                    dayTroDaChon,
                    "MaDayTro");
            }

            _dangTai = true;
            _nhanVienData = null;

            dgvDayTro.ItemsSource = null;
            dgvNhanVien.ItemsSource = null;

            txtDayEmpty.Text = "Đang tải danh sách dãy trọ...";
            txtDayEmpty.Visibility = Visibility.Visible;

            txtNhanVienEmpty.Text =
                "Đang tải danh sách nhân viên...";

            txtNhanVienEmpty.Visibility = Visibility.Visible;

            bool taiThanhCong = false;

            try
            {
                DataTable dayTro =
                    await PhanCongQuanLyService.GetDanhSachDayTroAsync();

                _nhanVienData =
                    await PhanCongQuanLyService.GetNhanVienQuanLyAsync();

                dgvDayTro.ItemsSource = dayTro.DefaultView;

                if (dayTro.Rows.Count == 0)
                {
                    txtDayEmpty.Text =
                        "Chưa có dãy trọ thuộc tài khoản này.";

                    txtDayEmpty.Visibility = Visibility.Visible;
                }
                else
                {
                    txtDayEmpty.Visibility = Visibility.Collapsed;
                }

                if (!string.IsNullOrWhiteSpace(maDayDangChon))
                {
                    foreach (DataRowView row in dayTro.DefaultView)
                    {
                        if (LayGiaTri(row, "MaDayTro") == maDayDangChon)
                        {
                            dgvDayTro.SelectedItem = row;
                            break;
                        }
                    }
                }

                taiThanhCong = true;
            }
            catch (InvalidOperationException ex)
            {
                HienThiLoi(ex.Message);
            }
            catch (SqlException)
            {
                HienThiLoi(
                    "Không thể tải dữ liệu phân công. "
                    + "Vui lòng kiểm tra SQL Server và các bảng liên quan.");
            }
            catch (Exception)
            {
                HienThiLoi(
                    "Đã xảy ra lỗi khi tải danh sách phân công.");
            }
            finally
            {
                _dangTai = false;
            }

            if (taiThanhCong)
            {
                DatBoLocMacDinh();
                CapNhatDanhSachNhanVien();
            }
        }

        private void DatBoLocMacDinh()
        {
            _dangDoiBoLoc = true;

            cmbBoLoc.SelectedIndex = 0;
            txtTimKiem.Clear();

            _trangHienTai = 1;

            _dangDoiBoLoc = false;
        }

        private void DayTro_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_dangTai)
                return;

            DatBoLocMacDinh();
            CapNhatDanhSachNhanVien();
        }

        private void BoLoc_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _dangTai || _dangDoiBoLoc)
                return;

            _trangHienTai = 1;
            CapNhatDanhSachNhanVien();
        }

        private void TimKiem_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (!IsLoaded || _dangTai || _dangDoiBoLoc)
                return;

            _trangHienTai = 1;
            CapNhatDanhSachNhanVien();
        }

        private void CapNhatDanhSachNhanVien()
        {
            dgvNhanVien.SelectedItem = null;
            dgvNhanVien.ItemsSource = null;

            if (dgvDayTro.SelectedItem is not DataRowView dayTro)
            {
                txtNhanVienEmpty.Text =
                    "Chọn một dãy trọ để xem nhân viên quản lý.";

                txtNhanVienEmpty.Visibility = Visibility.Visible;

                txtSoLuong.Text = "0 nhân viên";
                txtTrang.Text = "1/1";

                btnTrangTruoc.IsEnabled = false;
                btnTrangSau.IsEnabled = false;

                txtHuongDan.Text =
                    "Hãy chọn một dãy trọ để xem nhân viên quản lý.";

                return;
            }

            if (_nhanVienData == null)
                return;

            string maDay = LayGiaTri(dayTro, "MaDayTro");
            string tenDay = LayGiaTri(dayTro, "TenDayTro");

            string tuKhoa = txtTimKiem.Text.Trim();
            int boLoc = cmbBoLoc.SelectedIndex;

            var danhSach = new List<DataRow>();

            foreach (DataRow row in _nhanVienData.Rows)
            {
                string maNhanVien =
                    Convert.ToString(row["MaNhanVien"]) ?? "";

                string hoTen =
                    Convert.ToString(row["HoTen"]) ?? "";

                string maDayDuocGan =
                    row["MaDayTro"] == DBNull.Value
                        ? ""
                        : Convert.ToString(row["MaDayTro"]) ?? "";

                bool dangPhuTrach =
                    string.Equals(
                        maDayDuocGan,
                        maDay,
                        StringComparison.OrdinalIgnoreCase);

                bool chuaPhanCong =
                    string.IsNullOrWhiteSpace(maDayDuocGan);

                bool dangOdayKhac =
                    !chuaPhanCong && !dangPhuTrach;

                bool phuHopBoLoc = boLoc switch
                {
                    0 => dangPhuTrach,
                    1 => chuaPhanCong,
                    2 => dangOdayKhac,
                    _ => false
                };

                if (!phuHopBoLoc)
                    continue;

                if (!string.IsNullOrWhiteSpace(tuKhoa))
                {
                    bool timThay =
                        ChuaTuKhoa(maNhanVien, tuKhoa)
                        || ChuaTuKhoa(hoTen, tuKhoa);

                    if (!timThay)
                        continue;
                }

                row["HoTenHienThi"] = dangPhuTrach
                    ? hoTen + " (Đang phụ trách)"
                    : hoTen;

                danhSach.Add(row);
            }

            int tongSoNhanVien = danhSach.Count;

            _tongSoTrang = Math.Max(
                1,
                (tongSoNhanVien + SoNhanVienMoiTrang - 1)
                / SoNhanVienMoiTrang);

            _trangHienTai = Math.Clamp(
                _trangHienTai,
                1,
                _tongSoTrang);

            DataTable bangHienThi = _nhanVienData.Clone();

            foreach (DataRow row in danhSach
                .Skip((_trangHienTai - 1) * SoNhanVienMoiTrang)
                .Take(SoNhanVienMoiTrang))
            {
                bangHienThi.ImportRow(row);
            }

            dgvNhanVien.ItemsSource = bangHienThi.DefaultView;

            txtSoLuong.Text =
                tongSoNhanVien + " nhân viên";

            txtTrang.Text =
                _trangHienTai + "/" + _tongSoTrang;

            btnTrangTruoc.IsEnabled = _trangHienTai > 1;
            btnTrangSau.IsEnabled =
                _trangHienTai < _tongSoTrang;

            if (tongSoNhanVien == 0)
            {
                txtNhanVienEmpty.Text = boLoc switch
                {
                    0 => "Dãy trọ này chưa có nhân viên quản lý. "
                         + "Chọn “Chưa phân công” để gán người mới.",

                    1 => "Không có nhân viên chưa phân công "
                         + "phù hợp với tìm kiếm.",

                    2 => "Không có nhân viên ở dãy khác "
                         + "phù hợp với tìm kiếm.",

                    _ => "Không tìm thấy nhân viên."
                };

                txtNhanVienEmpty.Visibility = Visibility.Visible;
            }
            else
            {
                txtNhanVienEmpty.Visibility = Visibility.Collapsed;
            }

            txtHuongDan.Text =
                "Đang xem dãy: " + tenDay
                + ". Đổi bộ lọc để phân công hoặc điều chuyển nhân viên.";

            txtHuongDan.Foreground = new SolidColorBrush(
                Color.FromRgb(107, 114, 128));
        }

        private static bool ChuaTuKhoa(
            string noiDung,
            string tuKhoa)
        {
            var soSanh =
                CultureInfo.GetCultureInfo("vi-VN").CompareInfo;

            return soSanh.IndexOf(
                noiDung,
                tuKhoa,
                CompareOptions.IgnoreCase
                | CompareOptions.IgnoreNonSpace) >= 0;
        }

        private void TrangTruoc_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_trangHienTai <= 1)
                return;

            _trangHienTai--;
            CapNhatDanhSachNhanVien();
        }

        private void TrangSau_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_trangHienTai >= _tongSoTrang)
                return;

            _trangHienTai++;
            CapNhatDanhSachNhanVien();
        }

        private void NhanVien_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (dgvDayTro.SelectedItem is not DataRowView dayTro)
                return;

            if (dgvNhanVien.SelectedItem is not DataRowView nhanVien)
                return;

            string tenDay = LayGiaTri(dayTro, "TenDayTro");
            string hoTen = LayGiaTri(nhanVien, "HoTen");

            txtHuongDan.Text =
                "Đã chọn: " + hoTen
                + " → " + tenDay + ".";

            txtHuongDan.Foreground = new SolidColorBrush(
                Color.FromRgb(107, 114, 128));
        }

        private void ChonDayTro_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is RadioButton radio
                && radio.DataContext is DataRowView row)
            {
                dgvDayTro.SelectedItem = row;
            }
        }

        private void ChonNhanVien_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is RadioButton radio
                && radio.DataContext is DataRowView row)
            {
                dgvNhanVien.SelectedItem = row;
            }
        }

        private static string LayGiaTri(
            DataRowView row,
            string tenCot)
        {
            if (!row.DataView.Table.Columns.Contains(tenCot))
                return "";

            if (row[tenCot] == DBNull.Value)
                return "";

            return Convert.ToString(row[tenCot]) ?? "";
        }

        private void HienThiLoi(string noiDung)
        {
            txtDayEmpty.Text = noiDung;
            txtDayEmpty.Visibility = Visibility.Visible;

            txtNhanVienEmpty.Text = noiDung;
            txtNhanVienEmpty.Visibility = Visibility.Visible;

            txtHuongDan.Text = noiDung;
            txtHuongDan.Foreground = new SolidColorBrush(
                Color.FromRgb(220, 38, 38));
        }

        private async void LuuPhanCong_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (dgvDayTro.SelectedItem is not DataRowView dayTro)
            {
                MessageBox.Show(
                    "Vui lòng chọn dãy trọ cần phân công.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (dgvNhanVien.SelectedItem is not DataRowView nhanVien)
            {
                MessageBox.Show(
                    "Vui lòng chọn nhân viên cần phân công. "
                    + "Có thể chuyển bộ lọc sang “Chưa phân công” "
                    + "hoặc “Dãy khác (điều chuyển)”.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            string maDay = LayGiaTri(dayTro, "MaDayTro");
            string tenDay = LayGiaTri(dayTro, "TenDayTro");

            string maNhanVien =
                LayGiaTri(nhanVien, "MaNhanVien");

            string hoTen = LayGiaTri(nhanVien, "HoTen");
            string maDayCu = LayGiaTri(nhanVien, "MaDayTro");

            if (maDayCu == maDay)
            {
                MessageBox.Show(
                    "Nhân viên này đã phụ trách dãy trọ đang chọn.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            string xacNhan =
                "Bạn muốn phân công nhân viên "
                + hoTen
                + " phụ trách dãy trọ "
                + tenDay
                + "?";

            if (!string.IsNullOrWhiteSpace(maDayCu))
            {
                xacNhan +=
                    Environment.NewLine
                    + Environment.NewLine
                    + "Nhân viên đang phụ trách một dãy khác. "
                    + "Thao tác này sẽ điều chuyển nhân viên sang dãy mới.";
            }

            var ketQua = MessageBox.Show(
                xacNhan,
                "Xác nhận phân công",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (ketQua != MessageBoxResult.Yes)
                return;

            btnLuuPhanCong.IsEnabled = false;
            btnHuyPhanCong.IsEnabled = false;

            try
            {
                string? dayChuaCoQuanLy =
                    await PhanCongQuanLyService.LuuPhanCongAsync(
                        maDay,
                        maNhanVien);

                string thongBao =
                    "Lưu phân công thành công."
                    + Environment.NewLine
                    + "Nhân viên: " + hoTen
                    + Environment.NewLine
                    + "Dãy trọ: " + tenDay
                    + Environment.NewLine
                    + "Đã tạo thông báo nội bộ cho nhân viên.";

                if (!string.IsNullOrWhiteSpace(dayChuaCoQuanLy))
                {
                    thongBao +=
                        Environment.NewLine
                        + Environment.NewLine
                        + "Lưu ý: Dãy trọ "
                        + dayChuaCoQuanLy
                        + " hiện chưa có nhân viên quản lý đang làm việc.";
                }

                MessageBox.Show(
                    thongBao,
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await TaiDuLieuAsync();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Không thể phân công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (SqlException)
            {
                MessageBox.Show(
                    "Không thể lưu phân công vào SQL Server. "
                    + "Vui lòng kiểm tra kết nối và thử lại.",
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "Đã xảy ra lỗi khi lưu phân công.",
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                btnLuuPhanCong.IsEnabled = true;
                btnHuyPhanCong.IsEnabled = true;
            }
        }

        private async void HuyPhanCong_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (dgvDayTro.SelectedItem is not DataRowView dayTro)
            {
                MessageBox.Show(
                    "Vui lòng chọn dãy trọ cần hủy phân công.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (dgvNhanVien.SelectedItem is not DataRowView nhanVien)
            {
                MessageBox.Show(
                    "Vui lòng chọn nhân viên đang phụ trách dãy trọ.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            string maDay = LayGiaTri(dayTro, "MaDayTro");
            string tenDay = LayGiaTri(dayTro, "TenDayTro");

            string maNhanVien =
                LayGiaTri(nhanVien, "MaNhanVien");

            string hoTen = LayGiaTri(nhanVien, "HoTen");
            string maDayDaGan =
                LayGiaTri(nhanVien, "MaDayTro");

            if (maDayDaGan != maDay)
            {
                MessageBox.Show(
                    "Nhân viên này không phụ trách dãy trọ đang chọn. "
                    + "Hãy chuyển bộ lọc sang “Đang phụ trách dãy này”.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            var ketQua = MessageBox.Show(
                "Bạn có chắc muốn hủy quyền quản lý của "
                + hoTen
                + " tại dãy trọ "
                + tenDay
                + "?"
                + Environment.NewLine
                + Environment.NewLine
                + "Hồ sơ và tài khoản nhân viên vẫn được giữ nguyên.",
                "Xác nhận hủy phân công",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (ketQua != MessageBoxResult.Yes)
                return;

            btnLuuPhanCong.IsEnabled = false;
            btnHuyPhanCong.IsEnabled = false;

            try
            {
                bool khongConQuanLy =
                    await PhanCongQuanLyService.HuyPhanCongAsync(
                        maDay,
                        maNhanVien);

                string thongBao =
                    "Hủy quyền phân công thành công."
                    + Environment.NewLine
                    + "Nhân viên: " + hoTen
                    + Environment.NewLine
                    + "Dãy trọ: " + tenDay
                    + Environment.NewLine
                    + "Đã tạo thông báo nội bộ cho nhân viên.";

                if (khongConQuanLy)
                {
                    thongBao +=
                        Environment.NewLine
                        + Environment.NewLine
                        + "Lưu ý: Dãy trọ này chưa có nhân viên quản lý "
                        + "đang làm việc. Hãy phân công người phụ trách "
                        + "trước khi vận hành kỳ thu tiền.";
                }

                MessageBox.Show(
                    thongBao,
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await TaiDuLieuAsync();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Không thể hủy phân công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (SqlException)
            {
                MessageBox.Show(
                    "Không thể hủy phân công trong SQL Server. "
                    + "Vui lòng kiểm tra kết nối và thử lại.",
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "Đã xảy ra lỗi khi hủy quyền phân công.",
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                btnLuuPhanCong.IsEnabled = true;
                btnHuyPhanCong.IsEnabled = true;
            }
        }
    }
}
