using System;
using System.Data;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class QuanLyHoaDonView : UserControl
    {
        private DataTable? _duLieu;
        private bool _dangXuLy;

        public QuanLyHoaDonView()
        {
            InitializeComponent();

            Loaded += View_Loaded;
            btnTimKiem.Click += TimKiem_Click;
            dgHoaDon.SelectionChanged += ChonHoaDon_Changed;
            btnXemChiTiet.Click += XemChiTiet_Click;
            btnXacNhanThanhToan.Click += ThanhToan_Click;
        }

        private const string DieuKienQuyen = @"
            EXISTS (
                SELECT 1 FROM dbo.NhanVien nv
                WHERE nv.MaDayTro = p.MaDayTro
                  AND nv.MaNguoiDung = @QuanLy
                  AND nv.ChucVu = N'Quản lý'
                  AND nv.TrangThaiLamViec = N'Đang làm'
            )";

        private static string Tien(decimal soTien)
        {
            return soTien.ToString(
                "N0", CultureInfo.GetCultureInfo("vi-VN")) + " VNĐ";
        }

        private async void View_Loaded(
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
                dgHoaDon.ItemsSource = null;
                txtDanhSachTrong.Text = "Bạn không có quyền xem hóa đơn.";
                txtDanhSachTrong.Visibility = Visibility.Visible;
                CapNhatNut();
                return;
            }

            _dangXuLy = true;
            CapNhatNut();
            txtTrangThai.Text = "Đang tải hóa đơn...";

            try
            {
                using var connection = Database.CreateConnection();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT
                        hd.MaHoaDon,
                        hd.MaHopDong,
                        CAST(hd.Thang AS VARCHAR(2)) + '/'
                            + CAST(hd.Nam AS VARCHAR(4)) AS KyHoaDon,
                        d.TenDayTro + N' - ' + p.SoPhong AS TenPhong,
                        n.HoTen AS TenKhachThue,
                        hd.TongTien,
                        COALESCE(tt.DaTra, 0) AS DaTra,
                        hd.TongTien - COALESCE(tt.DaTra, 0) AS ConNo,
                        CASE WHEN COALESCE(tt.DaTra, 0) > 0
                            THEN N'Còn nợ'
                            ELSE N'Chưa thanh toán'
                        END AS TrangThai
                    FROM dbo.HoaDon hd
                    JOIN dbo.HopDongThue h ON h.MaHopDong = hd.MaHopDong
                    JOIN dbo.KhachThue k ON k.MaKhachThue = h.MaKhachThue
                    JOIN dbo.NguoiDung n ON n.MaNguoiDung = k.MaNguoiDung
                    JOIN dbo.PhongTro p ON p.MaPhong = hd.MaPhong
                    JOIN dbo.DayTro d ON d.MaDayTro = p.MaDayTro
                    OUTER APPLY (
                        SELECT SUM(t.SoTien) AS DaTra
                        FROM dbo.ThanhToan t
                        WHERE t.MaHoaDon = hd.MaHoaDon
                    ) tt
                    WHERE hd.TongTien > COALESCE(tt.DaTra, 0)
                      AND " + DieuKienQuyen + @"
                    ORDER BY hd.Nam DESC, hd.Thang DESC, hd.MaHoaDon;";

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
                _duLieu = null;
                dgHoaDon.ItemsSource = null;
                txtDanhSachTrong.Text = "Không tải được hóa đơn.";
                txtDanhSachTrong.Visibility = Visibility.Visible;
                txtTrangThai.Text = "Lỗi tải dữ liệu.";

                MessageBox.Show(
                    ex.Message, "Lỗi tải hóa đơn",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _dangXuLy = false;
                CapNhatNut();
            }
        }

        private void CapNhatNut()
        {
            bool coQuyen =
                UserSession.CurrentUser?.VaiTro == "Nhân viên quản lý";

            bool sanSang = coQuyen && !_dangXuLy && _duLieu != null;
            bool daChon = sanSang && dgHoaDon.SelectedItem is DataRowView;

            txtTimKiem.IsEnabled = sanSang;
            btnTimKiem.IsEnabled = sanSang;
            btnXemChiTiet.IsEnabled = daChon;
            btnXacNhanThanhToan.IsEnabled = daChon;
        }

        private void ChonHoaDon_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            CapNhatNut();
        }

        private void TimKiem_Click(object sender, RoutedEventArgs e)
        {
            if (!_dangXuLy)
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
                    "MaHoaDon", "TenPhong", "TenKhachThue", "KyHoaDon"
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

            dgHoaDon.ItemsSource = ketQua.DefaultView;

            txtDanhSachTrong.Text = _duLieu.Rows.Count == 0
                ? "Không có hóa đơn còn nợ trong phạm vi quản lý."
                : "Không có hóa đơn phù hợp.";

            txtDanhSachTrong.Visibility = ketQua.Rows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            txtTrangThai.Text =
                $"Hiển thị {ketQua.Rows.Count} hóa đơn còn nợ.";

            CapNhatNut();
        }

        private async void XemChiTiet_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangXuLy ||
                dgHoaDon.SelectedItem is not DataRowView dong)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
                return;

            string maHoaDon = Convert.ToString(dong["MaHoaDon"]) ?? "";

            _dangXuLy = true;
            CapNhatNut();

            try
            {
                using var connection = Database.CreateConnection();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT ct.LoaiKhoanThu, ct.SoLuong,
                           ct.DonGia, ct.ThanhTien
                    FROM dbo.ChiTietHoaDon ct
                    JOIN dbo.HoaDon hd ON hd.MaHoaDon = ct.MaHoaDon
                    JOIN dbo.PhongTro p ON p.MaPhong = hd.MaPhong
                    WHERE hd.MaHoaDon = @MaHoaDon
                      AND " + DieuKienQuyen + @"
                    ORDER BY ct.MaChiTietHoaDon;";

                command.Parameters.Add(
                    "@MaHoaDon", SqlDbType.VarChar, 20)
                    .Value = maHoaDon;
                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;

                await connection.OpenAsync();

                using var reader = await command.ExecuteReaderAsync();

                var noiDung = new StringBuilder();

                noiDung.AppendLine("Hóa đơn: " + maHoaDon);
                noiDung.AppendLine("Khách: " + dong["TenKhachThue"]);
                noiDung.AppendLine("Phòng: " + dong["TenPhong"]);
                noiDung.AppendLine();

                bool coChiTiet = false;

                while (await reader.ReadAsync())
                {
                    coChiTiet = true;

                    noiDung.AppendLine(
                        Convert.ToString(reader["LoaiKhoanThu"])
                        + ": " + reader["SoLuong"]
                        + " × " + Tien(Convert.ToDecimal(reader["DonGia"]))
                        + " = " + Tien(Convert.ToDecimal(reader["ThanhTien"])));
                }

                if (!coChiTiet)
                    noiDung.AppendLine("Không có khoản thu chi tiết được trả về.");

                noiDung.AppendLine();
                noiDung.AppendLine(
                    "Tổng tiền: " + Tien(Convert.ToDecimal(dong["TongTien"])));
                noiDung.AppendLine(
                    "Đã trả: " + Tien(Convert.ToDecimal(dong["DaTra"])));
                noiDung.AppendLine(
                    "Còn nợ: " + Tien(Convert.ToDecimal(dong["ConNo"])));

                MessageBox.Show(
                    noiDung.ToString(), "Chi tiết hóa đơn",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi xem hóa đơn");
            }
            finally
            {
                _dangXuLy = false;
                CapNhatNut();
            }
        }

        private async void ThanhToan_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangXuLy ||
                dgHoaDon.SelectedItem is not DataRowView dong)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
                return;

            string maHoaDon = Convert.ToString(dong["MaHoaDon"]) ?? "";
            decimal conNo = Convert.ToDecimal(dong["ConNo"]);

            var dialog = new Window
            {
                Title = "Ghi nhận thanh toán",
                Width = 440,
                Height = 390,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            panel.Children.Add(new TextBlock
            {
                Text = $"Hóa đơn: {maHoaDon}\nCòn nợ: {Tien(conNo)}",
                Margin = new Thickness(0, 0, 0, 16)
            });

            panel.Children.Add(new TextBlock { Text = "Số tiền đã nhận (VNĐ)" });

            var txtSoTien = new TextBox
            {
                Text = conNo.ToString("0", CultureInfo.InvariantCulture),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 6, 0, 14)
            };
            panel.Children.Add(txtSoTien);

            panel.Children.Add(new TextBlock { Text = "Phương thức thanh toán" });

            var cboPhuongThuc = new ComboBox
            {
                ItemsSource = new[] { "Tiền mặt", "Chuyển khoản", "Online" },
                SelectedIndex = -1,
                Margin = new Thickness(0, 6, 0, 14),
                MinHeight = 35
            };
            panel.Children.Add(cboPhuongThuc);

            var txtLoi = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            panel.Children.Add(txtLoi);

            decimal soTien = 0;
            string phuongThuc = "";

            var btnXacNhan = new Button
            {
                Content = "Xác nhận tiền đã nhận",
                Padding = new Thickness(12)
            };

            btnXacNhan.Click += (_, _) =>
            {
                if (!decimal.TryParse(
                        txtSoTien.Text.Trim(),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out soTien)
                    || soTien <= 0 || soTien > conNo)
                {
                    txtLoi.Text =
                        "Nhập số nguyên dương, không vượt quá số tiền còn nợ.";
                    return;
                }

                phuongThuc = cboPhuongThuc.SelectedItem?.ToString() ?? "";

                if (phuongThuc.Length == 0)
                {
                    txtLoi.Text = "Vui lòng chọn phương thức thanh toán.";
                    return;
                }

                if (MessageBox.Show(
                    dialog,
                    $"Xác nhận đã nhận {Tien(soTien)} bằng {phuongThuc}?",
                    "Xác nhận thanh toán",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    dialog.DialogResult = true;
                }
            };

            panel.Children.Add(btnXacNhan);
            dialog.Content = panel;

            if (dialog.ShowDialog() != true)
                return;

            _dangXuLy = true;
            IsEnabled = false;
            CapNhatNut();

            bool daLuu = false;

            try
            {
                using var connection = Database.CreateConnection();
                await connection.OpenAsync();

                using var transaction =
                    connection.BeginTransaction(IsolationLevel.Serializable);

                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                command.CommandText = @"
                    DECLARE @Tong DECIMAL(18,0);
                    DECLARE @DaTra DECIMAL(38,0);

                    SELECT @Tong = hd.TongTien
                    FROM dbo.HoaDon hd WITH (UPDLOCK, HOLDLOCK)
                    JOIN dbo.PhongTro p ON p.MaPhong = hd.MaPhong
                    WHERE hd.MaHoaDon = @MaHoaDon
                      AND " + DieuKienQuyen + @";

                    IF @Tong IS NULL
                        THROW 50001,
                            N'Hóa đơn không tồn tại hoặc bạn không có quyền.', 1;

                    SELECT @DaTra = COALESCE(SUM(t.SoTien), 0)
                    FROM dbo.ThanhToan t WITH (UPDLOCK, HOLDLOCK)
                    WHERE t.MaHoaDon = @MaHoaDon;

                    IF @SoTien <= 0 OR @SoTien > @Tong - @DaTra
                        THROW 50002,
                            N'Số tiền còn nợ đã thay đổi. Hãy tải lại danh sách.', 1;

                    INSERT INTO dbo.ThanhToan
                        (MaThanhToan, MaHoaDon, SoTien,
                         PhuongThuc, MaNguoiXacNhan, GhiChu)
                    VALUES
                        (@MaThanhToan, @MaHoaDon, @SoTien,
                         @PhuongThuc, @QuanLy,
                         N'Ghi nhận thanh toán từ màn hình quản lý');

                    UPDATE dbo.HoaDon
                    SET TrangThai = CASE
                            WHEN @DaTra + @SoTien >= @Tong
                                THEN N'Đã thanh toán'
                            ELSE N'Còn nợ'
                        END,
                        PhuongThucThanhToan = @PhuongThuc,
                        NgayThanhToan = GETDATE(),
                        MaGiaoDich = NULL
                    WHERE MaHoaDon = @MaHoaDon;";

                command.Parameters.Add(
                    "@MaHoaDon", SqlDbType.VarChar, 20)
                    .Value = maHoaDon;
                command.Parameters.Add(
                    "@MaThanhToan", SqlDbType.VarChar, 20)
                    .Value = "TT" + Guid.NewGuid().ToString("N")
                        .Substring(0, 18).ToUpperInvariant();
                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;
                command.Parameters.Add(
                    "@PhuongThuc", SqlDbType.NVarChar, 50)
                    .Value = phuongThuc;

                var thamSoTien = command.Parameters.Add(
                    "@SoTien", SqlDbType.Decimal);
                thamSoTien.Precision = 18;
                thamSoTien.Scale = 0;
                thamSoTien.Value = soTien;

                await command.ExecuteNonQueryAsync();
                transaction.Commit();

                daLuu = true;

                MessageBox.Show(
                    "Ghi nhận thanh toán thành công.",
                    "Thông báo");
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                MessageBox.Show(
                    "Dữ liệu đang được cập nhật. Tải lại danh sách và thử lại.",
                    "Thông báo");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không ghi nhận được thanh toán:\n" + ex.Message,
                    "Lỗi thanh toán");
            }
            finally
            {
                _dangXuLy = false;
                IsEnabled = true;
                CapNhatNut();
            }

            if (daLuu)
                await TaiDanhSachAsync();
        }
    }
}