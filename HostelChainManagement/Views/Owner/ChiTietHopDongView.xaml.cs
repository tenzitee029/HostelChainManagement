using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Owner
{
    public partial class ChiTietHopDongView : UserControl
    {
        private static readonly CultureInfo Vietnamese =
            CultureInfo.GetCultureInfo("vi-VN");

        private readonly string _maHopDong;

        private DataRow? _chiTiet;
        private bool _dangXuLy;

        public event Action? QuayLaiRequested;

        public ChiTietHopDongView(string maHopDong)
        {
            InitializeComponent();

            _maHopDong = maHopDong;

            CapNhatNutXuat();

            Loaded += ChiTietHopDongView_Loaded;
        }

        private async void ChiTietHopDongView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= ChiTietHopDongView_Loaded;

            await TaiChiTietAsync();
        }

        private async Task TaiChiTietAsync()
        {
            _chiTiet = null;
            CapNhatNutXuat();

            HienThiThongBao(
                "Đang tải chi tiết hợp đồng...");

            try
            {
                DataTable table =
                    await ChiTietHopDongChuTroService
                        .LayChiTietAsync(_maHopDong);

                if (!IsLoaded)
                    return;

                if (table.Rows.Count == 0)
                {
                    HienThiLoi(
                        "Không tìm thấy hợp đồng hoặc hợp đồng "
                        + "không thuộc quyền quản lý của bạn.");

                    return;
                }

                DataRow row = table.Rows[0];

                txtMaHopDong.Text =
                    LayChuoi(row, "MaHopDong");

                txtTrangThai.Text =
                    LayChuoi(row, "TrangThai");

                txtKhachThue.Text =
                    LayChuoi(row, "KhachThue");

                txtPhong.Text =
                    LayChuoi(row, "SoPhong");

                txtDayTro.Text =
                    LayChuoi(row, "TenDayTro");

                txtGiaThue.Text =
                    DinhDangTien(row, "GiaThue");

                txtNgayBatDau.Text =
                    DinhDangNgay(row, "NgayBatDau");

                txtNgayKetThuc.Text =
                    DinhDangNgay(row, "NgayKetThuc");

                txtTrangThai.Foreground =
                    txtTrangThai.Text switch
                    {
                        "Hiệu lực" =>
                            TaoMau(21, 128, 61),

                        "Đã thanh lý" =>
                            TaoMau(107, 114, 128),

                        _ =>
                            TaoMau(180, 83, 9)
                    };

                _chiTiet = row;

                CapNhatNutXuat();

                txtThongBao.Visibility =
                    Visibility.Collapsed;
            }
            catch (InvalidOperationException ex)
            {
                HienThiLoi(ex.Message);
            }
            catch (SqlException)
            {
                HienThiLoi(
                    "Không thể tải hợp đồng từ SQL Server. "
                    + "Vui lòng kiểm tra kết nối.");
            }
            catch (Exception ex)
            {
                HienThiLoi(
                    "Đã xảy ra lỗi khi tải chi tiết hợp đồng: "
                    + ex.Message);
            }
        }

        private void CapNhatNutXuat()
        {
            bool choPhep =
                _chiTiet != null && !_dangXuLy;

            btnTaiPdf.IsEnabled = choPhep;
            btnInHopDong.IsEnabled = choPhep;
        }

        private static string LayChuoi(
            DataRow row,
            string tenCot,
            string macDinh = "—")
        {
            if (!row.Table.Columns.Contains(tenCot)
                || row.IsNull(tenCot))
            {
                return macDinh;
            }

            string giaTri = Convert.ToString(
                row[tenCot],
                Vietnamese) ?? "";

            return string.IsNullOrWhiteSpace(giaTri)
                ? macDinh
                : giaTri;
        }

        private static string DinhDangNgay(
            DataRow row,
            string tenCot,
            string macDinh = "—")
        {
            if (!row.Table.Columns.Contains(tenCot)
                || row.IsNull(tenCot))
            {
                return macDinh;
            }

            return Convert.ToDateTime(
                    row[tenCot],
                    Vietnamese)
                .ToString(
                    "dd/MM/yyyy",
                    Vietnamese);
        }

        private static string DinhDangTien(
            DataRow row,
            string tenCot,
            string macDinh = "—",
            string donVi = " đ")
        {
            if (!row.Table.Columns.Contains(tenCot)
                || row.IsNull(tenCot))
            {
                return macDinh;
            }

            return Convert.ToDecimal(
                    row[tenCot],
                    Vietnamese)
                .ToString("N0", Vietnamese)
                + donVi;
        }

        private static SolidColorBrush TaoMau(
            byte r,
            byte g,
            byte b)
        {
            return new SolidColorBrush(
                Color.FromRgb(r, g, b));
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

        private void HienThiLoi(string noiDung)
        {
            _chiTiet = null;

            CapNhatNutXuat();

            txtThongBao.Text = noiDung;

            txtThongBao.Foreground =
                TaoMau(220, 38, 38);

            txtThongBao.Visibility =
                Visibility.Visible;
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
                "Xuất/In hợp đồng",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private async void TaiPdf_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_chiTiet == null || _dangXuLy)
                return;

            _dangXuLy = true;

            CapNhatNutXuat();

            try
            {
                string tenFile =
                    "HopDong_" + _maHopDong;

                foreach (char kyTu
                    in Path.GetInvalidFileNameChars())
                {
                    tenFile = tenFile.Replace(
                        kyTu,
                        '_');
                }

                var dialog = new SaveFileDialog
                {
                    Title = "Lưu bản PDF hợp đồng thuê",
                    FileName = tenFile + ".pdf",
                    Filter = "Tệp PDF (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    AddExtension = true,
                    OverwritePrompt = true,
                    CheckPathExists = true
                };

                Window? window =
                    Window.GetWindow(this);

                bool? accepted = window != null
                    ? dialog.ShowDialog(window)
                    : dialog.ShowDialog();

                if (accepted != true)
                {
                    HienThiThongBao(
                        "Đã hủy tải PDF hợp đồng.");

                    return;
                }

                HienThiThongBao(
                    "Đang tạo bản PDF hợp đồng...");

                byte[] pdf =
                    await HopDongChuTroPdfService
                        .TaoPdfAsync(_maHopDong);

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
                    "Đã lưu bản PDF hợp đồng thành công.\n\n"
                    + dialog.FileName,
                    "Tải PDF hợp đồng",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (SqlException)
            {
                HienThiLoiXuat(
                    "Không lấy được thông tin hợp đồng. "
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
                    "Không tải được PDF hợp đồng: "
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

        private async void InHopDong_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_chiTiet == null || _dangXuLy)
                return;

            _dangXuLy = true;

            CapNhatNutXuat();

            try
            {
                var dialog = new PrintDialog();

                if (dialog.ShowDialog() != true)
                {
                    HienThiThongBao(
                        "Đã hủy in hợp đồng.");

                    return;
                }

                HienThiThongBao(
                    "Đang chuẩn bị bản in hợp đồng...");

                // Lấy dữ liệu mới và kiểm tra lại quyền trước khi in.
                DataTable table =
                    await ChiTietHopDongChuTroService
                        .LayChiTietAsync(_maHopDong);

                if (!IsLoaded)
                    return;

                if (table.Rows.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Không tìm thấy hợp đồng hoặc hợp đồng "
                        + "không thuộc quyền quản lý của bạn.");
                }

                FlowDocument document = TaoBanIn(
                    table.Rows[0],
                    dialog.PrintableAreaWidth,
                    dialog.PrintableAreaHeight);

                var paginator =
                    ((IDocumentPaginatorSource)document)
                    .DocumentPaginator;

                dialog.PrintDocument(
                    paginator,
                    "Hợp đồng " + _maHopDong);

                HienThiThongBao(
                    "Đã gửi hợp đồng tới máy in.",
                    true);
            }
            catch (SqlException)
            {
                HienThiLoiXuat(
                    "Không lấy được thông tin hợp đồng. "
                    + "Vui lòng kiểm tra kết nối SQL Server.");
            }
            catch (Exception ex)
            {
                HienThiLoiXuat(
                    "Không in được hợp đồng: "
                    + ex.Message);
            }
            finally
            {
                _dangXuLy = false;

                CapNhatNutXuat();
            }
        }

        private static FlowDocument TaoBanIn(
            DataRow row,
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
                PagePadding = new Thickness(32),
                ColumnWidth = pageWidth - 64,
                FontFamily = new FontFamily("Arial"),
                FontSize = 14
            };

            string Chuoi(string cot) =>
                LayChuoi(
                    row,
                    cot,
                    "Chưa cập nhật");

            string Ngay(string cot) =>
                DinhDangNgay(
                    row,
                    cot,
                    "Chưa cập nhật");

            string Tien(string cot) =>
                DinhDangTien(
                    row,
                    cot,
                    "Chưa cập nhật",
                    " VNĐ");

            Paragraph ThemDong(
                string text,
                bool dam = false,
                double coChu = 14,
                TextAlignment canLe = TextAlignment.Left)
            {
                var paragraph = new Paragraph(
                    new Run(text))
                {
                    FontSize = coChu,

                    FontWeight = dam
                        ? FontWeights.Bold
                        : FontWeights.Normal,

                    TextAlignment = canLe,

                    Margin = new Thickness(
                        0, 0, 0, 7)
                };

                document.Blocks.Add(paragraph);

                return paragraph;
            }

            void ThemTieuDe(string text)
            {
                Paragraph p = ThemDong(
                    text,
                    true,
                    16);

                p.Margin =
                    new Thickness(0, 16, 0, 9);

                p.KeepWithNext = true;
            }

            void ThemTieuDeNho(string text)
            {
                Paragraph p = ThemDong(
                    text,
                    true);

                p.Margin =
                    new Thickness(0, 9, 0, 7);

                p.KeepWithNext = true;
            }

            void ThemTruong(
                string nhan,
                string giaTri)
            {
                var paragraph = new Paragraph
                {
                    Margin =
                        new Thickness(0, 0, 0, 7)
                };

                paragraph.Inlines.Add(
                    new Bold(
                        new Run(nhan + ": ")));

                paragraph.Inlines.Add(
                    new Run(giaTri));

                document.Blocks.Add(paragraph);
            }

            void ThemNoiDungNhieuDong(
                string text,
                string macDinh)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    if (!string.IsNullOrWhiteSpace(macDinh))
                        ThemDong(macDinh);

                    return;
                }

                string vanBan = text
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n');

                foreach (string dong
                    in vanBan.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(dong))
                        continue;

                    ThemDong(dong.Trim()).TextAlignment =
                        TextAlignment.Justify;
                }
            }

            ThemDong(
                "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM",
                true,
                14,
                TextAlignment.Center);

            Paragraph tieuNgu = ThemDong(
                "Độc lập - Tự do - Hạnh phúc",
                true,
                14,
                TextAlignment.Center);

            tieuNgu.Margin =
                new Thickness(0, 0, 0, 24);

            ThemDong(
                "HỢP ĐỒNG THUÊ PHÒNG TRỌ",
                true,
                23,
                TextAlignment.Center);

            ThemDong(
                "Số hợp đồng: " + Chuoi("MaHopDong"),
                false,
                14,
                TextAlignment.Center);

            Paragraph ngayKy = ThemDong(
                "Ngày ký: " + Ngay("NgayKy"),
                false,
                14,
                TextAlignment.Center);

            ngayKy.Margin =
                new Thickness(0, 0, 0, 18);

            ThemTieuDe("I. THÔNG TIN CÁC BÊN");

            ThemTieuDeNho("Bên A - Bên cho thuê");

            ThemTruong(
                "Đại diện bên cho thuê",
                Chuoi("DaiDienBenChoThue"));

            ThemTruong(
                "CCCD người đại diện",
                Chuoi("CCCDBenChoThue"));

            ThemTruong(
                "Chủ trọ quản lý dãy",
                Chuoi("TenChuTro"));

            ThemTruong(
                "Số điện thoại liên hệ Chủ trọ",
                Chuoi("SoDienThoaiChuTro"));

            ThemTruong(
                "Địa chỉ liên hệ Chủ trọ",
                Chuoi("DiaChiChuTro"));

            ThemTieuDeNho("Bên B - Bên thuê");

            ThemTruong(
                "Họ và tên",
                Chuoi("KhachThue"));

            ThemTruong(
                "CCCD",
                Chuoi("CCCDKhachThue"));

            ThemTruong(
                "Số điện thoại",
                Chuoi("SoDienThoaiKhachThue"));

            ThemTruong(
                "Địa chỉ liên hệ",
                Chuoi("DiaChiKhachThue"));

            ThemTieuDe("II. THÔNG TIN PHÒNG THUÊ");

            ThemTruong(
                "Dãy trọ",
                Chuoi("TenDayTro"));

            ThemTruong(
                "Số phòng",
                Chuoi("SoPhong"));

            ThemTruong(
                "Địa chỉ phòng thuê",
                Chuoi("DiaChiDayTro"));

            ThemTieuDe("III. THỜI HẠN VÀ THANH TOÁN");

            ThemTruong(
                "Ngày bắt đầu",
                Ngay("NgayBatDau"));

            ThemTruong(
                "Ngày kết thúc",
                Ngay("NgayKetThuc"));

            ThemTruong(
                "Giá thuê",
                Tien("GiaThue") + "/tháng");

            ThemTruong(
                "Tiền đặt cọc",
                Tien("TienCoc"));

            string kyThanhToan =
                Chuoi("KyThanhToan");

            ThemTruong(
                "Ngày đến hạn thanh toán hằng tháng",
                kyThanhToan == "Chưa cập nhật"
                    ? kyThanhToan
                    : "Ngày " + kyThanhToan);

            ThemTruong(
                "Trạng thái hợp đồng",
                Chuoi("TrangThai"));

            ThemTieuDe("IV. ĐIỀU KHOẢN HỢP ĐỒNG");

            ThemNoiDungNhieuDong(
                LayChuoi(row, "DieuKhoan", ""),
                "Chưa có điều khoản hợp đồng được lưu. "
                + "Cần bổ sung và thống nhất điều khoản "
                + "trước khi sử dụng văn bản để ký kết.");

            string quyDinh =
                LayChuoi(row, "QuyDinh", "");

            if (!string.IsNullOrWhiteSpace(quyDinh))
            {
                ThemTieuDeNho(
                    "Quy định của dãy trọ");

                ThemNoiDungNhieuDong(
                    quyDinh,
                    "");
            }

            ThemTieuDe("V. XÁC NHẬN CỦA CÁC BÊN");

            ThemDong(
                "Hai bên đọc, kiểm tra thông tin và ký tên "
                + "sau khi thống nhất toàn bộ nội dung hợp đồng.");

            var bangKy = new Table
            {
                CellSpacing = 0,
                Margin =
                    new Thickness(0, 16, 0, 0)
            };

            bangKy.Columns.Add(new TableColumn
            {
                Width = new GridLength(
                    document.ColumnWidth / 2)
            });

            bangKy.Columns.Add(new TableColumn
            {
                Width = new GridLength(
                    document.ColumnWidth / 2)
            });

            TableCell TaoCotKy(
                string tieuDe,
                string hoTen)
            {
                var p = new Paragraph
                {
                    TextAlignment =
                        TextAlignment.Center,

                    Margin = new Thickness(0),

                    KeepTogether = true
                };

                p.Inlines.Add(
                    new Bold(
                        new Run(tieuDe)));

                p.Inlines.Add(
                    new LineBreak());

                p.Inlines.Add(
                    new Run("(Ký và ghi rõ họ tên)")
                    {
                        FontStyle = FontStyles.Italic
                    });

                for (int i = 0; i < 5; i++)
                {
                    p.Inlines.Add(
                        new LineBreak());
                }

                p.Inlines.Add(
                    new Bold(
                        new Run(hoTen)));

                return new TableCell(p)
                {
                    Padding =
                        new Thickness(4),

                    BorderThickness =
                        new Thickness(0)
                };
            }

            var nhomKy = new TableRowGroup();
            var dongKy = new TableRow();

            dongKy.Cells.Add(
                TaoCotKy(
                    "ĐẠI DIỆN BÊN CHO THUÊ",
                    Chuoi("DaiDienBenChoThue")));

            dongKy.Cells.Add(
                TaoCotKy(
                    "BÊN THUÊ",
                    Chuoi("KhachThue")));

            nhomKy.Rows.Add(dongKy);

            bangKy.RowGroups.Add(nhomKy);

            document.Blocks.Add(bangKy);

            Paragraph ghiChu = ThemDong(
                "Bản hợp đồng được tạo từ dữ liệu của hệ thống "
                + "quản lý nhà trọ. Cần kiểm tra thông tin, "
                + "điều khoản và chữ ký trước khi sử dụng "
                + "như văn bản hợp đồng chính thức.",
                false,
                12);

            ghiChu.FontStyle =
                FontStyles.Italic;

            ghiChu.Margin =
                new Thickness(0, 24, 0, 0);

            return document;
        }

        private void QuayLai_Click(
            object sender,
            RoutedEventArgs e)
        {
            QuayLaiRequested?.Invoke();
        }
    }
}