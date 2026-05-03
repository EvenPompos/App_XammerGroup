using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace App_XammerGroup
{
    public static class SalesReportPdfExporter
    {
        public static void Export(string filePath, SalesReportData report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var lines = BuildLines(report);
            var pages = Paginate(lines, 42);
            var pdfBytes = BuildPdf(pages);

            File.WriteAllBytes(filePath, pdfBytes);
        }

        private static List<string> BuildLines(SalesReportData report)
        {
            var lines = new List<string>
            {
                "Sales Report",
                $"Period: {report.StartDate:yyyy-MM-dd} - {report.EndDate:yyyy-MM-dd}",
                $"Orders count: {report.OrdersCount}",
                $"Revenue total: {report.TotalRevenue.ToString("F2", CultureInfo.InvariantCulture)} RUB",
                string.Empty,
                "Products:",
                "ID   Name                           Qty        Revenue"
            };

            foreach (var item in report.Products)
            {
                string productName = Transliterate(item.ProductName);
                if (productName.Length > 28)
                {
                    productName = productName.Substring(0, 28);
                }

                lines.Add(
                    $"{item.ProductId,-4} {productName,-28} {item.QuantitySold,6}   {item.Revenue.ToString("F2", CultureInfo.InvariantCulture),12}");
            }

            if (report.Products.Count == 0)
            {
                lines.Add("No sales in selected period.");
            }

            return lines;
        }

        private static List<List<string>> Paginate(List<string> lines, int pageSize)
        {
            var pages = new List<List<string>>();

            for (int index = 0; index < lines.Count; index += pageSize)
            {
                pages.Add(lines.Skip(index).Take(pageSize).ToList());
            }

            if (pages.Count == 0)
            {
                pages.Add(new List<string> { "Sales Report", "No data." });
            }

            return pages;
        }

        private static byte[] BuildPdf(List<List<string>> pages)
        {
            var objects = new List<string>();
            objects.Add("<< /Type /Catalog /Pages 2 0 R >>");

            int pagesObjectIndex = 2;
            int fontObjectIndex = 3;
            int firstPageObjectIndex = 4;

            var pageObjectNumbers = new List<int>();
            for (int i = 0; i < pages.Count; i++)
            {
                pageObjectNumbers.Add(firstPageObjectIndex + (i * 2));
            }

            string kids = string.Join(" ", pageObjectNumbers.Select(number => $"{number} 0 R"));
            objects.Add($"<< /Type /Pages /Count {pages.Count} /Kids [ {kids} ] >>");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>");

            for (int i = 0; i < pages.Count; i++)
            {
                int pageObjectNumber = firstPageObjectIndex + (i * 2);
                int contentObjectNumber = pageObjectNumber + 1;

                objects.Add($"<< /Type /Page /Parent {pagesObjectIndex} 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 {fontObjectIndex} 0 R >> >> /Contents {contentObjectNumber} 0 R >>");
                objects.Add(BuildContentStream(pages[i]));
            }

            var builder = new StringBuilder();
            builder.Append("%PDF-1.4\n");

            var offsets = new List<int> { 0 };
            for (int i = 0; i < objects.Count; i++)
            {
                offsets.Add(builder.Length);
                builder.AppendFormat(CultureInfo.InvariantCulture, "{0} 0 obj\n", i + 1);
                builder.Append(objects[i]);
                builder.Append("\nendobj\n");
            }

            int xrefOffset = builder.Length;
            builder.AppendFormat(CultureInfo.InvariantCulture, "xref\n0 {0}\n", objects.Count + 1);
            builder.Append("0000000000 65535 f \n");

            for (int i = 1; i < offsets.Count; i++)
            {
                builder.AppendFormat(CultureInfo.InvariantCulture, "{0:0000000000} 00000 n \n", offsets[i]);
            }

            builder.Append("trailer\n");
            builder.AppendFormat(CultureInfo.InvariantCulture, "<< /Size {0} /Root 1 0 R >>\n", objects.Count + 1);
            builder.Append("startxref\n");
            builder.AppendFormat(CultureInfo.InvariantCulture, "{0}\n", xrefOffset);
            builder.Append("%%EOF");

            return Encoding.ASCII.GetBytes(builder.ToString());
        }

        private static string BuildContentStream(List<string> lines)
        {
            var streamBuilder = new StringBuilder();
            streamBuilder.Append("BT\n");
            streamBuilder.Append("/F1 11 Tf\n");
            streamBuilder.Append("50 800 Td\n");
            streamBuilder.Append("14 TL\n");

            for (int i = 0; i < lines.Count; i++)
            {
                string escapedLine = EscapePdfText(lines[i]);
                if (i == 0)
                {
                    streamBuilder.AppendFormat(CultureInfo.InvariantCulture, "({0}) Tj\n", escapedLine);
                }
                else
                {
                    streamBuilder.AppendFormat(CultureInfo.InvariantCulture, "T* ({0}) Tj\n", escapedLine);
                }
            }

            streamBuilder.Append("ET");
            string stream = streamBuilder.ToString();

            return string.Format(
                CultureInfo.InvariantCulture,
                "<< /Length {0} >>\nstream\n{1}\nendstream",
                Encoding.ASCII.GetByteCount(stream),
                stream);
        }

        private static string EscapePdfText(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("(", "\\(")
                .Replace(")", "\\)");
        }

        private static string Transliterate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            var map = new Dictionary<char, string>
            {
                ['А'] = "A", ['Б'] = "B", ['В'] = "V", ['Г'] = "G", ['Д'] = "D",
                ['Е'] = "E", ['Ё'] = "E", ['Ж'] = "Zh", ['З'] = "Z", ['И'] = "I",
                ['Й'] = "Y", ['К'] = "K", ['Л'] = "L", ['М'] = "M", ['Н'] = "N",
                ['О'] = "O", ['П'] = "P", ['Р'] = "R", ['С'] = "S", ['Т'] = "T",
                ['У'] = "U", ['Ф'] = "F", ['Х'] = "Kh", ['Ц'] = "Ts", ['Ч'] = "Ch",
                ['Ш'] = "Sh", ['Щ'] = "Sch", ['Ъ'] = "", ['Ы'] = "Y", ['Ь'] = "",
                ['Э'] = "E", ['Ю'] = "Yu", ['Я'] = "Ya",
                ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d",
                ['е'] = "e", ['ё'] = "e", ['ж'] = "zh", ['з'] = "z", ['и'] = "i",
                ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n",
                ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t",
                ['у'] = "u", ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts", ['ч'] = "ch",
                ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "",
                ['э'] = "e", ['ю'] = "yu", ['я'] = "ya"
            };

            var builder = new StringBuilder();
            foreach (char character in value)
            {
                if (map.TryGetValue(character, out string replacement))
                {
                    builder.Append(replacement);
                }
                else if (character <= 127)
                {
                    builder.Append(character);
                }
                else
                {
                    builder.Append('?');
                }
            }

            return builder.ToString();
        }
    }

    public sealed class SalesReportData
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int OrdersCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<SalesReportProductLine> Products { get; set; } = new List<SalesReportProductLine>();
    }

    public sealed class SalesReportProductLine
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }
}
