
using System;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Owner
{
    public partial class ChiTietHopDongView : UserControl
    {
        private readonly string _maHopDong;
        private DataRow? _chiTiet;

        public event Action? QuayLaiRequested;

        public ChiTietHopDongView(string maHopDong)
        {
            InitializeComponent();

            _maHopDong = maHopDong;

            btnTaiPdf.IsEnabled = false;

            btnTaiPdf.ToolTip =
                "In hợp đồng hoặc chọn Microsoft Print to PDF để lưu PDF.";

            btnTaiPdf.Click += TaiPdf_Click;

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
            btnTaiPdf.IsEnabled = false;

            txtThongBao.Text = "Đang tải chi tiết hợp đồng...";
            txtThongBao.Visibility = Visibility.Visible;

            try
            {
                DataTable table =
                    await ChiTietHopDongChuTroService
                        .LayChiTietAsync(_maHopDong);

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

                string trangThai = txtTrangThai.Text;

                if (trangThai == "Hiệu lực")
                {
                    txtTrangThai.Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(21, 128, 61));
                }
                else if (trangThai == "Đã thanh lý")
                {
                    txtTrangThai.Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(107, 114, 128));
                }
                else
                {
                    txtTrangThai.Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(180, 83, 9));
                }

                _chiTiet = row;

                btnTaiPdf.IsEnabled = true;

                txtThongBao.Visibility = Visibility.Collapsed;
            }
            catch (InvalidOperationException ex)
            {
                HienThiLoi(ex.Message);
            }
            catch (SqlException ex)
            {
                HienThiLoi(
                    "Không thể tải hợp đồng từ SQL Server: "
                    + ex.Message);
            }
            catch (Exception ex)
            {
                HienThiLoi(
                    "Đã xảy ra lỗi khi tải chi tiết hợp đồng: "
                    + ex.Message);
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
            if (!row.Table.Columns.Contains(tenCot))
                return "—";

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
            if (!row.Table.Columns.Contains(tenCot))
                return "—";

            if (row[tenCot] == DBNull.Value)
                return "—";

            decimal soTien =
                Convert.ToDecimal(row[tenCot]);

            return soTien.ToString(
                "N0",
                CultureInfo.GetCultureInfo("vi-VN")) + " đ";
        }

        private void TaiPdf_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_chiTiet == null)
            {
                MessageBox.Show(
                    "Chưa có dữ liệu hợp đồng để in.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                var printDialog = new PrintDialog();

                if (printDialog.ShowDialog() != true)
                    return;

                FlowDocument taiLieu =
                    TaoTaiLieuIn(_chiTiet);

                double chieuRong =
                    printDialog.PrintableAreaWidth > 200
                        ? printDialog.PrintableAreaWidth
                        : 793;

                double chieuCao =
                    printDialog.PrintableAreaHeight > 200
                        ? printDialog.PrintableAreaHeight
                        : 1122;

                taiLieu.PageWidth = chieuRong;
                taiLieu.PageHeight = chieuCao;
                taiLieu.PagePadding = new Thickness(38);
                taiLieu.ColumnWidth = chieuRong - 76;
                taiLieu.ColumnGap = 0;

                var paginator =
                    ((IDocumentPaginatorSource)taiLieu)
                        .DocumentPaginator;

                printDialog.PrintDocument(
                    paginator,
                    "Thông tin hợp đồng " + _maHopDong);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không thể thực hiện lệnh in: "
                    + ex.Message,
                    "Lỗi in hợp đồng",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static FlowDocument TaoTaiLieuIn(
            DataRow hopDong)
        {
            var taiLieu = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = Brushes.Black,
                TextAlignment = TextAlignment.Left
            };

            var tieuDe = new Paragraph(
                new Run("THÔNG TIN HỢP ĐỒNG THUÊ PHÒNG"))
            {
                FontSize = 19,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 5, 0, 8)
            };

            taiLieu.Blocks.Add(tieuDe);

            var phuDe = new Paragraph(
                new Run("HỆ THỐNG QUẢN LÝ NHÀ TRỌ"))
            {
                FontSize = 11,
                Foreground = Brushes.DimGray,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 24)
            };

            taiLieu.Blocks.Add(phuDe);

            var bang = new Table
            {
                CellSpacing = 0
            };

            bang.Columns.Add(
                new TableColumn
                {
                    Width = new GridLength(185)
                });

            bang.Columns.Add(
                new TableColumn
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });

            var nhomDong = new TableRowGroup();

            ThemDong(
                nhomDong,
                "Mã hợp đồng",
                LayChuoi(hopDong, "MaHopDong"));

            ThemDong(
                nhomDong,
                "Trạng thái",
                LayChuoi(hopDong, "TrangThai"));

            ThemDong(
                nhomDong,
                "Khách thuê",
                LayChuoi(hopDong, "KhachThue"));

            ThemDong(
                nhomDong,
                "Dãy trọ",
                LayChuoi(hopDong, "TenDayTro"));

            ThemDong(
                nhomDong,
                "Số phòng",
                LayChuoi(hopDong, "SoPhong"));

            ThemDong(
                nhomDong,
                "Giá thuê",
                DinhDangTien(hopDong, "GiaThue"));

            ThemDong(
                nhomDong,
                "Ngày bắt đầu",
                DinhDangNgay(hopDong, "NgayBatDau"));

            ThemDong(
                nhomDong,
                "Ngày kết thúc",
                DinhDangNgay(hopDong, "NgayKetThuc"));

            bang.RowGroups.Add(nhomDong);

            taiLieu.Blocks.Add(bang);

            var ngayIn = new Paragraph(
                new Run(
                    "Ngày in: "
                    + DateTime.Now.ToString("dd/MM/yyyy HH:mm")))
            {
                TextAlignment = TextAlignment.Right,
                FontSize = 11,
                Margin = new Thickness(0, 25, 0, 15)
            };

            taiLieu.Blocks.Add(ngayIn);

            var ghiChu = new Paragraph(
                new Run(
                    "Đây là bản tóm tắt thông tin hợp đồng "
                    + "được trích xuất từ hệ thống quản lý nhà trọ. "
                    + "Tài liệu không thay thế hợp đồng gốc có chữ ký."))
            {
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 15, 0, 0)
            };

            taiLieu.Blocks.Add(ghiChu);

            return taiLieu;
        }

        private static void ThemDong(
            TableRowGroup nhomDong,
            string nhan,
            string giaTri)
        {
            var dong = new TableRow();

            var vanBanNhan = new Paragraph(
                new Run(nhan))
            {
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0)
            };

            var vanBanGiaTri = new Paragraph(
                new Run(giaTri))
            {
                Margin = new Thickness(0)
            };

            var oNhan = new TableCell(vanBanNhan)
            {
                Padding = new Thickness(10),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var oGiaTri = new TableCell(vanBanGiaTri)
            {
                Padding = new Thickness(10),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            dong.Cells.Add(oNhan);
            dong.Cells.Add(oGiaTri);

            nhomDong.Rows.Add(dong);
        }

        private void HienThiLoi(string noiDung)
        {
            _chiTiet = null;
            btnTaiPdf.IsEnabled = false;

            txtThongBao.Text = noiDung;
            txtThongBao.Foreground =
                new SolidColorBrush(
                    Color.FromRgb(220, 38, 38));

            txtThongBao.Visibility = Visibility.Visible;
        }

        private void QuayLai_Click(
            object sender,
            RoutedEventArgs e)
        {
            QuayLaiRequested?.Invoke();
        }
    }
}
