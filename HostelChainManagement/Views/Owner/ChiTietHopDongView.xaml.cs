using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Owner
{
    public partial class ChiTietHopDongView : UserControl
    {
        private readonly string _maHopDong;
        private DataRow? _chiTiet;
        private bool _dangTaiPdf;

        public event Action? QuayLaiRequested;

        public ChiTietHopDongView(string maHopDong)
        {
            InitializeComponent();

            _maHopDong = maHopDong;

            btnTaiPdf.IsEnabled = false;
            btnTaiPdf.ToolTip =
                "Tải hợp đồng thuê phòng dưới dạng PDF.";

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

        private async void TaiPdf_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_dangTaiPdf)
                return;

            if (_chiTiet == null)
            {
                MessageBox.Show(
                    "Chưa có dữ liệu hợp đồng để tải PDF.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Lưu bản PDF hợp đồng thuê",
                FileName = TaoTenFileMacDinh(_maHopDong),
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
                return;

            _dangTaiPdf = true;
            btnTaiPdf.IsEnabled = false;

            txtThongBao.Text = "Đang tạo bản PDF...";
            txtThongBao.Foreground =
                new SolidColorBrush(
                    Color.FromRgb(107, 114, 128));

            txtThongBao.Visibility = Visibility.Visible;

            try
            {
                byte[] pdf =
                    await HopDongChuTroPdfService
                        .TaoPdfAsync(_maHopDong);

                if (!IsLoaded)
                    return;

                await File.WriteAllBytesAsync(
                    dialog.FileName,
                    pdf);

                if (!IsLoaded)
                    return;

                txtThongBao.Text =
                    "Đã lưu bản PDF tại: " + dialog.FileName;

                txtThongBao.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(21, 128, 61));

                MessageBox.Show(
                    window,
                    "Đã lưu bản PDF thành công.\n\n"
                    + dialog.FileName,
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex)
            {
                HienThiLoiTaiPdf(ex.Message);
            }
            catch (SqlException)
            {
                HienThiLoiTaiPdf(
                    "Không lấy được thông tin hợp đồng. "
                    + "Vui lòng kiểm tra kết nối SQL Server.");
            }
            catch (UnauthorizedAccessException)
            {
                HienThiLoiTaiPdf(
                    "Không có quyền lưu file tại vị trí đã chọn. "
                    + "Vui lòng chọn thư mục khác.");
            }
            catch (IOException)
            {
                HienThiLoiTaiPdf(
                    "Không thể lưu file PDF. "
                    + "File có thể đang được mở hoặc thư mục "
                    + "không còn truy cập được.");
            }
            catch (Exception ex)
            {
                HienThiLoiTaiPdf(
                    "Không xuất được PDF: " + ex.Message);
            }
            finally
            {
                _dangTaiPdf = false;
                btnTaiPdf.IsEnabled = _chiTiet != null;
            }
        }

        private void HienThiLoiTaiPdf(string noiDung)
        {
            txtThongBao.Text = noiDung;

            txtThongBao.Foreground =
                new SolidColorBrush(
                    Color.FromRgb(220, 38, 38));

            txtThongBao.Visibility = Visibility.Visible;

            MessageBox.Show(
                Window.GetWindow(this),
                noiDung,
                "Tải PDF",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private static string TaoTenFileMacDinh(
            string maHopDong)
        {
            string tenFile = "HopDong_" + maHopDong;

            foreach (char kyTu in Path.GetInvalidFileNameChars())
            {
                tenFile = tenFile.Replace(kyTu, '_');
            }

            return tenFile + ".pdf";
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