using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QuanLyChuoiNhaTro.Services.Owner;

namespace QuanLyChuoiNhaTro.Views.Owner
{
    public partial class ChiTietHoaDonView : UserControl
    {
        private readonly string _maHoaDon;
        private bool _coDuLieu;
        private bool _dangXuLy;

        public event Action? QuayLaiRequested;

        public ChiTietHoaDonView(string maHoaDon)
        {
            InitializeComponent();

            _maHoaDon = maHoaDon;

            Loaded += ChiTietHoaDonView_Loaded;
        }

        private async void ChiTietHoaDonView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= ChiTietHoaDonView_Loaded;

            await TaiChiTietAsync();
        }

        private async Task TaiChiTietAsync()
        {
            _coDuLieu = false;
            CapNhatNutXuat();

            txtThongBao.Text = "Đang tải chi tiết hóa đơn...";
            txtThongBao.Visibility = Visibility.Visible;

            dgvKhoanThu.ItemsSource = null;

            txtEmptyKhoanThu.Text = "Đang tải các khoản thu...";
            txtEmptyKhoanThu.Visibility = Visibility.Visible;

            try
            {
                var ketQua = await ChiTietHoaDonChuTroService
                    .LayChiTietAsync(_maHoaDon);

                if (ketQua.ThongTin.Rows.Count == 0)
                {
                    HienThiLoi(
                        "Không tìm thấy hóa đơn hoặc hóa đơn "
                        + "không thuộc quyền quản lý của bạn.");

                    txtEmptyKhoanThu.Text =
                        "Không có dữ liệu hóa đơn.";

                    return;
                }

                DataRow hoaDon = ketQua.ThongTin.Rows[0];

                txtMaHoaDon.Text =
                    LayChuoi(hoaDon, "MaHoaDon");

                txtMaHopDong.Text =
                    LayChuoi(hoaDon, "MaHopDong");

                txtKhachThue.Text =
                    LayChuoi(hoaDon, "HoTenKhach");

                txtPhong.Text =
                    LayChuoi(hoaDon, "SoPhong");

                txtDayTro.Text =
                    LayChuoi(hoaDon, "TenDayTro");

                txtTrangThai.Text =
                    LayChuoi(hoaDon, "TrangThai");

                txtKyThanhToan.Text =
                    LayChuoi(hoaDon, "Thang")
                    + "/"
                    + LayChuoi(hoaDon, "Nam");

                txtNgayLap.Text =
                    DinhDangNgay(hoaDon, "NgayLap");

                txtHanThanhToan.Text =
                    DinhDangNgay(hoaDon, "HanThanhToan");

                txtPhuongThuc.Text =
                    LayChuoi(hoaDon, "PhuongThucThanhToan");

                txtTongTien.Text =
                    DinhDangTien(hoaDon, "TongTien");

                if (txtTrangThai.Text == "Đã thanh toán")
                {
                    txtTrangThai.Foreground =
                        TaoMau(21, 128, 61);
                }
                else if (txtTrangThai.Text == "Còn nợ")
                {
                    txtTrangThai.Foreground =
                        TaoMau(220, 38, 38);
                }
                else
                {
                    txtTrangThai.Foreground =
                        TaoMau(180, 83, 9);
                }

                dgvKhoanThu.ItemsSource =
                    ketQua.CacKhoanThu.DefaultView;

                if (ketQua.CacKhoanThu.Rows.Count == 0)
                {
                    txtEmptyKhoanThu.Text =
                        "Hóa đơn chưa có chi tiết khoản thu.";

                    txtEmptyKhoanThu.Visibility =
                        Visibility.Visible;
                }
                else
                {
                    txtEmptyKhoanThu.Visibility =
                        Visibility.Collapsed;
                }

                txtThongBao.Visibility =
                    Visibility.Collapsed;

                _coDuLieu = true;
                CapNhatNutXuat();
            }
            catch (InvalidOperationException ex)
            {
                HienThiLoi(ex.Message);
            }
            catch (SqlException)
            {
                HienThiLoi(
                    "Không thể tải chi tiết hóa đơn từ SQL Server. "
                    + "Vui lòng kiểm tra kết nối.");
            }
            catch (Exception)
            {
                HienThiLoi(
                    "Đã xảy ra lỗi khi tải chi tiết hóa đơn.");
            }
        }

        private static string LayChuoi(
            DataRow row,
            string tenCot)
        {
            if (!row.Table.Columns.Contains(tenCot))
                return "—";

            if (row[tenCot] == DBNull.Value)
                return "—";

            string giaTri =
                Convert.ToString(row[tenCot]) ?? "";

            return string.IsNullOrWhiteSpace(giaTri)
                ? "—"
                : giaTri;
        }

        private static string DinhDangNgay(
            DataRow row,
            string tenCot)
        {
            if (row[tenCot] == DBNull.Value)
                return "—";

            DateTime ngay =
                Convert.ToDateTime(row[tenCot]);

            return ngay.ToString("dd/MM/yyyy");
        }

        private static string DinhDangTien(
            DataRow row,
            string tenCot)
        {
            if (row[tenCot] == DBNull.Value)
                return "—";

            decimal soTien =
                Convert.ToDecimal(row[tenCot]);

            return soTien.ToString(
                "N0",
                CultureInfo.GetCultureInfo("vi-VN")) + " đ";
        }

        private static SolidColorBrush TaoMau(
            byte r,
            byte g,
            byte b)
        {
            return new SolidColorBrush(
                Color.FromRgb(r, g, b));
        }

        private void HienThiLoi(string noiDung)
        {
            _coDuLieu = false;
            CapNhatNutXuat();

            txtThongBao.Text = noiDung;
            txtThongBao.Foreground =
                TaoMau(220, 38, 38);

            txtThongBao.Visibility =
                Visibility.Visible;
        }

        private void CapNhatNutXuat()
        {
            btnTaiPdf.IsEnabled =
                _coDuLieu && !_dangXuLy;

            btnInHoaDon.IsEnabled =
                _coDuLieu && !_dangXuLy;
        }

        private void HienThiThongBao(
            string noiDung,
            bool thanhCong = false)
        {
            txtThongBao.Text = noiDung;

            txtThongBao.Foreground = thanhCong
                ? TaoMau(21, 128, 61)
                : TaoMau(107, 114, 128);

            txtThongBao.Visibility =
                Visibility.Visible;
        }

        private async void TaiPdf_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_coDuLieu || _dangXuLy)
                return;

            _dangXuLy = true;
            CapNhatNutXuat();

            try
            {
                string tenFile = "HoaDon_" + _maHoaDon;

                foreach (char kyTu
                    in Path.GetInvalidFileNameChars())
                {
                    tenFile = tenFile.Replace(kyTu, '_');
                }

                var dialog = new SaveFileDialog
                {
                    Title = "Lưu bản PDF hóa đơn",
                    FileName = tenFile + ".pdf",
                    Filter = "Tệp PDF (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    AddExtension = true,
                    OverwritePrompt = true,
                    CheckPathExists = true
                };

                Window? window = Window.GetWindow(this);

                bool? accepted = window != null
                    ? dialog.ShowDialog(window)
                    : dialog.ShowDialog();

                if (accepted != true)
                {
                    HienThiThongBao(
                        "Đã hủy tải PDF hóa đơn.");

                    return;
                }

                HienThiThongBao(
                    "Đang tạo bản PDF hóa đơn...");

                var duLieu = await HoaDonChuTroPdfService
                    .LayDuLieuAsync(_maHoaDon);

                byte[] pdf = await Task.Run(
                    () => HoaDonChuTroPdfService
                        .TaoPdf(duLieu));

                if (!IsLoaded)
                    return;

                await LuuPdfAsync(
                    dialog.FileName,
                    pdf);

                if (!IsLoaded)
                    return;

                HienThiThongBao(
                    "Đã lưu bản PDF tại: "
                    + dialog.FileName,
                    true);

                MessageBox.Show(
                    window,
                    "Đã lưu bản PDF hóa đơn thành công.\n\n"
                    + dialog.FileName,
                    "Tải PDF hóa đơn",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (SqlException)
            {
                HienThiLoiXuat(
                    "Không lấy được dữ liệu hóa đơn. "
                    + "Vui lòng kiểm tra kết nối SQL Server.");
            }
            catch (UnauthorizedAccessException)
            {
                HienThiLoiXuat(
                    "Không có quyền lưu tệp tại vị trí đã chọn. "
                    + "Vui lòng chọn thư mục khác.");
            }
            catch (IOException)
            {
                HienThiLoiXuat(
                    "Không thể lưu bản PDF. "
                    + "Tệp có thể đang được mở "
                    + "hoặc thư mục không còn truy cập được.");
            }
            catch (Exception ex)
            {
                HienThiLoiXuat(
                    "Không tải được PDF hóa đơn: "
                    + ex.Message);
            }
            finally
            {
                _dangXuLy = false;
                CapNhatNutXuat();
            }
        }

        private static async Task LuuPdfAsync(
            string duongDan,
            byte[] pdf)
        {
            // Ghi xong tệp tạm rồi mới thay thế tệp đích.
            string tepTam = duongDan
                + "."
                + Guid.NewGuid().ToString("N")
                + ".tmp";

            try
            {
                await File.WriteAllBytesAsync(
                    tepTam,
                    pdf);

                File.Move(
                    tepTam,
                    duongDan,
                    true);
            }
            finally
            {
                if (File.Exists(tepTam))
                    File.Delete(tepTam);
            }
        }

        private async void InHoaDon_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_coDuLieu || _dangXuLy)
                return;

            _dangXuLy = true;
            CapNhatNutXuat();

            try
            {
                var dialog = new PrintDialog();

                if (dialog.ShowDialog() != true)
                {
                    HienThiThongBao(
                        "Đã hủy in hóa đơn.");

                    return;
                }

                HienThiThongBao(
                    "Đang chuẩn bị bản in hóa đơn...");

                var duLieu = await HoaDonChuTroPdfService
                    .LayDuLieuAsync(_maHoaDon);

                if (!IsLoaded)
                    return;

                FlowDocument document = TaoBanIn(
                    duLieu,
                    dialog.PrintableAreaWidth,
                    dialog.PrintableAreaHeight);

                var paginator =
                    ((IDocumentPaginatorSource)document)
                    .DocumentPaginator;

                dialog.PrintDocument(
                    paginator,
                    "Hóa đơn " + duLieu.MaHoaDon);

                HienThiThongBao(
                    "Đã gửi hóa đơn tới máy in.",
                    true);
            }
            catch (SqlException)
            {
                HienThiLoiXuat(
                    "Không lấy được dữ liệu hóa đơn. "
                    + "Vui lòng kiểm tra kết nối SQL Server.");
            }
            catch (Exception ex)
            {
                HienThiLoiXuat(
                    "Không in được hóa đơn: "
                    + ex.Message);
            }
            finally
            {
                _dangXuLy = false;
                CapNhatNutXuat();
            }
        }

        private static FlowDocument TaoBanIn(
            HoaDonChuTroPdfService.DuLieuHoaDon duLieu,
            double pageWidth,
            double pageHeight)
        {
            if (!double.IsFinite(pageWidth)
                || !double.IsFinite(pageHeight)
                || pageWidth <= 96
                || pageHeight <= 96)
            {
                throw new InvalidOperationException(
                    "Máy in không cung cấp kích thước trang hợp lệ.");
            }

            var document = new FlowDocument
            {
                PageWidth = pageWidth,
                PageHeight = pageHeight,
                PagePadding = new Thickness(24),
                ColumnWidth = pageWidth - 48,
                FontFamily = new FontFamily("Arial"),
                FontSize = 14
            };

            Paragraph ThemDong(
                string text,
                bool bold = false,
                double fontSize = 14)
            {
                var paragraph = new Paragraph(
                    new Run(text))
                {
                    Margin = new Thickness(0, 0, 0, 5),
                    FontSize = fontSize,
                    FontWeight = bold
                        ? FontWeights.Bold
                        : FontWeights.Normal
                };

                document.Blocks.Add(paragraph);

                return paragraph;
            }

            ThemDong(
                "HỆ THỐNG QUẢN LÝ NHÀ TRỌ",
                false,
                12).TextAlignment = TextAlignment.Center;

            Paragraph title = ThemDong(
                "HÓA ĐƠN TIỀN TRỌ",
                true,
                24);

            title.TextAlignment =
                TextAlignment.Center;

            title.Margin =
                new Thickness(0, 0, 0, 18);

            foreach (var thongTin in duLieu.ThongTin)
            {
                var paragraph = new Paragraph
                {
                    Margin = new Thickness(0, 0, 0, 5)
                };

                paragraph.Inlines.Add(
                    new Bold(
                        new Run(thongTin.Key + ": ")));

                paragraph.Inlines.Add(
                    new Run(thongTin.Value));

                document.Blocks.Add(paragraph);
            }

            Paragraph nhanKhoanThu = ThemDong(
                "CHI TIẾT CÁC KHOẢN THU",
                true);

            nhanKhoanThu.Margin =
                new Thickness(0, 12, 0, 8);

            nhanKhoanThu.KeepWithNext = true;

            var table = new Table
            {
                CellSpacing = 0,
                FontSize = 12
            };

            double tongTyLe =
                HoaDonChuTroPdfService.TyLeCot.Sum();

            foreach (double tyLe
                in HoaDonChuTroPdfService.TyLeCot)
            {
                table.Columns.Add(new TableColumn
                {
                    Width = new GridLength(
                        document.ColumnWidth
                        * tyLe
                        / tongTyLe)
                });
            }

            var group = new TableRowGroup();

            table.RowGroups.Add(group);

            TableCell TaoO(
                string text,
                bool header,
                int index)
            {
                var paragraph = new Paragraph(
                    new Run(text))
                {
                    Margin = new Thickness(0),

                    TextAlignment = header
                        ? TextAlignment.Center
                        : index >= 1 && index <= 5
                            ? TextAlignment.Right
                            : TextAlignment.Left
                };

                return new TableCell(paragraph)
                {
                    Padding = new Thickness(4),
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(0.5),

                    Background = header
                        ? Brushes.LightGray
                        : Brushes.White,

                    FontWeight = header
                        ? FontWeights.Bold
                        : FontWeights.Normal
                };
            }

            var headerRow = new TableRow();

            for (int i = 0;
                i < HoaDonChuTroPdfService.TieuDeCot.Count;
                i++)
            {
                headerRow.Cells.Add(
                    TaoO(
                        HoaDonChuTroPdfService.TieuDeCot[i],
                        true,
                        i));
            }

            group.Rows.Add(headerRow);

            if (duLieu.CacKhoanThu.Count == 0)
            {
                var emptyRow = new TableRow();

                var cell = TaoO(
                    "Hóa đơn chưa có chi tiết khoản thu.",
                    false,
                    0);

                cell.ColumnSpan =
                    HoaDonChuTroPdfService.TieuDeCot.Count;

                emptyRow.Cells.Add(cell);
                group.Rows.Add(emptyRow);
            }
            else
            {
                foreach (string[] khoanThu
                    in duLieu.CacKhoanThu)
                {
                    var row = new TableRow();

                    for (int i = 0;
                        i < khoanThu.Length;
                        i++)
                    {
                        row.Cells.Add(
                            TaoO(
                                khoanThu[i],
                                false,
                                i));
                    }

                    group.Rows.Add(row);
                }
            }

            document.Blocks.Add(table);

            Paragraph total = ThemDong(
                "TỔNG TIỀN: " + duLieu.TongTien,
                true,
                18);

            total.TextAlignment =
                TextAlignment.Right;

            total.Margin =
                new Thickness(0, 14, 0, 0);

            Paragraph ngayXuat = ThemDong(
                "Ngày xuất: "
                + duLieu.ThoiDiemXuat.ToString(
                    "dd/MM/yyyy HH:mm",
                    CultureInfo.GetCultureInfo("vi-VN")),
                false,
                12);

            ngayXuat.Margin =
                new Thickness(0, 16, 0, 0);

            return document;
        }

        private void HienThiLoiXuat(string noiDung)
        {
            if (!IsLoaded)
                return;

            txtThongBao.Text = noiDung;

            txtThongBao.Foreground =
                TaoMau(220, 38, 38);

            txtThongBao.Visibility =
                Visibility.Visible;

            MessageBox.Show(
                Window.GetWindow(this),
                noiDung,
                "Xuất/In hóa đơn",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void QuayLai_Click(
            object sender,
            RoutedEventArgs e)
        {
            QuayLaiRequested?.Invoke();
        }
    }
}