using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Management;
using DuLieuPhi = QuanLyChuoiNhaTro.Services.Management.TaoHoaDonService.DuLieuPhi;
using KhoanThu = QuanLyChuoiNhaTro.Services.Management.TaoHoaDonService.KhoanThu;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class TaoHoaDonView : UserControl
    {
        private const decimal MaxMoney =
            999999999999999999m;

        private bool _khoiTao;
        private bool _dangXuLy;
        private bool _doiPhong;
        private bool _daLuu;

        private int _thang;
        private int _nam;

        private DataTable? _phong;

        private readonly Dictionary<string, DuLieuPhi>
            _nhap = new();

        private readonly Dictionary<string, List<VatTuChoThu>>
            _vatTuTheoHopDong = new();

        public event Action? QuayLai;
        public event Action? DaLuu;

        public TaoHoaDonView()
        {
            InitializeComponent();

            _khoiTao = true;

            for (int i = 1; i <= 12; i++)
            {
                cboThang.Items.Add(i);
            }

            cboThang.SelectedItem = DateTime.Today.Month;
            txtNam.Text = DateTime.Today.Year.ToString();

            _khoiTao = false;

            HienThi();
        }

        private static string TaoMa(string tienTo)
        {
            return TaoHoaDonService.TaoMa(tienTo);
        }

        private static string Tien(decimal value)
        {
            return value.ToString(
                "N0",
                CultureInfo.GetCultureInfo("vi-VN")) + " VNĐ";
        }

        private string Khoa(DataRow row)
        {
            return row["MaHopDong"]
                + "/" + _thang
                + "/" + _nam;
        }

        private bool DocKy(out int thang, out int nam)
        {
            thang = cboThang.SelectedItem is int value
                ? value
                : 0;

            return int.TryParse(txtNam.Text, out nam)
                && thang >= 1
                && thang <= 12
                && nam >= 2000
                && nam <= 9998;
        }

        private static bool DocTien(
            string input,
            out decimal value)
        {
            return decimal.TryParse(
                    input.Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out value)
                && value >= 0
                && value <= MaxMoney;
        }

        private static bool DungTaiKhoan(string userId)
        {
            return TaoHoaDonService.DungTaiKhoan(userId);
        }

        private async void View_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await TaiPhongAsync();
        }

        private async void Thang_Changed(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!_khoiTao && !_dangXuLy && IsLoaded)
            {
                await TaiPhongAsync();
            }
        }

        private async void Nam_LostFocus(
            object sender,
            RoutedEventArgs e)
        {
            if (!_dangXuLy && IsLoaded)
            {
                await TaiPhongAsync();
            }
        }

        private async Task TaiPhongAsync()
        {
            if (_dangXuLy || _daLuu)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text =
                    "Bạn không có quyền tạo hóa đơn.";
                return;
            }

            if (!DocKy(out int thang, out int nam))
            {
                _phong = null;
                _vatTuTheoHopDong.Clear();

                cboPhong.ItemsSource = null;

                txtThongBao.Text =
                    "Chọn tháng và nhập năm từ 2000 đến 9998.";

                HienThi();
                return;
            }

            string userId = user.MaNguoiDung;

            _dangXuLy = true;
            _phong = null;

            cboPhong.ItemsSource = null;
            _vatTuTheoHopDong.Clear();

            HienThi();

            txtThongBao.Text =
                "Đang tải phòng và linh kiện chưa tính hóa đơn...";

            try
            {
                var table = await TaoHoaDonService.LayPhongAsync(
                    thang,
                    nam,
                    userId);

                // Tải vào bộ nhớ tạm, chỉ đưa lên giao diện
                // khi toàn bộ dữ liệu đã lấy thành công.
                var materialsByContract =
                    new Dictionary<string, List<VatTuChoThu>>();

                foreach (DataRow row in table.Rows)
                {
                    if (!IsLoaded || !DungTaiKhoan(userId))
                        return;

                    string maHopDong =
                        Convert.ToString(row["MaHopDong"]) ?? "";

                    materialsByContract[maHopDong] =
                        await TaoHoaDonService.LayVatTuAsync(
                            maHopDong,
                            thang,
                            nam);
                }

                if (!IsLoaded || !DungTaiKhoan(userId))
                    return;

                foreach (var item in materialsByContract)
                {
                    _vatTuTheoHopDong[item.Key] = item.Value;
                }

                _thang = thang;
                _nam = nam;
                _phong = table;

                cboPhong.ItemsSource = table.DefaultView;

                txtThongBao.Text = table.Rows.Count == 0
                    ? "Không có hợp đồng phù hợp chưa lập hóa đơn trong kỳ."
                    : "Chọn phòng và nhập chi phí. "
                      + "Linh kiện chưa thu được tự cộng vào tổng tiền.";
            }
            catch (Exception ex)
            {
                _phong = null;
                _vatTuTheoHopDong.Clear();

                if (IsLoaded && DungTaiKhoan(userId))
                {
                    txtThongBao.Text =
                        "Không tải được phòng: " + ex.Message;
                }
            }
            finally
            {
                _dangXuLy = false;
                HienThi();
            }
        }

        private void Phong_Changed(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (txtKhach == null)
                return;

            _doiPhong = true;

            if (cboPhong.SelectedItem is DataRowView phong
                && _nhap.TryGetValue(
                    Khoa(phong.Row),
                    out var phi))
            {
                txtPhuPhi.Text = phi.PhuPhi.ToString(
                    "0",
                    CultureInfo.InvariantCulture);
            }
            else
            {
                txtPhuPhi.Text = "0";
            }

            _doiPhong = false;

            HienThi();
        }

        private void PhuPhi_Changed(
            object sender,
            TextChangedEventArgs e)
        {
            if (_doiPhong || txtTongTien == null)
                return;

            if (cboPhong.SelectedItem is DataRowView phong
                && _nhap.TryGetValue(
                    Khoa(phong.Row),
                    out var phi)
                && DocTien(txtPhuPhi.Text, out decimal phuPhi))
            {
                phi.PhuPhi = phuPhi;
            }

            HienThi();
        }

        private decimal TongTienVatTu(DataRow phong)
        {
            string maHopDong =
                Convert.ToString(phong["MaHopDong"]) ?? "";

            return _vatTuTheoHopDong.TryGetValue(
                maHopDong,
                out var materials)
                ? materials.Sum(x => x.ThanhTien)
                : 0;
        }

        private void HienThi()
        {
            if (btnLuu == null)
                return;

            bool coQuyen =
                UserSession.CurrentUser?.VaiTro
                    == "Nhân viên quản lý";

            bool kyDung =
                DocKy(out int thang, out int nam)
                && thang == _thang
                && nam == _nam;

            pnlThongTin.IsEnabled =
                coQuyen && !_dangXuLy && !_daLuu;

            btnQuayLai.IsEnabled = !_dangXuLy;

            bool daChon =
                cboPhong.SelectedItem is DataRowView;

            bool coPhi = false;
            bool phiHopLe = false;

            txtMaHoaDon.Text = "—";
            txtKhach.Text = "—";
            txtChiSo.Text = "Chọn phòng để nhập dữ liệu";

            txtTienPhong.Text = "— VNĐ";
            txtTienDien.Text = "— VNĐ";
            txtTienNuoc.Text = "— VNĐ";
            txtPhiRac.Text = "— VNĐ";
            txtInternet.Text = "— VNĐ";
            txtPhiDichVu.Text = "— VNĐ";
            txtTongTien.Text = "— VNĐ";

            txtTongTien.ToolTip = null;
            txtChiSo.ToolTip = null;

            if (cboPhong.SelectedItem is DataRowView phong)
            {
                txtKhach.Text =
                    Convert.ToString(phong["HoTen"]) ?? "";

                txtTienPhong.Text = Tien(
                    Convert.ToDecimal(phong["GiaThue"]));

                txtChiSo.Text =
                    "Chưa nhập — nhấn vào đây để nhập";

                decimal tienVatTu = TongTienVatTu(phong.Row);

                txtTongTien.ToolTip =
                    "Tổng tiền đã bao gồm linh kiện: "
                    + Tien(tienVatTu);

                txtChiSo.ToolTip =
                    "Linh kiện chưa thu: " + Tien(tienVatTu)
                    + ". Không nhập lại khoản này vào phụ phí.";

                if (_nhap.TryGetValue(
                    Khoa(phong.Row),
                    out var phi))
                {
                    coPhi = true;

                    txtMaHoaDon.Text = phi.MaHoaDon;

                    txtChiSo.Text =
                        "Đã nhập — nhấn để kiểm tra/sửa";

                    var ds = TaoKhoanThu(phong.Row, phi);

                    txtTienDien.Text = Tien(ds[1].ThanhTien);
                    txtTienNuoc.Text = Tien(ds[2].ThanhTien);
                    txtPhiRac.Text = Tien(phi.Rac);
                    txtInternet.Text = Tien(phi.Internet);
                    txtPhiDichVu.Text = Tien(phi.DichVu);

                    decimal tong = ds.Sum(x => x.ThanhTien);

                    phiHopLe =
                        DocTien(txtPhuPhi.Text, out _)
                        && tong > 0
                        && tong <= MaxMoney;

                    txtTongTien.Text = phiHopLe
                        ? Tien(tong)
                        : "Kiểm tra phụ phí/tổng tiền";
                }
            }

            btnLuu.IsEnabled =
                coQuyen
                && !_dangXuLy
                && !_daLuu
                && kyDung
                && coPhi
                && phiHopLe;

            btnToanDay.IsEnabled =
                coQuyen
                && !_dangXuLy
                && !_daLuu
                && kyDung
                && daChon
                && DocTien(txtPhuPhi.Text, out _);
        }

        private void NhapDuLieu_Click(
            object sender,
            MouseButtonEventArgs e)
        {
            if (_dangXuLy
                || _daLuu
                || cboPhong.SelectedItem is not DataRowView phong)
            {
                return;
            }

            if (!DocKy(out int thang, out int nam)
                || thang != _thang
                || nam != _nam)
            {
                txtThongBao.Text =
                    "Hãy chọn lại kỳ và tải phòng.";
                return;
            }

            _nhap.TryGetValue(
                Khoa(phong.Row),
                out var cu);

            string[] labels =
            {
                "Chỉ số điện cũ",
                "Chỉ số điện mới",
                "Đơn giá điện (VNĐ)",
                "Chỉ số nước cũ",
                "Chỉ số nước mới",
                "Đơn giá nước (VNĐ)",
                "Phí rác (VNĐ)",
                "Internet (VNĐ)",
                "Phí dịch vụ (VNĐ)"
            };

            decimal[] giaTri = cu == null
                ? new decimal[9]
                : new[]
                {
                    cu.DienCu,
                    cu.DienMoi,
                    cu.GiaDien,
                    cu.NuocCu,
                    cu.NuocMoi,
                    cu.GiaNuoc,
                    cu.Rac,
                    cu.Internet,
                    cu.DichVu
                };

            var window = new Window
            {
                Title =
                    $"Nhập chi phí — {phong["TenHienThi"]}"
                    + $" — {_thang}/{_nam}",

                Width = 560,
                Height = 720,

                Owner = Window.GetWindow(this),

                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner
            };

            var panel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            var inputs = new TextBox[9];

            for (int i = 0; i < labels.Length; i++)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = labels[i]
                });

                inputs[i] = new TextBox
                {
                    Text = giaTri[i].ToString(
                        "0.##",
                        CultureInfo.InvariantCulture),

                    Padding = new Thickness(8),
                    Margin = new Thickness(0, 5, 0, 12)
                };

                panel.Children.Add(inputs[i]);
            }

            // Hiển thị danh sách linh kiện sẽ được tính.
            string maHopDong =
                Convert.ToString(phong["MaHopDong"]) ?? "";

            if (_vatTuTheoHopDong.TryGetValue(
                    maHopDong,
                    out var materials)
                && materials.Count > 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "Linh kiện tự động tính vào hóa đơn",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 8, 0, 8)
                });

                foreach (var material in materials)
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text =
                            $"{material.TenVatTu}: "
                            + $"{material.SoLuong:N2} × "
                            + $"{Tien(material.DonGia)}"
                            + $" = {Tien(material.ThanhTien)}",

                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 6)
                    });
                }

                panel.Children.Add(new TextBlock
                {
                    Text =
                        "Tổng linh kiện: "
                        + Tien(materials.Sum(x => x.ThanhTien)),

                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 4, 0, 14)
                });
            }

            var loi = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };

            panel.Children.Add(loi);

            var btn = new Button
            {
                Content = "Áp dụng dữ liệu",
                Padding = new Thickness(14)
            };

            btn.Click += (_, _) =>
            {
                var values = new decimal[9];

                for (int i = 0; i < values.Length; i++)
                {
                    bool chiSo =
                        i == 0 || i == 1 || i == 3 || i == 4;

                    bool hopLe = chiSo
                        ? decimal.TryParse(
                            inputs[i].Text.Trim().Replace(',', '.'),
                            NumberStyles.AllowDecimalPoint,
                            CultureInfo.InvariantCulture,
                            out values[i])
                          && values[i] >= 0
                          && values[i] <= 99999999.99m
                          && decimal.Round(values[i], 2) == values[i]
                        : DocTien(
                            inputs[i].Text,
                            out values[i]);

                    if (!hopLe)
                    {
                        loi.Text =
                            "Giá trị không hợp lệ: " + labels[i];
                        return;
                    }
                }

                if (values[1] < values[0]
                    || values[4] < values[3])
                {
                    loi.Text =
                        "Chỉ số mới phải lớn hơn hoặc bằng chỉ số cũ.";
                    return;
                }

                if ((values[1] > values[0] && values[2] == 0)
                    || (values[4] > values[3] && values[5] == 0))
                {
                    loi.Text =
                        "Có tiêu thụ điện/nước thì đơn giá phải lớn hơn 0.";
                    return;
                }

                var phi = new DuLieuPhi
                {
                    DienCu = values[0],
                    DienMoi = values[1],
                    GiaDien = values[2],

                    NuocCu = values[3],
                    NuocMoi = values[4],
                    GiaNuoc = values[5],

                    Rac = values[6],
                    Internet = values[7],
                    DichVu = values[8],

                    PhuPhi = cu?.PhuPhi ?? 0,
                    MaHoaDon = cu?.MaHoaDon ?? TaoMa("HDON")
                };

                decimal tong = TaoKhoanThu(
                    phong.Row,
                    phi).Sum(x => x.ThanhTien);

                if (tong <= 0 || tong > MaxMoney)
                {
                    loi.Text =
                        "Tổng tiền phải lớn hơn 0 "
                        + "và không vượt giới hạn database.";
                    return;
                }

                _nhap[Khoa(phong.Row)] = phi;

                window.DialogResult = true;
            };

            panel.Children.Add(btn);

            window.Content = new ScrollViewer
            {
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto,

                Content = panel
            };

            if (window.ShowDialog() == true)
            {
                txtThongBao.Text =
                    "Đã nhập dữ liệu nháp, chưa phát hành hóa đơn.";

                HienThi();
            }
        }

        private List<KhoanThu> TaoKhoanThu(
            DataRow phong,
            DuLieuPhi p)
        {
            return TaoHoaDonService.TaoKhoanThu(
                phong,
                p,
                _vatTuTheoHopDong);
        }

        private async void Luu_Click(
            object sender,
            RoutedEventArgs e)
        {
            await PhatHanhAsync(false);
        }

        private async void ToanDay_Click(
            object sender,
            RoutedEventArgs e)
        {
            await PhatHanhAsync(true);
        }

        private async Task PhatHanhAsync(bool toanDay)
        {
            if (_dangXuLy
                || _daLuu
                || _phong == null
                || cboPhong.SelectedItem is not DataRowView chon)
            {
                return;
            }

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
                return;

            if (!DocKy(out int thang, out int nam)
                || thang != _thang
                || nam != _nam
                || !DocTien(txtPhuPhi.Text, out decimal phuPhi))
            {
                txtThongBao.Text =
                    "Kiểm tra kỳ hóa đơn và phụ phí.";
                return;
            }

            // Đồng bộ phụ phí của phòng đang chọn.
            if (_nhap.TryGetValue(
                Khoa(chon.Row),
                out var selectedFee))
            {
                selectedFee.PhuPhi = phuPhi;
            }

            List<DataRow> danhSach = toanDay
                ? _phong.Rows.Cast<DataRow>()
                    .Where(row =>
                        Convert.ToString(row["MaDayTro"])
                        == Convert.ToString(chon["MaDayTro"]))
                    .ToList()
                : new List<DataRow> { chon.Row };

            foreach (DataRow phong in danhSach)
            {
                if (!_nhap.TryGetValue(
                    Khoa(phong),
                    out var phi))
                {
                    txtThongBao.Text =
                        "Chưa nhập dữ liệu cho "
                        + phong["TenHienThi"]
                        + ". Chọn phòng đó và nhập trước.";
                    return;
                }

                decimal tongPhong =
                    TaoKhoanThu(phong, phi)
                        .Sum(x => x.ThanhTien);

                if (tongPhong <= 0 || tongPhong > MaxMoney)
                {
                    txtThongBao.Text =
                        "Tổng tiền không hợp lệ: "
                        + phong["TenHienThi"];
                    return;
                }
            }

            decimal tongTatCa = danhSach.Sum(
                row => TaoKhoanThu(
                    row,
                    _nhap[Khoa(row)])
                    .Sum(x => x.ThanhTien));

            decimal tongVatTu =
                danhSach.Sum(TongTienVatTu);

            if (MessageBox.Show(
                Window.GetWindow(this),
                $"Phát hành {danhSach.Count} hóa đơn "
                + $"kỳ {_thang}/{_nam}?\n"
                + $"Tổng tiền: {Tien(tongTatCa)}\n"
                + $"Trong đó linh kiện: {Tien(tongVatTu)}\n\n"
                + "Tiền phòng tính đủ tháng. "
                + "Hạn thanh toán theo hợp đồng.",
                "Xác nhận phát hành",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question)
                != MessageBoxResult.Yes)
            {
                return;
            }

            _dangXuLy = true;

            HienThi();

            txtThongBao.Text =
                "Đang phát hành hóa đơn...";

            bool thanhCong = false;

            try
            {
                await TaoHoaDonService.PhatHanhAsync(
                    danhSach,
                    phong => _nhap[Khoa(phong)],
                    _vatTuTheoHopDong,
                    user.MaNguoiDung,
                    _thang,
                    _nam);

                thanhCong = true;
                _daLuu = true;
            }
            catch (SqlException ex)
                when (ex.Number is 2601 or 2627)
            {
                txtThongBao.Text =
                    "Hóa đơn hoặc mã đã tồn tại. "
                    + "Toàn bộ lượt phát hành đã hủy. "
                    + "Hãy mở lại form để tải dữ liệu mới.";
            }
            catch (SqlException ex)
                when (ex.Number == 1205)
            {
                txtThongBao.Text =
                    "Dữ liệu đang được cập nhật. "
                    + "Lượt phát hành đã hủy; vui lòng thử lại.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không phát hành được. "
                    + "Toàn bộ lượt phát hành đã hủy:\n"
                    + ex.Message;
            }
            finally
            {
                _dangXuLy = false;
                HienThi();
            }

            if (thanhCong
                && IsLoaded
                && DungTaiKhoan(user.MaNguoiDung))
            {
                MessageBox.Show(
                    Window.GetWindow(this),
                    $"Đã phát hành {danhSach.Count} hóa đơn.",
                    "Thành công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DaLuu?.Invoke();
            }
        }

        private void QuayLai_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_dangXuLy)
                QuayLai?.Invoke();
        }
    }
}