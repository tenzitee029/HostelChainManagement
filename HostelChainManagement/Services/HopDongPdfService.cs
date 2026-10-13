using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;

using PdfDocumentModel =
    MigraDoc.DocumentObjectModel.Document;

namespace QuanLyChuoiNhaTro.Services
{
    public static class HopDongPdfService
    {
        private static readonly CultureInfo Vietnamese =
            CultureInfo.GetCultureInfo("vi-VN");

        public static async Task<byte[]> TaoPdfAsync(
            string maHopDong)
        {
            if (string.IsNullOrWhiteSpace(maHopDong)
                || maHopDong.Length > 20)
            {
                throw new InvalidOperationException(
                    "Mã hợp đồng không hợp lệ.");
            }

            // Service hiện tại kiểm tra quyền Khách thuê
            // và chỉ trả về hợp đồng thuộc tài khoản đăng nhập.
            DataTable table =
                await KhachThueService.GetHopDongCuaToiAsync();

            DataRow? selected = null;

            foreach (DataRow row in table.Rows)
            {
                string currentId =
                    Convert.ToString(row["MaHopDong"]) ?? "";

                if (string.Equals(
                    currentId,
                    maHopDong,
                    StringComparison.Ordinal))
                {
                    selected = row;
                    break;
                }
            }

            DataRow contract = selected
                ?? throw new InvalidOperationException(
                    "Không tìm thấy hợp đồng thuộc tài khoản của bạn.");

            return await Task.Run(() => BuildPdf(contract));
        }

        private static byte[] BuildPdf(DataRow row)
        {
            var document = new PdfDocumentModel();

            document.Info.Title =
                "Hợp đồng thuê " + ReadText(row, "MaHopDong");

            document.Info.Author =
                "Hệ thống quản lý chuỗi nhà trọ";

            var normal = document.Styles["Normal"]
                ?? throw new InvalidOperationException(
                    "Không tìm thấy style Normal để tạo PDF.");

            normal.Font.Name = "Arial";
            normal.Font.Size = 11;

            normal.ParagraphFormat.SpaceAfter =
                Unit.FromPoint(7);

            Section section = document.AddSection();

            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.TopMargin = Unit.FromCentimeter(2);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(2.2);
            section.PageSetup.RightMargin = Unit.FromCentimeter(2.2);

            Paragraph title = section.AddParagraph(
                "THÔNG TIN HỢP ĐỒNG THUÊ PHÒNG");

            title.Format.Alignment = ParagraphAlignment.Center;
            title.Format.Font.Size = 17;
            title.Format.Font.Bold = true;
            title.Format.SpaceAfter = Unit.FromPoint(12);

            Paragraph code = section.AddParagraph(
                "Mã hợp đồng: " + ReadText(row, "MaHopDong"));

            code.Format.Alignment = ParagraphAlignment.Center;
            code.Format.SpaceAfter = Unit.FromPoint(18);

            AddHeading(section, "1. Thông tin các bên");

            AddField(
                section,
                "Đại diện bên cho thuê",
                ReadText(row, "DaiDienBenChoThue"));

            AddField(
                section,
                "Người đứng tên bên thuê",
                ReadText(row, "TenKhachThue"));

            AddField(
                section,
                "CCCD",
                ReadText(row, "CCCD"));

            AddField(
                section,
                "Số điện thoại",
                ReadText(row, "SoDienThoai"));

            AddHeading(section, "2. Phòng thuê");

            AddField(
                section,
                "Dãy trọ",
                ReadText(row, "TenDayTro"));

            AddField(
                section,
                "Phòng",
                ReadText(row, "SoPhong"));

            AddHeading(section, "3. Thời hạn và thanh toán");

            AddField(
                section,
                "Ngày ký",
                ReadDate(row, "NgayKy"));

            AddField(
                section,
                "Ngày bắt đầu",
                ReadDate(row, "NgayBatDau"));

            AddField(
                section,
                "Ngày kết thúc",
                ReadDate(row, "NgayKetThuc"));

            AddField(
                section,
                "Tiền cọc",
                ReadMoney(row, "TienCoc"));

            AddField(
                section,
                "Tiền thuê",
                row.IsNull("GiaThue")
                    ? "Chưa cập nhật"
                    : ReadMoney(row, "GiaThue") + "/tháng");

            AddField(
                section,
                "Ngày đến hạn thanh toán trong tháng",
                ReadText(row, "KyThanhToan"));

            AddField(
                section,
                "Trạng thái",
                ReadText(row, "TrangThai"));

            AddHeading(section, "4. Điều khoản");

            string terms = ReadText(row, "DieuKhoan");

            if (string.IsNullOrWhiteSpace(terms))
            {
                section.AddParagraph(
                    "Chưa có điều khoản được lưu.");
            }
            else
            {
                string normalized = terms
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n');

                foreach (string line in normalized.Split('\n'))
                {
                    Paragraph paragraph =
                        section.AddParagraph(line);

                    paragraph.Format.Alignment =
                        ParagraphAlignment.Justify;
                }
            }

            Paragraph note = section.AddParagraph(
                "Bản thông tin được xuất từ dữ liệu của hệ thống; "
                + "không thay thế bản hợp đồng có chữ ký.");

            note.Format.Font.Italic = true;
            note.Format.Font.Size = 9;
            note.Format.SpaceBefore = Unit.FromPoint(18);

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
                    "Không tạo được tài liệu PDF.");

            using var stream = new MemoryStream();

            pdfDocument.Save(stream, false);

            return stream.ToArray();
        }

        private static void AddHeading(
            Section section,
            string text)
        {
            Paragraph paragraph = section.AddParagraph(text);

            paragraph.Format.Font.Bold = true;
            paragraph.Format.Font.Size = 12;

            paragraph.Format.SpaceBefore = Unit.FromPoint(12);
            paragraph.Format.SpaceAfter = Unit.FromPoint(8);
            paragraph.Format.KeepWithNext = true;
        }

        private static void AddField(
            Section section,
            string label,
            string value)
        {
            Paragraph paragraph = section.AddParagraph();

            paragraph.AddFormattedText(
                label + ": ",
                TextFormat.Bold);

            paragraph.AddText(
                string.IsNullOrWhiteSpace(value)
                    ? "Chưa cập nhật"
                    : value);
        }

        private static string ReadText(
            DataRow row,
            string column)
        {
            if (row.IsNull(column))
                return "";

            return Convert.ToString(
                row[column],
                Vietnamese) ?? "";
        }

        private static string ReadDate(
            DataRow row,
            string column)
        {
            if (row.IsNull(column))
                return "Chưa cập nhật";

            return Convert.ToDateTime(row[column])
                .ToString("dd/MM/yyyy", Vietnamese);
        }

        private static string ReadMoney(
            DataRow row,
            string column)
        {
            if (row.IsNull(column))
                return "Chưa cập nhật";

            return Convert.ToDecimal(row[column])
                .ToString("N0", Vietnamese) + " VNĐ";
        }
    }
}