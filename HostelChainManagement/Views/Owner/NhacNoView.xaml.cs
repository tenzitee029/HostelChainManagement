
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
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Owner
{
    public partial class NhacNoView : UserControl
    {
        private sealed record LuaChonPhong(
            string? MaPhong,
            string TenHienThi);

        private readonly CultureInfo _vanHoa =
            CultureInfo.GetCultureInfo("vi-VN");

        private DataTable? _danhSach;
        private bool _dangTai;
        private bool _dangGui;

        public event Action? QuayLaiRequested;

        public NhacNoView()
        {
            InitializeComponent();

            dpHanChot.SelectedDate = DateTime.Today.AddDays(7);

            btnGuiNhacNo.IsEnabled = false;

            btnGuiNhacNo.ToolTip =
                "Chọn hóa đơn quá hạn để gửi thông báo nội bộ.";

            btnGuiNhacNo.Click += GuiNhacNo_Click;

            Loaded += NhacNoView_Loaded;
        }

        private async void NhacNoView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= NhacNoView_Loaded;

            await TaiDanhSachAsync();
        }

        private async Task TaiDanhSachAsync()
        {
            if (_dangTai || _dangGui)
                return;

            _dangTai = true;

            btnTaiLai.IsEnabled = false;
            btnGuiNhacNo.IsEnabled = false;

            _danhSach = null;

            dgvNoQuaHan.ItemsSource = null;
            cmbPhong.ItemsSource = null;
            cmbPhong.IsEnabled = false;

            txtTongTien.Text = "—";
            txtNoiDung.Clear();

            txtEmptyState.Text =
                "Đang tải danh sách hóa đơn quá hạn...";

            txtEmptyState.Visibility = Visibility.Visible;

            txtThongBao.Text =
                "Đang kiểm tra các khoản thanh toán từ SQL Server.";

            txtThongBao.Foreground = TaoMau(107, 114, 128);

            try
            {
                DataTable table =
                    await NhacNoChuTroService
                        .LayDanhSachNoQuaHanAsync();

                _danhSach = table;

                var danhSachPhong = new List<LuaChonPhong>
                {
                    new LuaChonPhong(null, "Tất cả phòng")
                };

                var phongDaThem = new HashSet<string>();

                foreach (DataRow row in table.Rows)
                {
                    string maPhong =
                        Convert.ToString(row["MaPhong"]) ?? "";

                    if (!phongDaThem.Add(maPhong))
                        continue;

                    string tenDay =
                        Convert.ToString(row["TenDayTro"]) ?? "";

                    string soPhong =
                        Convert.ToString(row["SoPhong"]) ?? "";

                    danhSachPhong.Add(
                        new LuaChonPhong(
                            maPhong,
                            tenDay + " - Phòng " + soPhong));
                }

                cmbPhong.DisplayMemberPath = "TenHienThi";
                cmbPhong.SelectedValuePath = "MaPhong";
                cmbPhong.ItemsSource = danhSachPhong;
                cmbPhong.SelectedIndex = 0;
                cmbPhong.IsEnabled = true;

                dgvNoQuaHan.ItemsSource = table.DefaultView;

                if (table.Rows.Count == 0)
                {
                    txtEmptyState.Text =
                        "Hiện không có hóa đơn nào quá hạn "
                        + "và còn tiền phải thu.";

                    txtEmptyState.Visibility = Visibility.Visible;
                }
                else
                {
                    txtEmptyState.Visibility = Visibility.Collapsed;
                }

                txtThongBao.Text =
                    "Tìm thấy " + table.Rows.Count
                    + " hóa đơn quá hạn. "
                    + "Chọn các hóa đơn của cùng một khách thuê để gửi.";
            }
            catch (InvalidOperationException ex)
            {
                HienThiLoi(ex.Message);
            }
            catch (SqlException ex)
            {
                HienThiLoi(
                    "Không thể tải danh sách nợ quá hạn: "
                    + ex.Message);
            }
            catch (Exception ex)
            {
                HienThiLoi(
                    "Đã xảy ra lỗi khi tải danh sách: "
                    + ex.Message);
            }
            finally
            {
                _dangTai = false;
                btnTaiLai.IsEnabled = true;
            }
        }

        private void Phong_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_dangTai || _dangGui || _danhSach == null)
                return;

            string? maPhong =
                (cmbPhong.SelectedItem as LuaChonPhong)?.MaPhong;

            _danhSach.DefaultView.RowFilter =
                string.IsNullOrWhiteSpace(maPhong)
                    ? ""
                    : "MaPhong = '"
                      + maPhong.Replace("'", "''")
                      + "'";

            dgvNoQuaHan.SelectedItems.Clear();

            txtEmptyState.Text =
                "Không có hóa đơn quá hạn ở phòng đã chọn.";

            txtEmptyState.Visibility =
                _danhSach.DefaultView.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            CapNhatNoiDung();
        }

        private void NoQuaHan_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_dangTai || _dangGui)
                return;

            CapNhatNoiDung();
        }

        private void HanChot_SelectedDateChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _dangTai || _dangGui)
                return;

            CapNhatNoiDung();
        }

        private void CapNhatNoiDung()
        {
            btnGuiNhacNo.IsEnabled = false;

            var cacHoaDon = dgvNoQuaHan.SelectedItems
                .OfType<DataRowView>()
                .ToList();

            if (cacHoaDon.Count == 0)
            {
                txtTongTien.Text = "—";
                txtNoiDung.Clear();

                txtThongBao.Text =
                    "Chọn hóa đơn quá hạn để lập nội dung nhắc nợ.";

                txtThongBao.Foreground = TaoMau(107, 114, 128);

                return;
            }

            decimal tongNo = cacHoaDon.Sum(
                row => Convert.ToDecimal(row["ConNo"]));

            txtTongTien.Text = DinhDangTien(tongNo);

            var nguoiNhan = cacHoaDon
                .Select(row =>
                    Convert.ToString(row["MaNguoiNhan"]) ?? "")
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (nguoiNhan.Count != 1
                || string.IsNullOrWhiteSpace(nguoiNhan[0]))
            {
                txtNoiDung.Clear();

                txtThongBao.Text =
                    "Các hóa đơn đã chọn thuộc nhiều khách thuê. "
                    + "Hãy gửi riêng từng khách để bảo mật thông tin công nợ.";

                txtThongBao.Foreground = TaoMau(220, 38, 38);

                return;
            }

            if (!dpHanChot.SelectedDate.HasValue
                || dpHanChot.SelectedDate.Value.Date
                    < DateTime.Today)
            {
                txtNoiDung.Clear();

                txtThongBao.Text =
                    "Vui lòng chọn hạn chót đề nghị thanh toán "
                    + "từ hôm nay trở đi.";

                txtThongBao.Foreground = TaoMau(220, 38, 38);

                return;
            }

            string hanDeNghi =
                dpHanChot.SelectedDate.Value.ToString(
                    "dd/MM/yyyy");

            string tenKhach =
                Convert.ToString(
                    cacHoaDon[0]["HoTenKhach"]) ?? "";

            var noiDung = new List<string>
            {
                "Kính gửi " + tenKhach + ",",
                "",
                "Theo hệ thống quản lý nhà trọ, "
                + "các hóa đơn sau đã quá hạn thanh toán:",
                ""
            };

            foreach (DataRowView row in cacHoaDon)
            {
                string maHoaDon =
                    Convert.ToString(row["MaHoaDon"]) ?? "";

                string tenDay =
                    Convert.ToString(row["TenDayTro"]) ?? "";

                string soPhong =
                    Convert.ToString(row["SoPhong"]) ?? "";

                decimal conNo =
                    Convert.ToDecimal(row["ConNo"]);

                noiDung.Add(
                    "- Hóa đơn " + maHoaDon
                    + ", " + tenDay
                    + ", phòng " + soPhong
                    + ": " + DinhDangTien(conNo));
            }

            noiDung.Add("");
            noiDung.Add(
                "Tổng số tiền còn nợ: "
                + DinhDangTien(tongNo));

            noiDung.Add(
                "Kính đề nghị thanh toán trước ngày "
                + hanDeNghi + ".");

            noiDung.Add("");
            noiDung.Add("Trân trọng.");

            txtNoiDung.Text =
                string.Join(Environment.NewLine, noiDung);

            btnGuiNhacNo.IsEnabled = true;

            txtThongBao.Text =
                "Đã chọn " + cacHoaDon.Count
                + " hóa đơn của " + tenKhach
                + ". Vui lòng kiểm tra nội dung trước khi gửi.";

            txtThongBao.Foreground = TaoMau(107, 114, 128);
        }

        private async void GuiNhacNo_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_dangTai || _dangGui)
                return;

            var cacHoaDon = dgvNoQuaHan.SelectedItems
                .OfType<DataRowView>()
                .ToList();

            if (cacHoaDon.Count == 0)
            {
                MessageBox.Show(
                    "Vui lòng chọn hóa đơn quá hạn.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            var nguoiNhan = cacHoaDon
                .Select(row =>
                    Convert.ToString(row["MaNguoiNhan"]) ?? "")
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (nguoiNhan.Count != 1
                || string.IsNullOrWhiteSpace(nguoiNhan[0]))
            {
                MessageBox.Show(
                    "Hãy chọn các hóa đơn của cùng một khách thuê.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (!dpHanChot.SelectedDate.HasValue
                || dpHanChot.SelectedDate.Value.Date
                    < DateTime.Today)
            {
                MessageBox.Show(
                    "Vui lòng chọn hạn chót thanh toán hợp lệ.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            string noiDung = txtNoiDung.Text.Trim();

            if (noiDung.Length < 10 || noiDung.Length > 4000)
            {
                MessageBox.Show(
                    "Nội dung nhắc nợ phải có từ 10 đến 4000 ký tự.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            string tenKhach =
                Convert.ToString(
                    cacHoaDon[0]["HoTenKhach"]) ?? "";

            decimal tongNo = cacHoaDon.Sum(
                row => Convert.ToDecimal(row["ConNo"]));

            string[] maHoaDon = cacHoaDon
                .Select(row =>
                    Convert.ToString(row["MaHoaDon"]) ?? "")
                .ToArray();

            DateTime hanChot =
                dpHanChot.SelectedDate.Value.Date;

            var xacNhan = MessageBox.Show(
                "Xác nhận gửi thông báo nhắc nợ nội bộ?"
                + Environment.NewLine
                + Environment.NewLine
                + "Người nhận: " + tenKhach
                + Environment.NewLine
                + "Số hóa đơn: " + maHoaDon.Length
                + Environment.NewLine
                + "Số tiền còn nợ: " + DinhDangTien(tongNo)
                + Environment.NewLine
                + "Hạn đề nghị: "
                + hanChot.ToString("dd/MM/yyyy")
                + Environment.NewLine
                + Environment.NewLine
                + "Thông báo sẽ được ghi vào hệ thống. "
                + "Không gửi SMS hoặc email.",
                "Xác nhận gửi nhắc nợ",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (xacNhan != MessageBoxResult.Yes)
                return;

            _dangGui = true;

            btnGuiNhacNo.IsEnabled = false;
            btnTaiLai.IsEnabled = false;
            cmbPhong.IsEnabled = false;
            dgvNoQuaHan.IsEnabled = false;

            bool guiThanhCong = false;

            try
            {
                var ketQua =
                    await NhacNoChuTroService.GuiNhacNoAsync(
                        maHoaDon,
                        tongNo,
                        hanChot,
                        noiDung);

                guiThanhCong = true;

                MessageBox.Show(
                    "Gửi thông báo nhắc nợ thành công."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Mã thông báo: "
                    + ketQua.MaThongBao
                    + Environment.NewLine
                    + "Người nhận: "
                    + ketQua.TenKhach
                    + Environment.NewLine
                    + "Số hóa đơn: "
                    + ketQua.SoHoaDon
                    + Environment.NewLine
                    + "Tổng còn nợ: "
                    + DinhDangTien(ketQua.TongNo)
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Thông báo đã được lưu vào hệ thống.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Không thể gửi nhắc nợ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (SqlException ex)
            {
                MessageBox.Show(
                    "Không thể lưu thông báo vào SQL Server: "
                    + ex.Message,
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Đã xảy ra lỗi khi gửi nhắc nợ: "
                    + ex.Message,
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _dangGui = false;

                btnTaiLai.IsEnabled = true;
                cmbPhong.IsEnabled = _danhSach != null;
                dgvNoQuaHan.IsEnabled = true;

                if (!guiThanhCong)
                {
                    CapNhatNoiDung();
                }
            }

            if (guiThanhCong)
            {
                await TaiDanhSachAsync();
            }
        }

        private string DinhDangTien(decimal soTien)
        {
            return soTien.ToString(
                "N0",
                _vanHoa) + " đ";
        }

        private void HienThiLoi(string noiDung)
        {
            dgvNoQuaHan.ItemsSource = null;

            txtEmptyState.Text = noiDung;
            txtEmptyState.Visibility = Visibility.Visible;

            btnGuiNhacNo.IsEnabled = false;

            txtThongBao.Text = noiDung;
            txtThongBao.Foreground = TaoMau(220, 38, 38);
        }

        private static SolidColorBrush TaoMau(
            byte r,
            byte g,
            byte b)
        {
            return new SolidColorBrush(
                Color.FromRgb(r, g, b));
        }

        private async void TaiLai_Click(
            object sender,
            RoutedEventArgs e)
        {
            await TaiDanhSachAsync();
        }

        private void QuayLai_Click(
            object sender,
            RoutedEventArgs e)
        {
            QuayLaiRequested?.Invoke();
        }
    }
}
