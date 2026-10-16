using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;

namespace QuanLyChuoiNhaTro.Services
{
    public static class HoaDonChuTroPdfService
    {
        private static readonly CultureInfo Vietnamese =
            CultureInfo.GetCultureInfo("vi-VN");

        public static IReadOnlyList<string> TieuDeCot { get; } =
            Array.AsReadOnly(new[]
            {
                "Khoản thu",
                "Chỉ số cũ",
                "Chỉ số mới",
                "Số lượng",
                "Đơn giá (đ)",
                "Thành tiền (đ)",
                "Ghi chú"
            });

        // Tổng chiều rộng 17 cm, vừa trang A4 với lề trái/phải 2 cm.
        public static IReadOnlyList<double> TyLeCot { get; } =
            Array.AsReadOnly(new[]
            {
                3.0, 1.5, 1.5, 1.5, 2.5, 2.5, 4.0
            });

        public sealed record DuLieuHoaDon(
            string MaHoaDon,
            string TongTien,
            DateTime ThoiDiemXuat,
            IReadOnlyList<KeyValuePair<string, string>> ThongTin,
            IReadOnlyList<string[]> CacKhoanThu);

        public static async Task<DuLieuHoaDon> LayDuLieuAsync(
            string maHoaDon)
        {
            // Lấy lại dữ liệu mỗi lần xuất/in.
            // Service hiện có kiểm tra vai trò và quyền sở hữu hóa đơn.
            var ketQua = await ChiTietHoaDonChuTroService
                .LayChiTietAsync(maHoaDon);

            return TaoDuLieu(ketQua);
        }

        internal static DuLieuHoaDon TaoDuLieu(
            ChiTietHoaDonChuTroService.KetQuaChiTiet ketQua)
        {
            if (ketQua.ThongTin.Rows.Count == 0)
            {
                throw new InvalidOperationException(
                    "Không tìm thấy hóa đơn hoặc hóa đơn "
                    + "không thuộc quyền quản lý của bạn.");
            }

            DataRow row = ketQua.ThongTin.Rows[0];

            var thongTin = new List<KeyValuePair<string, string>>
            {
                new("Mã hóa đơn", LayChuoi(row, "MaHoaDon")),
                new("Mã hợp đồng", LayChuoi(row, "MaHopDong")),
                new("Khách thuê", LayChuoi(row, "HoTenKhach")),
                new("Phòng", LayChuoi(row, "SoPhong")),
                new("Dãy trọ", LayChuoi(row, "TenDayTro")),
                new(
                    "Kỳ thanh toán",
                    LayChuoi(row, "Thang")
                    + "/"
                    + LayChuoi(row, "Nam")),
                new("Ngày lập", LayNgay(row, "NgayLap")),
                new("Hạn thanh toán", LayNgay(row, "HanThanhToan")),
                new(
                    "Trạng thái thanh toán",
                    LayChuoi(row, "TrangThai")),
                new(
                    "Phương thức thanh toán",
                    LayChuoi(row, "PhuongThucThanhToan"))
            };

            var cacKhoanThu = new List<string[]>();

            foreach (DataRow ct in ketQua.CacKhoanThu.Rows)
            {
                cacKhoanThu.Add(new[]
                {
                    LayChuoi(ct, "LoaiKhoanThu"),
                    LaySo(ct, "ChiSoCu", "N2"),
                    LaySo(ct, "ChiSoMoi", "N2"),
                    LaySo(ct, "SoLuong", "N2"),
                    LaySo(ct, "DonGia", "N0"),
                    LaySo(ct, "ThanhTien", "N0"),
                    LayChuoi(ct, "GhiChu")
                });
            }

            string tongTien = LaySo(row, "TongTien", "N0");

            return new DuLieuHoaDon(
                LayChuoi(row, "MaHoaDon"),
                tongTien == "—" ? tongTien : tongTien + " đ",
                DateTime.Now,
                thongTin.AsReadOnly(),
                cacKhoanThu.AsReadOnly());
        }

        public static byte[] TaoPdf(DuLieuHoaDon duLieu)
        {
            ArgumentNullException.ThrowIfNull(duLieu);

            var document = new Document();

            document.Info.Title =
                "Hóa đơn " + duLieu.MaHoaDon;

            document.Info.Author =
                "Hệ thống quản lý nhà trọ";

            var normal = document.Styles["Normal"]
                ?? throw new InvalidOperationException(
                    "Không tìm thấy định dạng PDF mặc định.");

            normal.Font.Name = "Arial";
            normal.Font.Size = 11;
            normal.ParagraphFormat.SpaceAfter =
                Unit.FromPoint(4);

            Section section = document.AddSection();

            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.TopMargin =
                Unit.FromCentimeter(2);
            section.PageSetup.BottomMargin =
                Unit.FromCentimeter(2);
            section.PageSetup.LeftMargin =
                Unit.FromCentimeter(2);
            section.PageSetup.RightMargin =
                Unit.FromCentimeter(2);

            Paragraph nhanHeThong = section.AddParagraph(
                "HỆ THỐNG QUẢN LÝ NHÀ TRỌ");

            nhanHeThong.Format.Alignment =
                ParagraphAlignment.Center;
            nhanHeThong.Format.Font.Size = 10;

            Paragraph tieuDe = section.AddParagraph(
                "HÓA ĐƠN TIỀN TRỌ");

            tieuDe.Format.Alignment =
                ParagraphAlignment.Center;
            tieuDe.Format.Font.Bold = true;
            tieuDe.Format.Font.Size = 18;
            tieuDe.Format.SpaceAfter =
                Unit.FromPoint(14);

            foreach (var thongTin in duLieu.ThongTin)
            {
                Paragraph paragraph =
                    section.AddParagraph();

                paragraph.AddFormattedText(
                    thongTin.Key + ": ",
                    TextFormat.Bold);

                paragraph.AddText(thongTin.Value);
            }

            Paragraph nhanKhoanThu = section.AddParagraph(
                "CHI TIẾT CÁC KHOẢN THU");

            nhanKhoanThu.Format.Font.Bold = true;
            nhanKhoanThu.Format.SpaceBefore =
                Unit.FromPoint(12);
            nhanKhoanThu.Format.SpaceAfter =
                Unit.FromPoint(8);
            nhanKhoanThu.Format.KeepWithNext = true;

            var table = section.AddTable();

            table.Rows.LeftIndent = Unit.Zero;
            table.Borders.Width = Unit.FromPoint(0.5);
            table.Borders.Color = Colors.Gray;
            table.Format.Font.Size = 9;
            table.Format.SpaceAfter = Unit.Zero;
            table.TopPadding = Unit.FromPoint(4);
            table.BottomPadding = Unit.FromPoint(4);

            for (int i = 0; i < TyLeCot.Count; i++)
            {
                var column = table.AddColumn(
                    Unit.FromCentimeter(TyLeCot[i]));

                if (i >= 1 && i <= 5)
                {
                    column.Format.Alignment =
                        ParagraphAlignment.Right;
                }
            }

            var header = table.AddRow();

            header.HeadingFormat = true;
            header.Format.Font.Bold = true;
            header.Shading.Color = Colors.LightGray;

            for (int i = 0; i < TieuDeCot.Count; i++)
            {
                header.Cells[i].Format.Alignment =
                    ParagraphAlignment.Center;

                header.Cells[i].AddParagraph(
                    TieuDeCot[i]);
            }

            if (duLieu.CacKhoanThu.Count == 0)
            {
                var empty = table.AddRow();

                empty.Cells[0].MergeRight =
                    TieuDeCot.Count - 1;

                empty.Cells[0].AddParagraph(
                    "Hóa đơn chưa có chi tiết khoản thu.");
            }
            else
            {
                foreach (string[] khoanThu
                    in duLieu.CacKhoanThu)
                {
                    var row = table.AddRow();

                    for (int i = 0; i < TieuDeCot.Count; i++)
                    {
                        row.Cells[i].AddParagraph(
                            khoanThu[i]);
                    }
                }
            }

            Paragraph tongTien = section.AddParagraph(
                "TỔNG TIỀN: " + duLieu.TongTien);

            tongTien.Format.Alignment =
                ParagraphAlignment.Right;
            tongTien.Format.Font.Bold = true;
            tongTien.Format.Font.Size = 14;
            tongTien.Format.SpaceBefore =
                Unit.FromPoint(12);

            Paragraph ngayXuat = section.AddParagraph(
                "Ngày xuất: "
                + duLieu.ThoiDiemXuat.ToString(
                    "dd/MM/yyyy HH:mm",
                    Vietnamese));

            ngayXuat.Format.Font.Size = 9;
            ngayXuat.Format.SpaceBefore =
                Unit.FromPoint(12);

            Paragraph footer =
                section.Footers.Primary.AddParagraph();

            footer.Format.Alignment =
                ParagraphAlignment.Center;
            footer.Format.Font.Size = 9;

            footer.AddText(
                duLieu.MaHoaDon + " · Trang ");
            footer.AddPageField();
            footer.AddText(" / ");
            footer.AddNumPagesField();

            var renderer = new PdfDocumentRenderer
            {
                Document = document
            };

            renderer.RenderDocument();

            var pdf = renderer.PdfDocument
                ?? throw new InvalidOperationException(
                    "Không thể tạo bản PDF hóa đơn.");

            using var stream = new MemoryStream();

            pdf.Save(stream, false);

            return stream.ToArray();
        }

        private static string LayChuoi(
            DataRow row,
            string tenCot)
        {
            if (!row.Table.Columns.Contains(tenCot)
                || row.IsNull(tenCot))
            {
                return "—";
            }

            string giaTri = Convert.ToString(
                row[tenCot],
                Vietnamese) ?? "";

            return string.IsNullOrWhiteSpace(giaTri)
                ? "—"
                : giaTri;
        }

        private static string LaySo(
            DataRow row,
            string tenCot,
            string dinhDang)
        {
            if (!row.Table.Columns.Contains(tenCot)
                || row.IsNull(tenCot))
            {
                return "—";
            }

            return Convert.ToDecimal(
                    row[tenCot],
                    Vietnamese)
                .ToString(dinhDang, Vietnamese);
        }

        private static string LayNgay(
            DataRow row,
            string tenCot)
        {
            if (!row.Table.Columns.Contains(tenCot)
                || row.IsNull(tenCot))
            {
                return "—";
            }

            return Convert.ToDateTime(
                    row[tenCot],
                    Vietnamese)
                .ToString("dd/MM/yyyy", Vietnamese);
        }
    }
}