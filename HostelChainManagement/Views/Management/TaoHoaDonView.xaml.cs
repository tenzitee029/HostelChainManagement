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
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class TaoHoaDonView : UserControl
    {
        private bool _khoiTao;
        private bool _dangXuLy;
        private bool _doiPhong;
        private bool _daLuu;
        private int _thang;
        private int _nam;
        private DataTable? _phong;

        private readonly Dictionary<string, DuLieuPhi> _nhap = new();

        public event Action? QuayLai;
        public event Action? DaLuu;

        private sealed class DuLieuPhi
        {
            public decimal DienCu, DienMoi, GiaDien;
            public decimal NuocCu, NuocMoi, GiaNuoc;
            public decimal Rac, Internet, DichVu, PhuPhi;
            public string MaHoaDon = TaoMa("HDON");
        }

        private sealed class KhoanThu
        {
            public string Loai = "";
            public string GhiChu = "";
            public decimal? Cu, Moi;
            public decimal SoLuong, DonGia;

            public decimal ThanhTien => decimal.Round(
                SoLuong * DonGia, 0, MidpointRounding.AwayFromZero);
        }

        public TaoHoaDonView()
        {
            InitializeComponent();

            _khoiTao = true;
            for (int i = 1; i <= 12; i++)
                cboThang.Items.Add(i);

            cboThang.SelectedItem = DateTime.Today.Month;
            txtNam.Text = DateTime.Today.Year.ToString();
            _khoiTao = false;

            HienThi();
        }

        private static string TaoMa(string tienTo)
        {
            return tienTo + Guid.NewGuid().ToString("N")
                .Substring(0, 20 - tienTo.Length).ToUpperInvariant();
        }

        private static string Tien(decimal tien)
        {
            return tien.ToString(
                "N0", CultureInfo.GetCultureInfo("vi-VN")) + " VNĐ";
        }

        private string Khoa(DataRow dong)
        {
            return dong["MaHopDong"] + "/" + _thang + "/" + _nam;
        }

        private bool DocKy(out int thang, out int nam)
        {
            thang = cboThang.SelectedItem is int t ? t : 0;
            return int.TryParse(txtNam.Text, out nam)
                && thang >= 1 && thang <= 12
                && nam >= 2000 && nam <= 9998;
        }

        private async void View_Loaded(object sender, RoutedEventArgs e)
        {
            await TaiPhongAsync();
        }

        private async void Thang_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            if (!_khoiTao && !_dangXuLy && IsLoaded)
                await TaiPhongAsync();
        }

        private async void Nam_LostFocus(
            object sender, RoutedEventArgs e)
        {
            if (!_dangXuLy && IsLoaded)
                await TaiPhongAsync();
        }

        private async Task TaiPhongAsync()
        {
            if (_dangXuLy || _daLuu)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền tạo hóa đơn.";
                return;
            }

            if (!DocKy(out int thang, out int nam))
            {
                _phong = null;
                cboPhong.ItemsSource = null;
                txtThongBao.Text = "Chọn tháng và nhập năm từ 2000 đến 9998.";
                HienThi();
                return;
            }

            _dangXuLy = true;
            _phong = null;
            cboPhong.ItemsSource = null;
            HienThi();
            txtThongBao.Text = "Đang tải phòng...";

            try
            {
                using var connection = Database.CreateConnection();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT h.MaHopDong, h.MaPhong, h.GiaThue,
                           h.KyThanhToan, p.MaDayTro,
                           n.HoTen,
                           d.TenDayTro + N' - Phòng ' + p.SoPhong
                               AS TenHienThi
                    FROM dbo.HopDongThue h
                    JOIN dbo.PhongTro p ON p.MaPhong = h.MaPhong
                    JOIN dbo.DayTro d ON d.MaDayTro = p.MaDayTro
                    JOIN dbo.KhachThue k
                      ON k.MaKhachThue = h.MaKhachThue
                    JOIN dbo.NguoiDung n
                      ON n.MaNguoiDung = k.MaNguoiDung
                    WHERE h.TrangThai IN (N'Hiệu lực', N'Hết hạn')
                      AND h.NgayBatDau <=
                          EOMONTH(DATEFROMPARTS(@Nam,@Thang,1))
                      AND h.NgayKetThuc >=
                          DATEFROMPARTS(@Nam,@Thang,1)
                      AND NOT EXISTS (
                          SELECT 1 FROM dbo.HoaDon hd
                          WHERE hd.MaHopDong = h.MaHopDong
                            AND hd.Thang = @Thang AND hd.Nam = @Nam
                      )
                      AND EXISTS (
                          SELECT 1 FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @QuanLy
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      )
                    ORDER BY d.TenDayTro, p.SoPhong, h.MaHopDong;";

                command.Parameters.Add(
                    "@Thang", SqlDbType.Int).Value = thang;
                command.Parameters.Add(
                    "@Nam", SqlDbType.Int).Value = nam;
                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;

                await connection.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();

                var table = new DataTable();
                table.Load(reader);

                _thang = thang;
                _nam = nam;
                _phong = table;
                cboPhong.ItemsSource = table.DefaultView;

                txtThongBao.Text = table.Rows.Count == 0
                    ? "Không có hợp đồng phù hợp chưa được lập hóa đơn trong kỳ."
                    : "Chọn phòng, sau đó nhấn ô trạng thái để nhập chi phí.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text = "Không tải được phòng: " + ex.Message;
            }
            finally
            {
                _dangXuLy = false;
                HienThi();
            }
        }

        private void Phong_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            if (txtKhach == null)
                return;

            _doiPhong = true;

            if (cboPhong.SelectedItem is DataRowView phong
                && _nhap.TryGetValue(Khoa(phong.Row), out var phi))
            {
                txtPhuPhi.Text = phi.PhuPhi.ToString(
                    "0", CultureInfo.InvariantCulture);
            }
            else
            {
                txtPhuPhi.Text = "0";
            }

            _doiPhong = false;
            HienThi();
        }

        private void PhuPhi_Changed(
            object sender, TextChangedEventArgs e)
        {
            if (_doiPhong || txtTongTien == null)
                return;

            if (cboPhong.SelectedItem is DataRowView phong
                && _nhap.TryGetValue(Khoa(phong.Row), out var phi)
                && DocTien(txtPhuPhi.Text, out decimal phuPhi))
            {
                phi.PhuPhi = phuPhi;
            }

            HienThi();
        }

        private static bool DocTien(string chuoi, out decimal tien)
        {
            return decimal.TryParse(
                chuoi.Trim(), NumberStyles.None,
                CultureInfo.InvariantCulture, out tien)
                && tien >= 0 && tien <= 999999999999999999m;
        }

        private void HienThi()
        {
            if (btnLuu == null)
                return;

            bool coQuyen =
                UserSession.CurrentUser?.VaiTro == "Nhân viên quản lý";

            bool kyDung = DocKy(out int thang, out int nam)
                && thang == _thang && nam == _nam;

            pnlThongTin.IsEnabled = coQuyen && !_dangXuLy && !_daLuu;
            btnQuayLai.IsEnabled = !_dangXuLy;

            bool daChon = cboPhong.SelectedItem is DataRowView;
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

            if (cboPhong.SelectedItem is DataRowView phong)
            {
                txtKhach.Text = Convert.ToString(phong["HoTen"]);
                txtTienPhong.Text = Tien(
                    Convert.ToDecimal(phong["GiaThue"]));
                txtChiSo.Text = "Chưa nhập — nhấn vào đây để nhập";

                if (_nhap.TryGetValue(Khoa(phong.Row), out var phi))
                {
                    coPhi = true;
                    txtMaHoaDon.Text = phi.MaHoaDon;
                    txtChiSo.Text = "Đã nhập — nhấn để kiểm tra/sửa";

                    var ds = TaoKhoanThu(phong.Row, phi);

                    txtTienDien.Text = Tien(ds[1].ThanhTien);
                    txtTienNuoc.Text = Tien(ds[2].ThanhTien);
                    txtPhiRac.Text = Tien(phi.Rac);
                    txtInternet.Text = Tien(phi.Internet);
                    txtPhiDichVu.Text = Tien(phi.DichVu);

                    decimal tong = ds.Sum(x => x.ThanhTien);

                    phiHopLe = DocTien(txtPhuPhi.Text, out _)
                        && tong > 0 && tong <= 999999999999999999m;

                    txtTongTien.Text = phiHopLe
                        ? Tien(tong) : "Kiểm tra phụ phí/tổng tiền";
                }
            }

            btnLuu.IsEnabled =
                coQuyen && !_dangXuLy && !_daLuu
                && kyDung && coPhi && phiHopLe;

            btnToanDay.IsEnabled =
                coQuyen && !_dangXuLy && !_daLuu
                && kyDung && daChon && DocTien(txtPhuPhi.Text, out _);
        }

        private void NhapDuLieu_Click(
            object sender, MouseButtonEventArgs e)
        {
            if (_dangXuLy || _daLuu
                || cboPhong.SelectedItem is not DataRowView phong)
                return;

            if (!DocKy(out int thang, out int nam)
                || thang != _thang || nam != _nam)
            {
                txtThongBao.Text = "Hãy chọn lại kỳ và tải phòng.";
                return;
            }

            _nhap.TryGetValue(Khoa(phong.Row), out var cu);

            var labels = new[]
            {
                "Chỉ số điện cũ", "Chỉ số điện mới", "Đơn giá điện (VNĐ)",
                "Chỉ số nước cũ", "Chỉ số nước mới", "Đơn giá nước (VNĐ)",
                "Phí rác (VNĐ)", "Internet (VNĐ)", "Phí dịch vụ (VNĐ)"
            };

            decimal[] giaTri = cu == null
                ? new decimal[9]
                : new[]
                {
                    cu.DienCu, cu.DienMoi, cu.GiaDien,
                    cu.NuocCu, cu.NuocMoi, cu.GiaNuoc,
                    cu.Rac, cu.Internet, cu.DichVu
                };

            var window = new Window
            {
                Title = $"Nhập chi phí — {phong["TenHienThi"]} — {_thang}/{_nam}",
                Width = 520,
                Height = 680,
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var panel = new StackPanel { Margin = new Thickness(20) };
            var inputs = new TextBox[9];

            for (int i = 0; i < labels.Length; i++)
            {
                panel.Children.Add(new TextBlock { Text = labels[i] });

                inputs[i] = new TextBox
                {
                    Text = giaTri[i].ToString(
                        "0.##", CultureInfo.InvariantCulture),
                    Padding = new Thickness(8),
                    Margin = new Thickness(0, 5, 0, 12)
                };

                panel.Children.Add(inputs[i]);
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
                var v = new decimal[9];

                for (int i = 0; i < v.Length; i++)
                {
                    bool chiSo = i == 0 || i == 1 || i == 3 || i == 4;

                    bool hopLe = chiSo
                        ? decimal.TryParse(
                            inputs[i].Text.Trim().Replace(',', '.'),
                            NumberStyles.AllowDecimalPoint,
                            CultureInfo.InvariantCulture, out v[i])
                            && v[i] >= 0 && v[i] <= 99999999.99m
                            && decimal.Round(v[i], 2) == v[i]
                        : DocTien(inputs[i].Text, out v[i]);

                    if (!hopLe)
                    {
                        loi.Text = "Giá trị không hợp lệ: " + labels[i];
                        return;
                    }
                }

                if (v[1] < v[0] || v[4] < v[3])
                {
                    loi.Text = "Chỉ số mới phải lớn hơn hoặc bằng chỉ số cũ.";
                    return;
                }

                if ((v[1] > v[0] && v[2] == 0)
                    || (v[4] > v[3] && v[5] == 0))
                {
                    loi.Text = "Có tiêu thụ điện/nước thì đơn giá phải lớn hơn 0.";
                    return;
                }

                var phi = new DuLieuPhi
                {
                    DienCu = v[0],
                    DienMoi = v[1],
                    GiaDien = v[2],
                    NuocCu = v[3],
                    NuocMoi = v[4],
                    GiaNuoc = v[5],
                    Rac = v[6],
                    Internet = v[7],
                    DichVu = v[8],
                    PhuPhi = cu?.PhuPhi ?? 0,
                    MaHoaDon = cu?.MaHoaDon ?? TaoMa("HDON")
                };

                if (TaoKhoanThu(phong.Row, phi).Sum(x => x.ThanhTien)
                    > 999999999999999999m)
                {
                    loi.Text = "Tổng tiền vượt giới hạn database.";
                    return;
                }

                _nhap[Khoa(phong.Row)] = phi;
                window.DialogResult = true;
            };

            panel.Children.Add(btn);

            window.Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = panel
            };

            if (window.ShowDialog() == true)
            {
                txtThongBao.Text = "Đã nhập dữ liệu nháp, chưa phát hành hóa đơn.";
                HienThi();
            }
        }

        private static List<KhoanThu> TaoKhoanThu(
            DataRow phong, DuLieuPhi p)
        {
            return new List<KhoanThu>
            {
                new KhoanThu
                {
                    Loai = "Tiền phòng", SoLuong = 1,
                    DonGia = Convert.ToDecimal(phong["GiaThue"]),
                    GhiChu = "Tiền phòng theo hợp đồng, tính đủ tháng"
                },
                new KhoanThu
                {
                    Loai = "Điện", Cu = p.DienCu, Moi = p.DienMoi,
                    SoLuong = p.DienMoi - p.DienCu, DonGia = p.GiaDien,
                    GhiChu = "Tiền điện"
                },
                new KhoanThu
                {
                    Loai = "Nước", Cu = p.NuocCu, Moi = p.NuocMoi,
                    SoLuong = p.NuocMoi - p.NuocCu, DonGia = p.GiaNuoc,
                    GhiChu = "Tiền nước"
                },
                new KhoanThu
                {
                    Loai = "Dịch vụ", SoLuong = 1,
                    DonGia = p.Rac, GhiChu = "Phí rác"
                },
                new KhoanThu
                {
                    Loai = "Dịch vụ", SoLuong = 1,
                    DonGia = p.Internet, GhiChu = "Internet"
                },
                new KhoanThu
                {
                    Loai = "Dịch vụ", SoLuong = 1,
                    DonGia = p.DichVu, GhiChu = "Phí dịch vụ"
                },
                new KhoanThu
                {
                    Loai = "Phụ phí", SoLuong = 1,
                    DonGia = p.PhuPhi, GhiChu = "Phụ phí phát sinh"
                }
            };
        }

        private async void Luu_Click(object sender, RoutedEventArgs e)
        {
            await PhatHanhAsync(false);
        }

        private async void ToanDay_Click(object sender, RoutedEventArgs e)
        {
            await PhatHanhAsync(true);
        }

        private async Task PhatHanhAsync(bool toanDay)
        {
            if (_dangXuLy || _daLuu || _phong == null
                || cboPhong.SelectedItem is not DataRowView chon)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
                return;

            if (!DocKy(out int thang, out int nam)
                || thang != _thang || nam != _nam
                || !DocTien(txtPhuPhi.Text, out _))
            {
                txtThongBao.Text = "Kiểm tra kỳ hóa đơn và phụ phí.";
                return;
            }

            var danhSach = toanDay
                ? _phong.Rows.Cast<DataRow>()
                    .Where(r => Convert.ToString(r["MaDayTro"])
                        == Convert.ToString(chon["MaDayTro"]))
                    .ToList()
                : new List<DataRow> { chon.Row };

            foreach (var phong in danhSach)
            {
                if (!_nhap.TryGetValue(Khoa(phong), out var phi))
                {
                    txtThongBao.Text =
                        "Chưa nhập dữ liệu cho " + phong["TenHienThi"]
                        + ". Chọn phòng đó và nhập trước.";
                    return;
                }

                decimal tongPhong =
                    TaoKhoanThu(phong, phi).Sum(x => x.ThanhTien);

                if (tongPhong <= 0 ||
                    tongPhong > 999999999999999999m)
                {
                    txtThongBao.Text =
                        "Tổng tiền không hợp lệ: " + phong["TenHienThi"];
                    return;
                }
            }

            decimal tongTatCa = danhSach.Sum(
                r => TaoKhoanThu(r, _nhap[Khoa(r)]).Sum(x => x.ThanhTien));

            if (MessageBox.Show(
                $"Phát hành {danhSach.Count} hóa đơn kỳ {_thang}/{_nam}?\n"
                + $"Tổng tiền: {Tien(tongTatCa)}\n\n"
                + "Tiền phòng tính đủ tháng. Hạn thanh toán theo hợp đồng.",
                "Xác nhận phát hành",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            _dangXuLy = true;
            HienThi();
            txtThongBao.Text = "Đang phát hành hóa đơn...";

            bool thanhCong = false;

            try
            {
                using var connection = Database.CreateConnection();
                await connection.OpenAsync();

                using var transaction =
                    connection.BeginTransaction(IsolationLevel.Serializable);

                // Thứ tự cố định khi lập nhiều phòng.
                foreach (var phong in danhSach.OrderBy(
                    r => Convert.ToString(r["MaPhong"])))
                {
                    await LuuMotHoaDonAsync(
                        connection, transaction, phong,
                        _nhap[Khoa(phong)], user.MaNguoiDung);
                }

                transaction.Commit();
                thanhCong = true;
                _daLuu = true;
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                txtThongBao.Text =
                    "Có hóa đơn đã được tạo hoặc mã bị trùng. "
                    + "Toàn bộ lượt phát hành đã hủy. Hãy tải lại phòng.";
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                txtThongBao.Text =
                    "Dữ liệu đang được cập nhật. Lượt phát hành đã hủy; thử lại.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không phát hành được. Toàn bộ lượt phát hành đã hủy:\n"
                    + ex.Message;
            }
            finally
            {
                _dangXuLy = false;
                HienThi();
            }

            if (thanhCong)
            {
                MessageBox.Show(
                    $"Đã phát hành {danhSach.Count} hóa đơn.",
                    "Thành công");

                DaLuu?.Invoke();
            }
        }

        private async Task LuuMotHoaDonAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            DataRow phong,
            DuLieuPhi phi,
            string quanLy)
        {
            var khoanThu = TaoKhoanThu(phong, phi);
            decimal tong = khoanThu.Sum(x => x.ThanhTien);

            int ngay = Math.Min(
                Convert.ToInt32(phong["KyThanhToan"]),
                DateTime.DaysInMonth(_nam, _thang));

            DateTime han = new DateTime(_nam, _thang, ngay);

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText = @"
                    DECLARE @Khoa VARCHAR(20);
                    DECLARE @Gia DECIMAL(18,0);
                    DECLARE @Ky INT;

                    SELECT @Khoa = p.MaPhong
                    FROM dbo.PhongTro p WITH (UPDLOCK, HOLDLOCK)
                    WHERE p.MaPhong = @MaPhong
                      AND EXISTS (
                          SELECT 1 FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @QuanLy
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      );

                    IF @Khoa IS NULL
                        THROW 50001, N'Bạn không còn quyền với phòng.', 1;

                    SELECT @Gia = h.GiaThue, @Ky = h.KyThanhToan
                    FROM dbo.HopDongThue h WITH (UPDLOCK, HOLDLOCK)
                    WHERE h.MaHopDong = @MaHopDong
                      AND h.MaPhong = @MaPhong
                      AND h.TrangThai IN (N'Hiệu lực', N'Hết hạn')
                      AND h.NgayBatDau <=
                          EOMONTH(DATEFROMPARTS(@Nam,@Thang,1))
                      AND h.NgayKetThuc >=
                          DATEFROMPARTS(@Nam,@Thang,1);

                    IF @Gia IS NULL OR @Gia <> @GiaCu OR @Ky <> @KyCu
                        THROW 50002,
                            N'Hợp đồng đã thay đổi. Hãy tải lại phòng.', 1;

                    IF EXISTS (
                        SELECT 1 FROM dbo.HoaDon WITH (UPDLOCK, HOLDLOCK)
                        WHERE MaHopDong = @MaHopDong
                          AND Thang = @Thang AND Nam = @Nam
                    )
                        THROW 50003, N'Hóa đơn của kỳ này đã tồn tại.', 1;

                    INSERT INTO dbo.HoaDon
                        (MaHoaDon, MaHopDong, MaPhong,
                         Thang, Nam, HanThanhToan, TongTien, TrangThai)
                    VALUES
                        (@MaHoaDon, @MaHopDong, @MaPhong,
                         @Thang, @Nam, @Han, @Tong, N'Chưa thanh toán');";

                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20).Value = quanLy;
                command.Parameters.Add(
                    "@MaHoaDon", SqlDbType.VarChar, 20).Value = phi.MaHoaDon;
                command.Parameters.Add(
                    "@MaHopDong", SqlDbType.VarChar, 20)
                    .Value = phong["MaHopDong"];
                command.Parameters.Add(
                    "@MaPhong", SqlDbType.VarChar, 20)
                    .Value = phong["MaPhong"];
                command.Parameters.Add(
                    "@Thang", SqlDbType.Int).Value = _thang;
                command.Parameters.Add(
                    "@Nam", SqlDbType.Int).Value = _nam;
                command.Parameters.Add(
                    "@KyCu", SqlDbType.Int).Value = phong["KyThanhToan"];
                command.Parameters.Add(
                    "@Han", SqlDbType.Date).Value = han;

                ThemSo(command, "@GiaCu", 18, 0,
                    Convert.ToDecimal(phong["GiaThue"]));
                ThemSo(command, "@Tong", 18, 0, tong);

                await command.ExecuteNonQueryAsync();
            }

            foreach (var khoan in khoanThu)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                command.CommandText = @"
                    INSERT INTO dbo.ChiTietHoaDon
                        (MaChiTietHoaDon, MaHoaDon, LoaiKhoanThu,
                         ChiSoCu, ChiSoMoi, SoLuong,
                         DonGia, ThanhTien, GhiChu)
                    VALUES
                        (@MaCT, @MaHoaDon, @Loai,
                         @Cu, @Moi, @SoLuong,
                         @Gia, @ThanhTien, @GhiChu);";

                command.Parameters.Add(
                    "@MaCT", SqlDbType.VarChar, 20).Value = TaoMa("CT");
                command.Parameters.Add(
                    "@MaHoaDon", SqlDbType.VarChar, 20).Value = phi.MaHoaDon;
                command.Parameters.Add(
                    "@Loai", SqlDbType.NVarChar, 50).Value = khoan.Loai;
                command.Parameters.Add(
                    "@GhiChu", SqlDbType.NVarChar, 255).Value = khoan.GhiChu;

                ThemSo(command, "@Cu", 10, 2, khoan.Cu);
                ThemSo(command, "@Moi", 10, 2, khoan.Moi);
                ThemSo(command, "@SoLuong", 10, 2, khoan.SoLuong);
                ThemSo(command, "@Gia", 18, 0, khoan.DonGia);
                ThemSo(command, "@ThanhTien", 18, 0, khoan.ThanhTien);

                await command.ExecuteNonQueryAsync();
            }
        }

        private static void ThemSo(
            SqlCommand command, string ten,
            byte precision, byte scale, decimal? giaTri)
        {
            var p = command.Parameters.Add(ten, SqlDbType.Decimal);
            p.Precision = precision;
            p.Scale = scale;
            p.Value = giaTri.HasValue
                ? (object)giaTri.Value : DBNull.Value;
        }

        private void QuayLai_Click(object sender, RoutedEventArgs e)
        {
            if (!_dangXuLy)
                QuayLai?.Invoke();
        }
    }
}