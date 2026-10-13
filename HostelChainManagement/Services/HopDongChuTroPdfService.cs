using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfDocumentModel = MigraDoc.DocumentObjectModel.Document;
using Section = MigraDoc.DocumentObjectModel.Section;
using Paragraph = MigraDoc.DocumentObjectModel.Paragraph;
using Table = MigraDoc.DocumentObjectModel.Tables.Table;

namespace QuanLyChuoiNhaTro.Services
{
    public static class HopDongChuTroPdfService
    {
        private static readonly CultureInfo Vietnamese =
            CultureInfo.GetCultureInfo("vi-VN");

        public static async Task<byte[]> TaoPdfAsync(
            string maHopDong)
        {
            DataTable table =
                await ChiTietHopDongChuTroService
                    .LayChiTietAsync(maHopDong);

            if (table.Rows.Count == 0)
            {
                throw new InvalidOperationException(
                    "Không tìm thấy hợp đồng thuộc quyền quản lý của bạn.");
            }

            DataRow hopDong = table.Rows[0];

            return await Task.Run(() => TaoTaiLieu(hopDong));
        }

        private static byte[] TaoTaiLieu(DataRow row)
        {
            var document = new PdfDocumentModel();

            document.Info.Title =
                "Hợp đồng thuê phòng " + LayChuoi(row, "MaHopDong");

            document.Info.Author =
                "Hệ thống quản lý nhà trọ";

            var normal = document.Styles["Normal"]
                ?? throw new InvalidOperationException(
                    "Không tìm thấy định dạng PDF mặc định.");

            normal.Font.Name = "Arial";
            normal.Font.Size = 11;
            normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);

            Section section = document.AddSection();

            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.TopMargin = Unit.FromCentimeter(2);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(2.2);
            section.PageSetup.RightMargin = Unit.FromCentimeter(2.2);

            Paragraph quocHieu = section.AddParagraph(
                "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM");

            quocHieu.Format.Alignment = ParagraphAlignment.Center;
            quocHieu.Format.Font.Bold = true;
            quocHieu.Format.Font.Size = 11;
            quocHieu.Format.SpaceAfter = Unit.FromPoint(3);

            Paragraph tieuNgu = section.AddParagraph(
                "Độc lập - Tự do - Hạnh phúc");

            tieuNgu.Format.Alignment = ParagraphAlignment.Center;
            tieuNgu.Format.Font.Bold = true;
            tieuNgu.Format.SpaceAfter = Unit.FromPoint(20);

            Paragraph tieuDe = section.AddParagraph(
                "HỢP ĐỒNG THUÊ PHÒNG TRỌ");

            tieuDe.Format.Alignment = ParagraphAlignment.Center;
            tieuDe.Format.Font.Size = 17;
            tieuDe.Format.Font.Bold = true;
            tieuDe.Format.SpaceAfter = Unit.FromPoint(7);

            Paragraph maHopDong = section.AddParagraph(
                "Số hợp đồng: " + LayChuoi(row, "MaHopDong"));

            maHopDong.Format.Alignment = ParagraphAlignment.Center;
            maHopDong.Format.SpaceAfter = Unit.FromPoint(6);

            Paragraph ngayKy = section.AddParagraph(
                "Ngày ký: " + LayNgay(row, "NgayKy"));

            ngayKy.Format.Alignment = ParagraphAlignment.Center;
            ngayKy.Format.SpaceAfter = Unit.FromPoint(15);

            ThemTieuDe(section, "I. THÔNG TIN CÁC BÊN");

            ThemTieuDeNho(section, "Bên A - Bên cho thuê");

            ThemTruong(
                section,
                "Đại diện bên cho thuê",
                LayChuoi(row, "DaiDienBenChoThue"));

            ThemTruong(
                section,
                "CCCD người đại diện",
                LayChuoi(row, "CCCDBenChoThue"));

            ThemTruong(
                section,
                "Chủ trọ quản lý dãy",
                LayChuoi(row, "TenChuTro"));

            ThemTruong(
                section,
                "Số điện thoại liên hệ Chủ trọ",
                LayChuoi(row, "SoDienThoaiChuTro"));

            ThemTruong(
                section,
                "Địa chỉ liên hệ Chủ trọ",
                LayChuoi(row, "DiaChiChuTro"));

            ThemTieuDeNho(section, "Bên B - Bên thuê");

            ThemTruong(
                section,
                "Họ và tên",
                LayChuoi(row, "KhachThue"));

            ThemTruong(
                section,
                "CCCD",
                LayChuoi(row, "CCCDKhachThue"));

            ThemTruong(
                section,
                "Số điện thoại",
                LayChuoi(row, "SoDienThoaiKhachThue"));

            ThemTruong(
                section,
                "Địa chỉ liên hệ",
                LayChuoi(row, "DiaChiKhachThue"));

            ThemTieuDe(section, "II. THÔNG TIN PHÒNG THUÊ");

            ThemTruong(
                section,
                "Dãy trọ",
                LayChuoi(row, "TenDayTro"));

            ThemTruong(
                section,
                "Số phòng",
                LayChuoi(row, "SoPhong"));

            ThemTruong(
                section,
                "Địa chỉ phòng thuê",
                LayChuoi(row, "DiaChiDayTro"));

            ThemTieuDe(
                section,
                "III. THỜI HẠN VÀ THANH TOÁN");

            ThemTruong(
                section,
                "Ngày bắt đầu",
                LayNgay(row, "NgayBatDau"));

            ThemTruong(
                section,
                "Ngày kết thúc",
                LayNgay(row, "NgayKetThuc"));

            ThemTruong(
                section,
                "Giá thuê",
                LayTien(row, "GiaThue") + "/tháng");

            ThemTruong(
                section,
                "Tiền đặt cọc",
                LayTien(row, "TienCoc"));

            string ngayThanhToan =
                LayChuoi(row, "KyThanhToan");

            ThemTruong(
                section,
                "Ngày đến hạn thanh toán hằng tháng",
                ngayThanhToan == "Chưa cập nhật"
                    ? ngayThanhToan
                    : "Ngày " + ngayThanhToan);

            ThemTruong(
                section,
                "Trạng thái hợp đồng",
                LayChuoi(row, "TrangThai"));

            ThemTieuDe(
                section,
                "IV. ĐIỀU KHOẢN HỢP ĐỒNG");

            ThemNoiDungNhieuDong(
                section,
                LayChuoiRong(row, "DieuKhoan"),
                "Chưa có điều khoản hợp đồng được lưu. "
                + "Cần bổ sung và thống nhất điều khoản "
                + "trước khi sử dụng văn bản để ký kết.");

            string quyDinh =
                LayChuoiRong(row, "QuyDinh");

            if (!string.IsNullOrWhiteSpace(quyDinh))
            {
                ThemTieuDeNho(
                    section,
                    "Quy định của dãy trọ");

                ThemNoiDungNhieuDong(
                    section,
                    quyDinh,
                    "");
            }

            ThemTieuDe(
                section,
                "V. XÁC NHẬN CỦA CÁC BÊN");

            Paragraph xacNhan = section.AddParagraph(
                "Hai bên đọc, kiểm tra thông tin và ký tên "
                + "sau khi thống nhất toàn bộ nội dung hợp đồng.");

            xacNhan.Format.SpaceAfter = Unit.FromPoint(18);

            Table bangKy = section.AddTable();

            bangKy.Borders.Visible = false;

            bangKy.AddColumn(Unit.FromCentimeter(8.2));
            bangKy.AddColumn(Unit.FromCentimeter(8.2));

            Row dongTieuDeKy = bangKy.AddRow();

            dongTieuDeKy.Cells[0].Format.Alignment =
                ParagraphAlignment.Center;

            dongTieuDeKy.Cells[1].Format.Alignment =
                ParagraphAlignment.Center;

            dongTieuDeKy.Cells[0].Format.Font.Bold = true;
            dongTieuDeKy.Cells[1].Format.Font.Bold = true;

            dongTieuDeKy.Cells[0].AddParagraph(
                "ĐẠI DIỆN BÊN CHO THUÊ");

            dongTieuDeKy.Cells[1].AddParagraph(
                "BÊN THUÊ");

            Row dongHuongDanKy = bangKy.AddRow();

            dongHuongDanKy.Cells[0].Format.Alignment =
                ParagraphAlignment.Center;

            dongHuongDanKy.Cells[1].Format.Alignment =
                ParagraphAlignment.Center;

            dongHuongDanKy.Cells[0].AddParagraph(
                "(Ký và ghi rõ họ tên)");

            dongHuongDanKy.Cells[1].AddParagraph(
                "(Ký và ghi rõ họ tên)");

            Row khoangTrongKy = bangKy.AddRow();
            khoangTrongKy.Height = Unit.FromCentimeter(2.5);

            Row dongHoTenKy = bangKy.AddRow();

            dongHoTenKy.Cells[0].Format.Alignment =
                ParagraphAlignment.Center;

            dongHoTenKy.Cells[1].Format.Alignment =
                ParagraphAlignment.Center;

            dongHoTenKy.Cells[0].AddParagraph(
                LayChuoi(row, "DaiDienBenChoThue"));

            dongHoTenKy.Cells[1].AddParagraph(
                LayChuoi(row, "KhachThue"));

            Paragraph ghiChu = section.AddParagraph(
                "Bản hợp đồng được tạo từ dữ liệu của hệ thống "
                + "quản lý nhà trọ. Cần kiểm tra thông tin, "
                + "điều khoản và chữ ký trước khi sử dụng "
                + "như văn bản hợp đồng chính thức.");

            ghiChu.Format.Font.Italic = true;
            ghiChu.Format.Font.Size = 9;
            ghiChu.Format.SpaceBefore = Unit.FromPoint(20);

            Paragraph footer =
                section.Footers.Primary.AddParagraph();

            footer.Format.Alignment = ParagraphAlignment.Center;
            footer.Format.Font.Size = 9;

            footer.AddText("Trang ");
            footer.AddPageField();
            footer.AddText(" / ");
            footer.AddNumPagesField();

            var renderer = new PdfDocumentRenderer
            {
                Document = document
            };

            renderer.RenderDocument();

            var pdfDocument = renderer.PdfDocument
                ?? throw new InvalidOperationException(
                    "Không thể tạo tài liệu PDF.");

            using var stream = new MemoryStream();

            pdfDocument.Save(stream, false);

            return stream.ToArray();
        }

        private static void ThemTieuDe(
            Section section,
            string noiDung)
        {
            Paragraph paragraph =
                section.AddParagraph(noiDung);

            paragraph.Format.Font.Bold = true;
            paragraph.Format.Font.Size = 12;
            paragraph.Format.SpaceBefore = Unit.FromPoint(13);
            paragraph.Format.SpaceAfter = Unit.FromPoint(8);
            paragraph.Format.KeepWithNext = true;
        }

        private static void ThemTieuDeNho(
            Section section,
            string noiDung)
        {
            Paragraph paragraph =
                section.AddParagraph(noiDung);

            paragraph.Format.Font.Bold = true;
            paragraph.Format.SpaceBefore = Unit.FromPoint(8);
            paragraph.Format.SpaceAfter = Unit.FromPoint(5);
            paragraph.Format.KeepWithNext = true;
        }

        private static void ThemTruong(
            Section section,
            string nhan,
            string giaTri)
        {
            Paragraph paragraph = section.AddParagraph();

            paragraph.AddFormattedText(
                nhan + ": ",
                TextFormat.Bold);

            paragraph.AddText(
                string.IsNullOrWhiteSpace(giaTri)
                    ? "Chưa cập nhật"
                    : giaTri);
        }

        private static void ThemNoiDungNhieuDong(
            Section section,
            string noiDung,
            string noiDungThayThe)
        {
            if (string.IsNullOrWhiteSpace(noiDung))
            {
                section.AddParagraph(noiDungThayThe);
                return;
            }

            string vanBan = noiDung
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');

            foreach (string dong in vanBan.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(dong))
                    continue;

                Paragraph paragraph =
                    section.AddParagraph(dong.Trim());

                paragraph.Format.Alignment =
                    ParagraphAlignment.Justify;
            }
        }

        private static string LayChuoi(
            DataRow row,
            string tenCot)
        {
            string giaTri =
                LayChuoiRong(row, tenCot);

            return string.IsNullOrWhiteSpace(giaTri)
                ? "Chưa cập nhật"
                : giaTri;
        }

        private static string LayChuoiRong(
            DataRow row,
            string tenCot)
        {
            if (!row.Table.Columns.Contains(tenCot)
                || row.IsNull(tenCot))
            {
                return "";
            }

            return Convert.ToString(
                row[tenCot],
                Vietnamese) ?? "";
        }

        private static string LayNgay(
            DataRow row,
            string tenCot)
        {
            if (!row.Table.Columns.Contains(tenCot)
                || row.IsNull(tenCot))
            {
                return "Chưa cập nhật";
            }

            return Convert.ToDateTime(row[tenCot])
                .ToString("dd/MM/yyyy", Vietnamese);
        }

        private static string LayTien(
            DataRow row,
            string tenCot)
        {
            if (!row.Table.Columns.Contains(tenCot)
                || row.IsNull(tenCot))
            {
                return "Chưa cập nhật";
            }

            return Convert.ToDecimal(row[tenCot])
                .ToString("N0", Vietnamese) + " VNĐ";
        }
    }
}