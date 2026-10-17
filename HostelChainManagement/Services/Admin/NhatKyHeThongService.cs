using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Services.Admin
{
    public static class NhatKyHeThongService
    {
        private static string UserId()
        {
            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Admin")
            {
                throw new InvalidOperationException(
                    "Chức năng chỉ dành cho Admin hệ thống.");
            }

            return user.MaNguoiDung;
        }

        private static async Task AuthorizeAsync(
            SqlConnection connection,
            SqlTransaction? transaction = null)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = @"
                SELECT COUNT(*)
                FROM dbo.NguoiDung
                WHERE MaNguoiDung = @User
                  AND VaiTro = N'Admin'
                  AND TrangThai = N'Hoạt động';";

            command.Parameters.Add(
                "@User", SqlDbType.VarChar, 20)
                .Value = UserId();

            int count = Convert.ToInt32(
                await command.ExecuteScalarAsync());

            if (count != 1)
            {
                throw new InvalidOperationException(
                    "Tài khoản Admin không còn hoạt động. "
                    + "Vui lòng đăng nhập lại.");
            }
        }

        public static async Task<DataTable> GetLogsAsync()
        {
            using var connection = Database.CreateConnection();

            await connection.OpenAsync();
            await AuthorizeAsync(connection);

            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    MaNhatKy,
                    HanhDong,
                    NoiDung,
                    ThoiGian,
                    CASE
                        WHEN HanhDong = N'Log truy cập'
                            THEN N'Log truy cập'
                        WHEN HanhDong = N'Log lỗi runtime'
                            THEN N'Log lỗi runtime'
                        ELSE N'Quản trị'
                    END AS LoaiNhatKy
                FROM dbo.NhatKyHeThong
                ORDER BY MaNhatKy DESC;";

            command.Parameters.Add(
                "@User", SqlDbType.VarChar, 20)
                .Value = UserId();

            using var reader =
                await command.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }

        public static void ExportLogs(
            DataTable table,
            string path,
            bool excel)
        {
            string Clean(object value)
            {
                return Regex.Replace(
                    value.ToString() ?? "",
                    @"[\x00-\x08\x0B\x0C\x0E-\x1F\uFFFE\uFFFF]",
                    "");
            }

            var rows = new List<string[]>
            {
                new[]
                {
                    "Loại nhật ký",
                    "Nội dung nhật ký",
                    "Thời gian"
                }
            };

            rows.AddRange(
                table.AsEnumerable().Select(row => new[]
                {
                    Clean(row["LoaiNhatKy"]),
                    Clean(row["NoiDung"]),
                    Convert.ToDateTime(row["ThoiGian"])
                        .ToString("dd/MM/yyyy HH:mm:ss")
                }));

            if (!excel)
            {
                File.WriteAllLines(
                    path,
                    rows.Select(row =>
                        string.Join(
                            "\t",
                            row.Select(value =>
                                value.Replace("\t", " ")
                                    .Replace("\r", " ")
                                    .Replace("\n", " ")))),
                    new UTF8Encoding(true));

                return;
            }

            // Tạo workbook .xlsx bằng Open XML,
            // không yêu cầu cài thêm thư viện Excel.
            using var zip = ZipFile.Open(
                path,
                ZipArchiveMode.Create);

            void Part(string name, string content)
            {
                using var writer = new StreamWriter(
                    zip.CreateEntry(name).Open(),
                    new UTF8Encoding(false));

                writer.Write(content);
            }

            Part(
                "[Content_Types].xml",
                """
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);

            Part(
                "_rels/.rels",
                """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);

            Part(
                "xl/workbook.xml",
                """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets>
                    <sheet name="Nhật ký" sheetId="1" r:id="rId1"/>
                  </sheets>
                </workbook>
                """);

            Part(
                "xl/_rels/workbook.xml.rels",
                """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);

            var xml = new StringBuilder(
                """
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <cols>
                    <col min="1" max="1" width="22" customWidth="1"/>
                    <col min="2" max="2" width="90" customWidth="1"/>
                    <col min="3" max="3" width="24" customWidth="1"/>
                  </cols>
                  <sheetData>
                """);

            for (int i = 0; i < rows.Count; i++)
            {
                xml.Append($"<row r=\"{i + 1}\">");

                for (int j = 0; j < 3; j++)
                {
                    string cell = $"{(char)('A' + j)}{i + 1}";

                    xml.Append(
                        $"<c r=\"{cell}\" t=\"inlineStr\">"
                        + "<is><t xml:space=\"preserve\">"
                        + SecurityElement.Escape(rows[i][j])
                        + "</t></is></c>");
                }

                xml.Append("</row>");
            }

            xml.Append("</sheetData></worksheet>");

            Part(
                "xl/worksheets/sheet1.xml",
                xml.ToString());
        }
    }
}