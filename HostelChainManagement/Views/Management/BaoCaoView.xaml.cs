using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Xml;
using Microsoft.Win32;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class BaoCaoView : UserControl
    {
        private bool _khoiTao;
        private bool _dangXuLy;
        private BanBaoCao? _baoCao;

        public sealed class ChiTieu
        {
            public string NoiDung { get; set; } = "";
            public decimal GiaTri { get; set; }
            public string DonVi { get; set; } = "";

            public string HienThi =>
                GiaTri.ToString(
                    "N0", CultureInfo.GetCultureInfo("vi-VN"))
                + " " + DonVi;
        }

        public sealed class BanBaoCao
        {
            public string NguoiLap { get; set; } = "";
            public string DayTro { get; set; } = "";
            public string Ky { get; set; } = "";
            public string NoiDung { get; set; } = "";
            public DateTime NgayLap { get; set; }
            public string GhiChu { get; set; } = "";
            public List<ChiTieu> ChiTieu { get; set; } = new();
        }

        public BaoCaoView()
        {
            InitializeComponent();

            _khoiTao = true;

            for (int i = 1; i <= 12; i++)
                cboThang.Items.Add(i);

            cboThang.SelectedItem = DateTime.Today.Month;
            txtNam.Text = DateTime.Today.Year.ToString();
            cboNoiDung.SelectedIndex = 0;

            _khoiTao = false;
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (cboDay.ItemsSource != null || _dangXuLy)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền xem báo cáo.";
                btnTongHop.IsEnabled = false;
                return;
            }

            _dangXuLy = true;
            btnTongHop.IsEnabled = false;

            try
            {
                using var connection = Database.CreateConnection();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT d.MaDayTro, d.TenDayTro
                    FROM dbo.DayTro d
                    WHERE EXISTS (
                        SELECT 1 FROM dbo.NhanVien nv
                        WHERE nv.MaDayTro = d.MaDayTro
                          AND nv.MaNguoiDung = @QuanLy
                          AND nv.ChucVu = N'Quản lý'
                          AND nv.TrangThaiLamViec = N'Đang làm'
                    )
                    ORDER BY d.TenDayTro;";

                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;

                await connection.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();

                var table = new DataTable();
                table.Load(reader);

                if (table.Rows.Count == 0)
                {
                    txtThongBao.Text = "Bạn chưa được phân công dãy trọ.";
                    return;
                }

                var tatCa = table.NewRow();
                tatCa["MaDayTro"] = "";
                tatCa["TenDayTro"] = "Tất cả dãy phụ trách";
                table.Rows.InsertAt(tatCa, 0);

                cboDay.ItemsSource = table.DefaultView;
                cboDay.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                txtThongBao.Text = "Không tải được dãy trọ: " + ex.Message;
            }
            finally
            {
                _dangXuLy = false;
                btnTongHop.IsEnabled = cboDay.ItemsSource != null;
            }
        }

        private void DieuKien_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            XoaKetQua();
        }

        private void Nam_Changed(
            object sender, TextChangedEventArgs e)
        {
            XoaKetQua();
        }

        private void LoaiKy_Changed(
            object sender, RoutedEventArgs e)
        {
            if (cboThang != null)
                cboThang.IsEnabled = rbThang.IsChecked == true;

            XoaKetQua();
        }

        private void XoaKetQua()
        {
            if (_khoiTao || dgBaoCao == null)
                return;

            _baoCao = null;
            dgBaoCao.ItemsSource = null;
            txtNguoiLap.Clear();
            txtThoiGian.Clear();
            txtNoiDung.Clear();

            btnLuu.IsEnabled = false;
            btnXuat.IsEnabled = false;
        }

        private async void TongHop_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangXuLy)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
                return;

            if (cboDay.SelectedItem is not DataRowView day ||
                !int.TryParse(txtNam.Text, out int nam) ||
                nam < 2000 || nam > 9998 ||
                cboNoiDung.SelectedIndex < 0)
            {
                txtThongBao.Text = "Chọn dãy, nội dung và năm từ 2000–9998.";
                return;
            }

            bool theoThang = rbThang.IsChecked == true;
            int thang = cboThang.SelectedItem is int t ? t : 0;

            if (theoThang && (thang < 1 || thang > 12))
            {
                txtThongBao.Text = "Vui lòng chọn tháng.";
                return;
            }

            DateTime tuNgay = theoThang
                ? new DateTime(nam, thang, 1)
                : new DateTime(nam, 1, 1);

            DateTime denNgay = theoThang
                ? tuNgay.AddMonths(1)
                : tuNgay.AddYears(1);

            string maDay = Convert.ToString(day["MaDayTro"]) ?? "";
            string tenDay = Convert.ToString(day["TenDayTro"]) ?? "";
            int loai = cboNoiDung.SelectedIndex;

            string noiDung =
                Convert.ToString(
                    (cboNoiDung.SelectedItem as ComboBoxItem)?.Content)
                ?? "";

            string nguoiLap = user.HoTen;
            string ky = theoThang ? $"{thang:00}/{nam}" : nam.ToString();

            XoaKetQua();
            _dangXuLy = true;

            // Không đổi điều kiện trong lúc đang tổng hợp.
            cboDay.IsEnabled = false;
            rbThang.IsEnabled = false;
            rbNam.IsEnabled = false;
            cboThang.IsEnabled = false;
            txtNam.IsEnabled = false;
            cboNoiDung.IsEnabled = false;
            btnTongHop.IsEnabled = false;

            txtThongBao.Text = "Đang tổng hợp dữ liệu...";

            try
            {
                using var connection = Database.CreateConnection();
                await connection.OpenAsync();

                using var transaction =
                    connection.BeginTransaction(IsolationLevel.Serializable);

                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                command.CommandText = @"
                    DECLARE @Phong TABLE (
                        MaPhong VARCHAR(20) PRIMARY KEY,
                        TrangThai NVARCHAR(30)
                    );

                    INSERT INTO @Phong
                    SELECT p.MaPhong, p.TrangThai
                    FROM dbo.PhongTro p
                    WHERE (@MaDay = '' OR p.MaDayTro = @MaDay)
                      AND EXISTS (
                          SELECT 1 FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @QuanLy
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      );

                    IF @MaDay <> '' AND NOT EXISTS (
                        SELECT 1 FROM dbo.NhanVien nv
                        WHERE nv.MaDayTro = @MaDay
                          AND nv.MaNguoiDung = @QuanLy
                          AND nv.ChucVu = N'Quản lý'
                          AND nv.TrangThaiLamViec = N'Đang làm'
                    )
                        THROW 50001, N'Bạn không còn quyền với dãy đã chọn.', 1;

                    SELECT
                        (
                            SELECT COALESCE(SUM(tt.SoTien), 0)
                            FROM dbo.ThanhToan tt
                            JOIN dbo.HoaDon hd ON hd.MaHoaDon = tt.MaHoaDon
                            JOIN @Phong p ON p.MaPhong = hd.MaPhong
                            WHERE tt.NgayThanhToan >= @TuNgay
                              AND tt.NgayThanhToan < @DenNgay
                        ) AS ThucThu,

                        (
                            SELECT COUNT(*)
                            FROM dbo.ThanhToan tt
                            JOIN dbo.HoaDon hd ON hd.MaHoaDon = tt.MaHoaDon
                            JOIN @Phong p ON p.MaPhong = hd.MaPhong
                            WHERE tt.NgayThanhToan >= @TuNgay
                              AND tt.NgayThanhToan < @DenNgay
                        ) AS SoLanThu,

                        (
                            SELECT COUNT(*)
                            FROM dbo.HoaDon hd
                            JOIN @Phong p ON p.MaPhong = hd.MaPhong
                            WHERE hd.Nam = @Nam
                              AND (@TheoThang = 0 OR hd.Thang = @Thang)
                        ) AS SoHoaDon,

                        (
                            SELECT COALESCE(SUM(hd.TongTien), 0)
                            FROM dbo.HoaDon hd
                            JOIN @Phong p ON p.MaPhong = hd.MaPhong
                            WHERE hd.Nam = @Nam
                              AND (@TheoThang = 0 OR hd.Thang = @Thang)
                        ) AS TongHoaDon,

                        (
                            SELECT COALESCE(SUM(
                                CASE WHEN hd.TongTien > COALESCE(tt.DaTra, 0)
                                     THEN hd.TongTien - COALESCE(tt.DaTra, 0)
                                     ELSE 0 END
                            ), 0)
                            FROM dbo.HoaDon hd
                            JOIN @Phong p ON p.MaPhong = hd.MaPhong
                            OUTER APPLY (
                                SELECT SUM(t.SoTien) AS DaTra
                                FROM dbo.ThanhToan t
                                WHERE t.MaHoaDon = hd.MaHoaDon
                            ) tt
                            WHERE hd.Nam = @Nam
                              AND (@TheoThang = 0 OR hd.Thang = @Thang)
                        ) AS ConNo,

                        (SELECT COUNT(*) FROM @Phong) AS TongPhong,
                        (SELECT COUNT(*) FROM @Phong
                         WHERE TrangThai = N'Trống') AS PhongTrong,
                        (SELECT COUNT(*) FROM @Phong
                         WHERE TrangThai = N'Đang thuê') AS DangThue,
                        (SELECT COUNT(*) FROM @Phong
                         WHERE TrangThai = N'Đang sửa chữa') AS DangSua,

                        (
                            SELECT COUNT(*)
                            FROM dbo.PhieuSuCo s
                            JOIN @Phong p ON p.MaPhong = s.MaPhong
                            WHERE s.NgayGui >= @TuNgay AND s.NgayGui < @DenNgay
                        ) AS TongSuCo,

                        (
                            SELECT COUNT(*)
                            FROM dbo.PhieuSuCo s
                            JOIN @Phong p ON p.MaPhong = s.MaPhong
                            WHERE s.NgayGui >= @TuNgay AND s.NgayGui < @DenNgay
                              AND s.TrangThai = N'Đã hoàn thành'
                        ) AS HoanThanh,

                        (
                            SELECT COUNT(*)
                            FROM dbo.PhieuSuCo s
                            JOIN @Phong p ON p.MaPhong = s.MaPhong
                            WHERE s.NgayGui >= @TuNgay AND s.NgayGui < @DenNgay
                              AND s.TrangThai = N'Từ chối'
                        ) AS TuChoi;";

                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;
                command.Parameters.Add(
                    "@MaDay", SqlDbType.VarChar, 20).Value = maDay;
                command.Parameters.Add(
                    "@Nam", SqlDbType.Int).Value = nam;
                command.Parameters.Add(
                    "@Thang", SqlDbType.Int).Value = thang;
                command.Parameters.Add(
                    "@TheoThang", SqlDbType.Bit).Value = theoThang;
                command.Parameters.Add(
                    "@TuNgay", SqlDbType.Date).Value = tuNgay;
                command.Parameters.Add(
                    "@DenNgay", SqlDbType.Date).Value = denNgay;

                var table = new DataTable();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    table.Load(reader);
                }

                transaction.Commit();

                var r = table.Rows[0];

                decimal V(string cot) => Convert.ToDecimal(r[cot]);

                var chiTieu = new List<ChiTieu>();

                void Them(string ten, decimal giaTri, string donVi)
                {
                    chiTieu.Add(new ChiTieu
                    {
                        NoiDung = ten,
                        GiaTri = giaTri,
                        DonVi = donVi
                    });
                }

                if (loai == 0)
                {
                    Them("Tiền thực nhận trong kỳ", V("ThucThu"), "VNĐ");
                    Them("Số lần ghi nhận thanh toán", V("SoLanThu"), "lần");
                }
                else if (loai == 1)
                {
                    Them("Số hóa đơn của kỳ", V("SoHoaDon"), "hóa đơn");
                    Them("Tổng tiền hóa đơn của kỳ", V("TongHoaDon"), "VNĐ");
                    Them("Nợ còn lại của hóa đơn kỳ này, tính hiện tại",
                        V("ConNo"), "VNĐ");
                }
                else if (loai == 2)
                {
                    Them("Tổng phòng hiện tại", V("TongPhong"), "phòng");
                    Them("Phòng trống hiện tại", V("PhongTrong"), "phòng");
                    Them("Phòng đang thuê hiện tại", V("DangThue"), "phòng");
                    Them("Phòng đang sửa chữa hiện tại", V("DangSua"), "phòng");
                }
                else
                {
                    Them("Yêu cầu gửi trong kỳ", V("TongSuCo"), "yêu cầu");
                    Them("Hiện đã hoàn thành", V("HoanThanh"), "yêu cầu");
                    Them("Hiện đã từ chối", V("TuChoi"), "yêu cầu");
                    Them("Hiện chưa hoàn thành",
                        V("TongSuCo") - V("HoanThanh") - V("TuChoi"),
                        "yêu cầu");
                }

                _baoCao = new BanBaoCao
                {
                    NguoiLap = nguoiLap,
                    DayTro = tenDay,
                    Ky = ky,
                    NoiDung = noiDung,
                    NgayLap = DateTime.Now,
                    ChiTieu = chiTieu
                };

                dgBaoCao.ItemsSource = chiTieu;
                txtNguoiLap.Text = nguoiLap;
                txtThoiGian.Text = ky;
                txtNoiDung.Text = noiDung;

                btnLuu.IsEnabled = true;
                btnXuat.IsEnabled = true;

                txtThongBao.Text =
                    "Đã tổng hợp. Số liệu là ảnh chụp tại thời điểm lập báo cáo.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text = "Không tổng hợp được: " + ex.Message;
            }
            finally
            {
                _dangXuLy = false;
                cboDay.IsEnabled = true;
                rbThang.IsEnabled = true;
                rbNam.IsEnabled = true;
                cboThang.IsEnabled = rbThang.IsChecked == true;
                txtNam.IsEnabled = true;
                cboNoiDung.IsEnabled = true;
                btnTongHop.IsEnabled = true;
            }
        }

        private void Luu_Click(object sender, RoutedEventArgs e)
        {
            if (_baoCao == null)
                return;

            var dialog = new SaveFileDialog
            {
                Title = "Lưu báo cáo",
                Filter = "Báo cáo JSON (*.json)|*.json",
                FileName = "BaoCao_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                DefaultExt = ".json",
                AddExtension = true
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                _baoCao.GhiChu = txtGhiChu.Text.Trim();

                File.WriteAllText(
                    dialog.FileName,
                    JsonSerializer.Serialize(
                        _baoCao,
                        new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));

                txtThongBao.Text = "Đã lưu báo cáo.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text = "Không lưu được: " + ex.Message;
            }
        }

        private void Xuat_Click(object sender, RoutedEventArgs e)
        {
            if (_baoCao == null)
                return;

            var menu = new ContextMenu();
            var excel = new MenuItem { Header = "Xuất Excel (.xlsx)" };
            var pdf = new MenuItem { Header = "In / Xuất PDF" };

            excel.Click += (_, _) => XuatExcel();
            pdf.Click += (_, _) => InBaoCao();

            menu.Items.Add(excel);
            menu.Items.Add(pdf);
            menu.PlacementTarget = btnXuat;
            menu.IsOpen = true;
        }

        private void XuatExcel()
        {
            if (_baoCao == null)
                return;

            var dialog = new SaveFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = "BaoCao_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                DefaultExt = ".xlsx",
                AddExtension = true
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                _baoCao.GhiChu = txtGhiChu.Text.Trim();

                using var file = File.Create(dialog.FileName);
                using var zip = new ZipArchive(file, ZipArchiveMode.Create);

                GhiXml(zip, "[Content_Types].xml",
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
                      <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
                      <Default Extension=""xml"" ContentType=""application/xml""/>
                      <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
                      <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
                    </Types>");

                GhiXml(zip, "_rels/.rels",
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
                      <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
                    </Relationships>");

                GhiXml(zip, "xl/workbook.xml",
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""
                              xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
                      <sheets><sheet name=""Bao cao"" sheetId=""1"" r:id=""rId1""/></sheets>
                    </workbook>");

                GhiXml(zip, "xl/_rels/workbook.xml.rels",
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
                      <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
                    </Relationships>");

                var entry = zip.CreateEntry("xl/worksheets/sheet1.xml");

                using (var stream = entry.Open())
                using (var writer = XmlWriter.Create(
                    stream, new XmlWriterSettings
                    {
                        Encoding = new UTF8Encoding(false),
                        Indent = true
                    }))
                {
                    const string ns =
                        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

                    writer.WriteStartDocument();
                    writer.WriteStartElement("worksheet", ns);

                    writer.WriteStartElement("cols", ns);

                    void Cot(int so, int rong)
                    {
                        writer.WriteStartElement("col", ns);
                        writer.WriteAttributeString("min", so.ToString());
                        writer.WriteAttributeString("max", so.ToString());
                        writer.WriteAttributeString("width", rong.ToString());
                        writer.WriteAttributeString("customWidth", "1");
                        writer.WriteEndElement();
                    }

                    Cot(1, 65);
                    Cot(2, 25);
                    Cot(3, 18);

                    writer.WriteEndElement();
                    writer.WriteStartElement("sheetData", ns);

                    void OChu(string chuoi)
                    {
                        writer.WriteStartElement("c", ns);
                        writer.WriteAttributeString("t", "inlineStr");
                        writer.WriteStartElement("is", ns);
                        writer.WriteStartElement("t", ns);
                        writer.WriteString(chuoi);
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                    }

                    void DongChu(params string[] giaTri)
                    {
                        writer.WriteStartElement("row", ns);
                        foreach (string giaTriO in giaTri)
                            OChu(giaTriO);
                        writer.WriteEndElement();
                    }

                    DongChu("BÁO CÁO", _baoCao.NoiDung);
                    DongChu("Người lập", _baoCao.NguoiLap);
                    DongChu("Dãy trọ", _baoCao.DayTro);
                    DongChu("Kỳ", _baoCao.Ky);
                    DongChu("Ngày lập", _baoCao.NgayLap.ToString("dd/MM/yyyy HH:mm"));
                    DongChu("Nội dung", "Giá trị", "Đơn vị");

                    foreach (var ct in _baoCao.ChiTieu)
                    {
                        writer.WriteStartElement("row", ns);
                        OChu(ct.NoiDung);

                        writer.WriteStartElement("c", ns);
                        writer.WriteStartElement("v", ns);
                        writer.WriteString(
                            ct.GiaTri.ToString(CultureInfo.InvariantCulture));
                        writer.WriteEndElement();
                        writer.WriteEndElement();

                        OChu(ct.DonVi);
                        writer.WriteEndElement();
                    }

                    DongChu("Ghi chú", _baoCao.GhiChu);

                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }

                txtThongBao.Text = "Đã xuất Excel.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text = "Không xuất được Excel: " + ex.Message;
            }
        }

        private static void GhiXml(
            ZipArchive zip, string ten, string noiDung)
        {
            var entry = zip.CreateEntry(ten);
            using var stream = entry.Open();
            using var writer = new StreamWriter(
                stream, new UTF8Encoding(false));

            writer.Write(noiDung);
        }

        private void InBaoCao()
        {
            if (_baoCao == null)
                return;

            try
            {
                var dialog = new PrintDialog();

                // Muốn xuất PDF, chọn Microsoft Print to PDF.
                if (dialog.ShowDialog() != true)
                    return;

                var doc = new FlowDocument
                {
                    PagePadding = new Thickness(40),
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                    FontSize = 12
                };

                void Dong(string noiDung, bool dam = false)
                {
                    var paragraph = new Paragraph(new Run(noiDung));
                    if (dam)
                        paragraph.FontWeight = FontWeights.Bold;
                    doc.Blocks.Add(paragraph);
                }

                Dong("BÁO CÁO: " + _baoCao.NoiDung, true);
                Dong("Người lập: " + _baoCao.NguoiLap);
                Dong("Dãy trọ: " + _baoCao.DayTro);
                Dong("Kỳ: " + _baoCao.Ky);
                Dong("Lập lúc: " + _baoCao.NgayLap.ToString("dd/MM/yyyy HH:mm"));

                foreach (var ct in _baoCao.ChiTieu)
                    Dong(ct.NoiDung + ": " + ct.HienThi);

                Dong("Ghi chú: " + txtGhiChu.Text.Trim());

                doc.PageWidth = dialog.PrintableAreaWidth;
                doc.PageHeight = dialog.PrintableAreaHeight;
                doc.ColumnWidth = dialog.PrintableAreaWidth;

                dialog.PrintDocument(
                    ((IDocumentPaginatorSource)doc).DocumentPaginator,
                    "Bao cao quan ly nha tro");
            }
            catch (Exception ex)
            {
                txtThongBao.Text = "Không in được: " + ex.Message;
            }
        }
    }
}