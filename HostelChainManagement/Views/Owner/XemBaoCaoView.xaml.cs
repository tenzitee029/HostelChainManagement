
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Owner
{
    public partial class XemBaoCaoView : UserControl
    {
        private sealed record KyBaoCao(
            string TenKy,
            decimal DoanhThu,
            int SoGiaoDich,
            decimal CongNo,
            int SoHoaDonConNo,
            int TongPhong,
            int PhongDangThue,
            decimal TyLeLapDay);

        private readonly CultureInfo _vanHoa =
            CultureInfo.GetCultureInfo("vi-VN");

        private List<KyBaoCao>? _cacKy;
        private bool _dangTai;

        public XemBaoCaoView()
        {
            InitializeComponent();

            cmbLoaiBaoCao.SelectionChanged +=
                LoaiBaoCao_SelectionChanged;

            CapNhatTieuDe();

            Loaded += XemBaoCaoView_Loaded;
        }

        private async void XemBaoCaoView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= XemBaoCaoView_Loaded;

            await TaiDanhSachDayTroAsync();

            await TaiBaoCaoAsync();
        }

        private async Task TaiDanhSachDayTroAsync()
        {
            cmbDayTro.IsEnabled = false;

            try
            {
                DataTable table =
                    await HopDongHoaDonChuTroService
                        .LayDanhSachDayTroAsync();

                cmbDayTro.Items.Clear();

                cmbDayTro.Items.Add(
                    new ComboBoxItem
                    {
                        Content = "Tất cả dãy trọ"
                    });

                foreach (DataRow row in table.Rows)
                {
                    cmbDayTro.Items.Add(
                        new ComboBoxItem
                        {
                            Content =
                                Convert.ToString(
                                    row["TenDayTro"]) ?? "",
                            Tag =
                                Convert.ToString(
                                    row["MaDayTro"])
                        });
                }

                cmbDayTro.SelectedIndex = 0;
                cmbDayTro.IsEnabled = true;
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không thể tải danh sách dãy trọ: "
                    + ex.Message;

                txtThongBao.Foreground =
                    TaoMau(220, 38, 38);
            }
        }

        private void LoaiBaoCao_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            CapNhatTieuDe();

            if (_cacKy != null)
            {
                HienThiBaoCao();
            }
        }

        private void CapNhatTieuDe()
        {
            string loaiBaoCao =
                (cmbLoaiBaoCao.SelectedItem as ComboBoxItem)?
                    .Content?.ToString() ?? "Doanh thu";

            txtTieuDeBieuDo.Text =
                "Biểu đồ báo cáo "
                + loaiBaoCao.ToLower(_vanHoa);

            txtTieuDeChiTiet.Text =
                "Thông tin báo cáo "
                + loaiBaoCao.ToLower(_vanHoa);
        }

        private async void XemBaoCao_Click(
            object sender,
            RoutedEventArgs e)
        {
            await TaiBaoCaoAsync();
        }

        private async Task TaiBaoCaoAsync()
        {
            if (_dangTai)
                return;

            _dangTai = true;
            btnXemBaoCao.IsEnabled = false;

            txtThongBao.Text =
                "Đang tải dữ liệu báo cáo từ SQL Server...";

            txtThongBao.Foreground =
                TaoMau(107, 114, 128);

            dgvBaoCao.ItemsSource = null;
            itemsBieuDo.Items.Clear();

            txtEmptyBaoCao.Visibility = Visibility.Visible;
            txtEmptyBieuDo.Visibility = Visibility.Visible;

            try
            {
                string? maDayTro =
                    (cmbDayTro.SelectedItem as ComboBoxItem)?
                    .Tag as string;

                DataTable table =
                    await BaoCaoChuTroService.LayBaoCaoAsync(
                        maDayTro,
                        cmbKieuKy.SelectedIndex,
                        cmbThoiGian.SelectedIndex);

                var danhSach = new List<KyBaoCao>();

                foreach (DataRow row in table.Rows)
                {
                    danhSach.Add(
                        new KyBaoCao(
                            Convert.ToString(row["TenKy"]) ?? "",
                            Convert.ToDecimal(row["DoanhThu"]),
                            Convert.ToInt32(row["SoGiaoDich"]),
                            Convert.ToDecimal(row["CongNo"]),
                            Convert.ToInt32(row["SoHoaDonConNo"]),
                            Convert.ToInt32(row["TongPhong"]),
                            Convert.ToInt32(row["PhongDangThue"]),
                            Convert.ToDecimal(row["TyLeLapDay"])));
                }

                _cacKy = danhSach;

                HienThiBaoCao();
            }
            catch (SqlException ex)
            {
                HienThiLoi(
                    "Không thể tải báo cáo từ SQL Server: "
                    + ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                HienThiLoi(ex.Message);
            }
            catch (Exception ex)
            {
                HienThiLoi(
                    "Đã xảy ra lỗi khi tính báo cáo: "
                    + ex.Message);
            }
            finally
            {
                _dangTai = false;
                btnXemBaoCao.IsEnabled = true;
            }
        }

        private void HienThiBaoCao()
        {
            if (_cacKy == null || _cacKy.Count == 0)
            {
                txtTongDoanhThu.Text = "—";
                txtTongCongNo.Text = "—";
                txtTyLeLapDay.Text = "—";

                dgvBaoCao.ItemsSource = null;
                itemsBieuDo.Items.Clear();

                txtEmptyBaoCao.Text =
                    "Không có dữ liệu báo cáo trong kỳ này.";

                txtEmptyBieuDo.Text =
                    "Không có dữ liệu báo cáo trong kỳ này.";

                txtEmptyBaoCao.Visibility = Visibility.Visible;
                txtEmptyBieuDo.Visibility = Visibility.Visible;

                return;
            }

            decimal tongDaThu =
                _cacKy.Sum(x => x.DoanhThu);

            KyBaoCao kyCuoi = _cacKy[^1];

            txtTongDoanhThu.Text =
                DinhDangTien(tongDaThu);

            txtTongCongNo.Text =
                DinhDangTien(kyCuoi.CongNo);

            txtTyLeLapDay.Text = kyCuoi.TongPhong > 0
                ? kyCuoi.TyLeLapDay.ToString(
                    "N2",
                    _vanHoa) + "%"
                : "—";

            int loaiBaoCao = cmbLoaiBaoCao.SelectedIndex;

            DataTable bangChiTiet =
                TaoBangChiTiet(loaiBaoCao);

            bool coDuLieu = _cacKy.Any(
                x => LayGiaTriBieuDo(
                    x,
                    loaiBaoCao) > 0);

            if (coDuLieu)
            {
                dgvBaoCao.ItemsSource =
                    bangChiTiet.DefaultView;

                txtEmptyBaoCao.Visibility =
                    Visibility.Collapsed;
            }
            else
            {
                dgvBaoCao.ItemsSource = null;

                txtEmptyBaoCao.Text =
                    "Không có dữ liệu báo cáo trong kỳ này.";

                txtEmptyBaoCao.Visibility =
                    Visibility.Visible;
            }

            VeBieuDo(loaiBaoCao);

            string thongBao =
                "Doanh thu là tổng thanh toán trong các kỳ đã chọn. "
                + "Công nợ và tỷ lệ lấp đầy là số chốt cuối kỳ gần nhất. "
                + "Công nợ bao gồm khoản chưa đến hạn, không chỉ nợ quá hạn.";

            if (!coDuLieu)
            {
                thongBao =
                    "Không có dữ liệu báo cáo trong kỳ này. "
                    + thongBao;
            }

            if (loaiBaoCao == 1)
            {
                thongBao +=
                    " Tỷ lệ lấp đầy được ước tính từ hợp đồng "
                    + "tại ngày chốt kỳ. CSDL hiện chưa lưu "
                    + "lịch sử tạo và ngừng sử dụng từng phòng.";
            }

            txtThongBao.Text = thongBao;
            txtThongBao.Foreground =
                TaoMau(107, 114, 128);
        }

        private DataTable TaoBangChiTiet(int loaiBaoCao)
        {
            var table = new DataTable();

            table.Columns.Add("Kỳ", typeof(string));

            if (loaiBaoCao == 0)
            {
                table.Columns.Add(
                    "Doanh thu đã thu",
                    typeof(string));

                table.Columns.Add(
                    "Số giao dịch",
                    typeof(string));
            }
            else if (loaiBaoCao == 1)
            {
                table.Columns.Add(
                    "Phòng có khách",
                    typeof(string));

                table.Columns.Add(
                    "Tổng phòng",
                    typeof(string));

                table.Columns.Add(
                    "Tỷ lệ lấp đầy",
                    typeof(string));
            }
            else
            {
                table.Columns.Add(
                    "Hóa đơn còn phải thu",
                    typeof(string));

                table.Columns.Add(
                    "Công nợ tại cuối kỳ",
                    typeof(string));
            }

            foreach (KyBaoCao ky in _cacKy!)
            {
                if (loaiBaoCao == 0)
                {
                    table.Rows.Add(
                        ky.TenKy,
                        DinhDangTien(ky.DoanhThu),
                        ky.SoGiaoDich.ToString(_vanHoa));
                }
                else if (loaiBaoCao == 1)
                {
                    table.Rows.Add(
                        ky.TenKy,
                        ky.PhongDangThue.ToString(_vanHoa),
                        ky.TongPhong.ToString(_vanHoa),
                        ky.TongPhong > 0
                            ? ky.TyLeLapDay.ToString(
                                "N2",
                                _vanHoa) + "%"
                            : "—");
                }
                else
                {
                    table.Rows.Add(
                        ky.TenKy,
                        ky.SoHoaDonConNo.ToString(_vanHoa),
                        DinhDangTien(ky.CongNo));
                }
            }

            return table;
        }

        private static decimal LayGiaTriBieuDo(
            KyBaoCao ky,
            int loaiBaoCao)
        {
            return loaiBaoCao switch
            {
                0 => ky.DoanhThu,
                1 => ky.TyLeLapDay,
                2 => ky.CongNo,
                _ => 0
            };
        }

        private void VeBieuDo(int loaiBaoCao)
        {
            itemsBieuDo.Items.Clear();

            if (_cacKy == null || _cacKy.Count == 0)
            {
                txtEmptyBieuDo.Visibility =
                    Visibility.Visible;

                return;
            }

            decimal lonNhat = _cacKy.Max(
                x => LayGiaTriBieuDo(
                    x,
                    loaiBaoCao));

            if (lonNhat <= 0)
            {
                txtEmptyBieuDo.Text =
                    "Không có dữ liệu biểu đồ trong kỳ này.";

                txtEmptyBieuDo.Visibility =
                    Visibility.Visible;

                return;
            }

            txtEmptyBieuDo.Visibility = Visibility.Collapsed;

            var bieuDo = new UniformGrid
            {
                Columns = _cacKy.Count,
                Rows = 1,
                HorizontalAlignment =
                    HorizontalAlignment.Stretch,
                VerticalAlignment =
                    VerticalAlignment.Stretch
            };

            foreach (KyBaoCao ky in _cacKy)
            {
                decimal giaTri =
                    LayGiaTriBieuDo(
                        ky,
                        loaiBaoCao);

                double chieuCao = giaTri <= 0
                    ? 0
                    : Math.Max(
                        4,
                        (double)(
                            giaTri / lonNhat) * 106);

                var cot = new Grid
                {
                    Height = 165,
                    Margin = new Thickness(3, 0, 3, 0)
                };

                cot.RowDefinitions.Add(
                    new RowDefinition
                    {
                        Height = new GridLength(28)
                    });

                cot.RowDefinitions.Add(
                    new RowDefinition
                    {
                        Height = new GridLength(112)
                    });

                cot.RowDefinitions.Add(
                    new RowDefinition
                    {
                        Height = new GridLength(25)
                    });

                var nhanGiaTri = new TextBlock
                {
                    Text = DinhDangNgan(
                        giaTri,
                        loaiBaoCao),
                    FontSize = 10,
                    Foreground =
                        TaoMau(55, 65, 81),
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment =
                        VerticalAlignment.Bottom,
                    TextTrimming =
                        TextTrimming.CharacterEllipsis,
                    ToolTip = loaiBaoCao == 1
                        ? giaTri.ToString(
                            "N2",
                            _vanHoa) + "%"
                        : DinhDangTien(giaTri)
                };

                Grid.SetRow(nhanGiaTri, 0);
                cot.Children.Add(nhanGiaTri);

                var thanhGiaTri = new Border
                {
                    Height = chieuCao,
                    Width = 30,
                    MaxWidth = 30,
                    Background =
                        TaoMau(37, 99, 235),
                    CornerRadius =
                        new CornerRadius(3, 3, 0, 0),
                    HorizontalAlignment =
                        HorizontalAlignment.Center,
                    VerticalAlignment =
                        VerticalAlignment.Bottom
                };

                Grid.SetRow(thanhGiaTri, 1);
                cot.Children.Add(thanhGiaTri);

                var nhanKy = new TextBlock
                {
                    Text = ky.TenKy,
                    FontSize = 10,
                    Foreground =
                        TaoMau(107, 114, 128),
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment =
                        VerticalAlignment.Center,
                    TextTrimming =
                        TextTrimming.CharacterEllipsis
                };

                Grid.SetRow(nhanKy, 2);
                cot.Children.Add(nhanKy);

                bieuDo.Children.Add(cot);
            }

            itemsBieuDo.Items.Add(bieuDo);
        }

        private string DinhDangNgan(
            decimal giaTri,
            int loaiBaoCao)
        {
            if (loaiBaoCao == 1)
            {
                return giaTri.ToString(
                    "0.#",
                    _vanHoa) + "%";
            }

            if (giaTri >= 1000000000)
            {
                return (giaTri / 1000000000)
                    .ToString("0.#", _vanHoa) + " tỷ";
            }

            if (giaTri >= 1000000)
            {
                return (giaTri / 1000000)
                    .ToString("0.#", _vanHoa) + " tr";
            }

            if (giaTri >= 1000)
            {
                return (giaTri / 1000)
                    .ToString("0.#", _vanHoa) + " k";
            }

            return giaTri.ToString("N0", _vanHoa);
        }

        private string DinhDangTien(decimal giaTri)
        {
            return giaTri.ToString(
                "N0",
                _vanHoa) + " đ";
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
            _cacKy = null;

            txtTongDoanhThu.Text = "—";
            txtTongCongNo.Text = "—";
            txtTyLeLapDay.Text = "—";

            dgvBaoCao.ItemsSource = null;
            itemsBieuDo.Items.Clear();

            txtEmptyBaoCao.Text = noiDung;
            txtEmptyBieuDo.Text = noiDung;

            txtEmptyBaoCao.Visibility =
                Visibility.Visible;

            txtEmptyBieuDo.Visibility =
                Visibility.Visible;

            txtThongBao.Text = noiDung;
            txtThongBao.Foreground =
                TaoMau(220, 38, 38);
        }
    }
}
