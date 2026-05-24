using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

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

            File.WriteAllBytes(filePath, BuildPdf(Paginate(BuildSalesLines(report), 42)));
        }

        public static void ExportInventoryPdf(string filePath, InventoryReportData report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            File.WriteAllBytes(filePath, BuildPdf(Paginate(BuildInventoryLines(report), 42)));
        }

        public static void ExportInventoryExcel(string filePath, InventoryReportData report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            File.WriteAllText(filePath, BuildInventoryExcelXml(report), Encoding.UTF8);
        }

        private static List<string> BuildSalesLines(SalesReportData report)
        {
            var lines = new List<string>
            {
                "Отчет по продажам",
                $"Период: {report.StartDate:dd.MM.yyyy} - {report.EndDate:dd.MM.yyyy}",
                $"Количество заказов: {report.OrdersCount}",
                $"Общая выручка: {report.TotalRevenue:N2} руб.",
                string.Empty,
                "Продажи по товарам:",
                PadColumns("ID", 5, "Товар", 34, "Кол-во", 10, "Выручка", 14)
            };

            foreach (var item in report.Products)
            {
                lines.Add(PadColumns(
                    item.ProductId.ToString(CultureInfo.InvariantCulture), 5,
                    TrimTo(item.ProductName, 34), 34,
                    item.QuantitySold.ToString(CultureInfo.InvariantCulture), 10,
                    $"{item.Revenue:N2}", 14));
            }

            if (report.Products.Count == 0)
            {
                lines.Add("За выбранный период продаж не найдено.");
            }

            return lines;
        }

        private static List<string> BuildInventoryLines(InventoryReportData report)
        {
            var lines = new List<string>
            {
                "Отчет по складу",
                $"Дата формирования: {report.CreatedAt:dd.MM.yyyy HH:mm}",
                $"Всего позиций: {report.Items.Count}",
                string.Empty,
                PadColumns("ID", 5, "Материал", 34, "Остаток", 13, "Минимум", 13)
            };

            foreach (var item in report.Items)
            {
                lines.Add(PadColumns(
                    item.InventoryItemId.ToString(CultureInfo.InvariantCulture), 5,
                    TrimTo(item.ItemName, 34), 34,
                    $"{item.QuantityOnHand:N3} {item.UnitName}", 13,
                    $"{item.MinQuantity:N3} {item.UnitName}", 13));
            }

            if (report.Items.Count == 0)
            {
                lines.Add("Складские позиции не найдены.");
            }

            return lines;
        }

        private static string BuildInventoryExcelXml(InventoryReportData report)
        {
            var builder = new StringBuilder();
            builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            builder.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
            builder.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
            builder.AppendLine("<Styles>");
            builder.AppendLine("<Style ss:ID=\"Title\"><Font ss:Bold=\"1\" ss:Size=\"16\"/><Alignment ss:Horizontal=\"Center\"/></Style>");
            builder.AppendLine("<Style ss:ID=\"Header\"><Font ss:Bold=\"1\"/><Interior ss:Color=\"#D9EAF7\" ss:Pattern=\"Solid\"/><Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/><Border ss:Position=\"Left\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/><Border ss:Position=\"Right\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/><Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/></Borders></Style>");
            builder.AppendLine("<Style ss:ID=\"Cell\"><Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/><Border ss:Position=\"Left\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/><Border ss:Position=\"Right\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/><Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/></Borders></Style>");
            builder.AppendLine("<Style ss:ID=\"Number\"><NumberFormat ss:Format=\"0.000\"/><Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/><Border ss:Position=\"Left\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/><Border ss:Position=\"Right\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/><Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/></Borders></Style>");
            builder.AppendLine("</Styles>");
            builder.AppendLine("<Worksheet ss:Name=\"Склад\"><Table>");
            builder.AppendLine("<Column ss:Width=\"60\"/><Column ss:Width=\"260\"/><Column ss:Width=\"90\"/><Column ss:Width=\"110\"/><Column ss:Width=\"110\"/><Column ss:Width=\"140\"/>");
            builder.AppendLine("<Row><Cell ss:StyleID=\"Title\" ss:MergeAcross=\"5\"><Data ss:Type=\"String\">Отчет по складу</Data></Cell></Row>");
            builder.AppendLine($"<Row><Cell ss:MergeAcross=\"5\"><Data ss:Type=\"String\">Дата формирования: {EscapeXml(report.CreatedAt.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture))}</Data></Cell></Row>");
            builder.AppendLine("<Row/>");
            builder.AppendLine("<Row>");
            AppendHeaderCell(builder, "ID");
            AppendHeaderCell(builder, "Материал");
            AppendHeaderCell(builder, "Ед. изм.");
            AppendHeaderCell(builder, "Остаток");
            AppendHeaderCell(builder, "Минимум");
            AppendHeaderCell(builder, "Статус");
            builder.AppendLine("</Row>");

            foreach (var item in report.Items)
            {
                builder.AppendLine("<Row>");
                AppendCell(builder, item.InventoryItemId.ToString(CultureInfo.InvariantCulture));
                AppendCell(builder, item.ItemName);
                AppendCell(builder, item.UnitName);
                AppendNumberCell(builder, item.QuantityOnHand);
                AppendNumberCell(builder, item.MinQuantity);
                AppendCell(builder, item.StatusText);
                builder.AppendLine("</Row>");
            }

            builder.AppendLine("</Table></Worksheet></Workbook>");
            return builder.ToString();
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
                pages.Add(new List<string> { "Отчет", "Нет данных." });
            }

            return pages;
        }

        private static byte[] BuildPdf(List<List<string>> pages)
        {
            byte[] fontBytes = LoadFontBytes();
            var cmap = TrueTypeCMap.Load(fontBytes);
            var usedGlyphs = CollectUsedGlyphs(pages, cmap);

            var objects = new List<PdfObject>();
            objects.Add(PdfObject.Text("<< /Type /Catalog /Pages 2 0 R >>"));

            int pageCount = pages.Count;
            int firstPageObject = 8;
            string kids = string.Join(" ", Enumerable.Range(0, pageCount).Select(index => $"{firstPageObject + index * 2} 0 R"));
            objects.Add(PdfObject.Text($"<< /Type /Pages /Count {pageCount} /Kids [ {kids} ] >>"));
            objects.Add(PdfObject.Text("<< /Type /Font /Subtype /Type0 /BaseFont /NotoSans-Regular /Encoding /Identity-H /DescendantFonts [ 4 0 R ] /ToUnicode 7 0 R >>"));
            objects.Add(PdfObject.Text("<< /Type /Font /Subtype /CIDFontType2 /BaseFont /NotoSans-Regular /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor 5 0 R /CIDToGIDMap /Identity /DW 600 >>"));
            objects.Add(PdfObject.Text("<< /Type /FontDescriptor /FontName /NotoSans-Regular /Flags 4 /Ascent 1069 /Descent -293 /CapHeight 714 /ItalicAngle 0 /StemV 80 /FontBBox [-621 -389 2800 1067] /FontFile2 6 0 R >>"));
            objects.Add(PdfObject.Stream("<< /Length {0} /Length1 " + fontBytes.Length.ToString(CultureInfo.InvariantCulture) + " >>", fontBytes));
            objects.Add(PdfObject.Stream("<< /Length {0} >>", Encoding.ASCII.GetBytes(BuildToUnicodeCMap(usedGlyphs))));

            foreach (var page in pages)
            {
                int contentObject = objects.Count + 2;
                objects.Add(PdfObject.Text($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObject} 0 R >>"));
                objects.Add(PdfObject.Stream("<< /Length {0} >>", Encoding.ASCII.GetBytes(BuildContentStream(page, cmap))));
            }

            return WritePdf(objects);
        }

        private static Dictionary<int, int> CollectUsedGlyphs(IEnumerable<IEnumerable<string>> pages, TrueTypeCMap cmap)
        {
            var result = new Dictionary<int, int>();
            foreach (string line in pages.SelectMany(page => page))
            {
                foreach (char character in line ?? string.Empty)
                {
                    int glyphId = cmap.GetGlyphId(character);
                    if (!result.ContainsKey(glyphId))
                    {
                        result[glyphId] = character;
                    }
                }
            }

            return result;
        }

        private static string BuildToUnicodeCMap(Dictionary<int, int> glyphToUnicode)
        {
            var builder = new StringBuilder();
            builder.AppendLine("/CIDInit /ProcSet findresource begin");
            builder.AppendLine("12 dict begin");
            builder.AppendLine("begincmap");
            builder.AppendLine("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def");
            builder.AppendLine("/CMapName /NotoSans-Regular-UCS def");
            builder.AppendLine("/CMapType 2 def");
            builder.AppendLine("1 begincodespacerange");
            builder.AppendLine("<0000> <FFFF>");
            builder.AppendLine("endcodespacerange");

            var entries = glyphToUnicode.OrderBy(item => item.Key).ToList();
            for (int index = 0; index < entries.Count; index += 100)
            {
                var chunk = entries.Skip(index).Take(100).ToList();
                builder.AppendLine(chunk.Count.ToString(CultureInfo.InvariantCulture) + " beginbfchar");
                foreach (var entry in chunk)
                {
                    builder.AppendLine($"<{entry.Key:X4}> <{entry.Value:X4}>");
                }
                builder.AppendLine("endbfchar");
            }

            builder.AppendLine("endcmap");
            builder.AppendLine("CMapName currentdict /CMap defineresource pop");
            builder.AppendLine("end");
            builder.AppendLine("end");
            return builder.ToString();
        }

        private static string BuildContentStream(List<string> lines, TrueTypeCMap cmap)
        {
            var streamBuilder = new StringBuilder();
            streamBuilder.AppendLine("BT");
            streamBuilder.AppendLine("/F1 10 Tf");
            streamBuilder.AppendLine("50 800 Td");
            streamBuilder.AppendLine("14 TL");

            for (int i = 0; i < lines.Count; i++)
            {
                string hexLine = ToGlyphHex(lines[i], cmap);
                streamBuilder.AppendLine(i == 0
                    ? $"<{hexLine}> Tj"
                    : $"T* <{hexLine}> Tj");
            }

            streamBuilder.Append("ET");
            return streamBuilder.ToString();
        }

        private static string ToGlyphHex(string value, TrueTypeCMap cmap)
        {
            var builder = new StringBuilder();
            foreach (char character in value ?? string.Empty)
            {
                builder.Append(cmap.GetGlyphId(character).ToString("X4", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static byte[] WritePdf(List<PdfObject> objects)
        {
            using (var stream = new MemoryStream())
            {
                WriteAscii(stream, "%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
                var offsets = new List<long> { 0 };

                for (int index = 0; index < objects.Count; index++)
                {
                    offsets.Add(stream.Position);
                    WriteAscii(stream, (index + 1).ToString(CultureInfo.InvariantCulture) + " 0 obj\n");
                    stream.Write(objects[index].Bytes, 0, objects[index].Bytes.Length);
                    WriteAscii(stream, "\nendobj\n");
                }

                long xrefOffset = stream.Position;
                WriteAscii(stream, "xref\n");
                WriteAscii(stream, $"0 {objects.Count + 1}\n");
                WriteAscii(stream, "0000000000 65535 f \n");
                for (int index = 1; index < offsets.Count; index++)
                {
                    WriteAscii(stream, offsets[index].ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
                }

                WriteAscii(stream, "trailer\n");
                WriteAscii(stream, $"<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
                WriteAscii(stream, "startxref\n");
                WriteAscii(stream, xrefOffset.ToString(CultureInfo.InvariantCulture) + "\n");
                WriteAscii(stream, "%%EOF");
                return stream.ToArray();
            }
        }

        private static void WriteAscii(Stream stream, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static byte[] LoadFontBytes()
        {
            try
            {
                var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/Noto/NotoSans-Regular.ttf", UriKind.Absolute));
                if (resource != null)
                {
                    using (resource.Stream)
                    using (var buffer = new MemoryStream())
                    {
                        resource.Stream.CopyTo(buffer);
                        return buffer.ToArray();
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (UriFormatException)
            {
            }

            foreach (string candidatePath in new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Noto", "NotoSans-Regular.ttf"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Resources", "Noto", "NotoSans-Regular.ttf"),
                Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Noto", "NotoSans-Regular.ttf")
            })
            {
                string fullPath = Path.GetFullPath(candidatePath);
                if (File.Exists(fullPath))
                {
                    return File.ReadAllBytes(fullPath);
                }
            }

            throw new FileNotFoundException("Не найден шрифт для PDF-отчета.", "Resources/Noto/NotoSans-Regular.ttf");
        }

        private static string PadColumns(string value1, int width1, string value2, int width2, string value3, int width3, string value4, int width4)
        {
            return $"{(value1 ?? string.Empty).PadRight(width1)} {(value2 ?? string.Empty).PadRight(width2)} {(value3 ?? string.Empty).PadLeft(width3)} {(value4 ?? string.Empty).PadLeft(width4)}";
        }

        private static string TrimTo(string value, int length)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            return value.Length <= length ? value : value.Substring(0, length - 3) + "...";
        }

        private static void AppendHeaderCell(StringBuilder builder, string value)
        {
            builder.Append($"<Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">{EscapeXml(value)}</Data></Cell>");
        }

        private static void AppendCell(StringBuilder builder, string value)
        {
            builder.Append($"<Cell ss:StyleID=\"Cell\"><Data ss:Type=\"String\">{EscapeXml(value)}</Data></Cell>");
        }

        private static void AppendNumberCell(StringBuilder builder, decimal value)
        {
            builder.Append($"<Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{value.ToString(CultureInfo.InvariantCulture)}</Data></Cell>");
        }

        private static string EscapeXml(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private sealed class PdfObject
        {
            private PdfObject(byte[] bytes)
            {
                Bytes = bytes;
            }

            public byte[] Bytes { get; }

            public static PdfObject Text(string text)
            {
                return new PdfObject(Encoding.ASCII.GetBytes(text));
            }

            public static PdfObject Stream(string dictionaryTemplate, byte[] streamBytes)
            {
                string dictionary = string.Format(CultureInfo.InvariantCulture, dictionaryTemplate, streamBytes.Length);
                using (var stream = new MemoryStream())
                {
                    WriteAscii(stream, dictionary);
                    WriteAscii(stream, "\nstream\n");
                    stream.Write(streamBytes, 0, streamBytes.Length);
                    WriteAscii(stream, "\nendstream");
                    return new PdfObject(stream.ToArray());
                }
            }
        }

        private sealed class TrueTypeCMap
        {
            private readonly byte[] _fontBytes;
            private readonly int[] _endCodes;
            private readonly int[] _startCodes;
            private readonly short[] _idDeltas;
            private readonly int[] _idRangeOffsets;
            private readonly int _idRangeOffsetsStart;

            private TrueTypeCMap(byte[] fontBytes, int format4Offset)
            {
                _fontBytes = fontBytes;
                int segCount = ReadUInt16(fontBytes, format4Offset + 6) / 2;
                _endCodes = new int[segCount];
                _startCodes = new int[segCount];
                _idDeltas = new short[segCount];
                _idRangeOffsets = new int[segCount];

                int endCodesOffset = format4Offset + 14;
                int startCodesOffset = endCodesOffset + segCount * 2 + 2;
                int idDeltasOffset = startCodesOffset + segCount * 2;
                _idRangeOffsetsStart = idDeltasOffset + segCount * 2;

                for (int i = 0; i < segCount; i++)
                {
                    _endCodes[i] = ReadUInt16(fontBytes, endCodesOffset + i * 2);
                    _startCodes[i] = ReadUInt16(fontBytes, startCodesOffset + i * 2);
                    _idDeltas[i] = unchecked((short)ReadUInt16(fontBytes, idDeltasOffset + i * 2));
                    _idRangeOffsets[i] = ReadUInt16(fontBytes, _idRangeOffsetsStart + i * 2);
                }
            }

            public static TrueTypeCMap Load(byte[] fontBytes)
            {
                int tableDirectoryOffset = 12;
                int tableCount = ReadUInt16(fontBytes, 4);
                int cmapOffset = -1;

                for (int i = 0; i < tableCount; i++)
                {
                    int entryOffset = tableDirectoryOffset + i * 16;
                    string tag = Encoding.ASCII.GetString(fontBytes, entryOffset, 4);
                    if (tag == "cmap")
                    {
                        cmapOffset = ReadInt32(fontBytes, entryOffset + 8);
                        break;
                    }
                }

                if (cmapOffset < 0)
                {
                    throw new InvalidOperationException("В шрифте не найдена таблица cmap.");
                }

                int subtableCount = ReadUInt16(fontBytes, cmapOffset + 2);
                int format4Offset = -1;
                for (int i = 0; i < subtableCount; i++)
                {
                    int recordOffset = cmapOffset + 4 + i * 8;
                    int platformId = ReadUInt16(fontBytes, recordOffset);
                    int encodingId = ReadUInt16(fontBytes, recordOffset + 2);
                    int subtableOffset = cmapOffset + ReadInt32(fontBytes, recordOffset + 4);
                    int format = ReadUInt16(fontBytes, subtableOffset);

                    if (format == 4 && platformId == 3 && (encodingId == 1 || encodingId == 0))
                    {
                        format4Offset = subtableOffset;
                        break;
                    }

                    if (format == 4 && format4Offset < 0)
                    {
                        format4Offset = subtableOffset;
                    }
                }

                if (format4Offset < 0)
                {
                    throw new InvalidOperationException("В шрифте не найдена Unicode cmap format 4.");
                }

                return new TrueTypeCMap(fontBytes, format4Offset);
            }

            public int GetGlyphId(char character)
            {
                int code = character;
                for (int i = 0; i < _endCodes.Length; i++)
                {
                    if (code > _endCodes[i])
                    {
                        continue;
                    }

                    if (code < _startCodes[i])
                    {
                        return 0;
                    }

                    if (_idRangeOffsets[i] == 0)
                    {
                        return (code + _idDeltas[i]) & 0xFFFF;
                    }

                    int glyphOffset = _idRangeOffsetsStart + i * 2 + _idRangeOffsets[i] + (code - _startCodes[i]) * 2;
                    int glyphId = ReadUInt16(_fontBytes, glyphOffset);
                    if (glyphId == 0)
                    {
                        return 0;
                    }

                    return (glyphId + _idDeltas[i]) & 0xFFFF;
                }

                return 0;
            }

            private static int ReadUInt16(byte[] bytes, int offset)
            {
                return (bytes[offset] << 8) | bytes[offset + 1];
            }

            private static int ReadInt32(byte[] bytes, int offset)
            {
                return (bytes[offset] << 24) |
                       (bytes[offset + 1] << 16) |
                       (bytes[offset + 2] << 8) |
                       bytes[offset + 3];
            }
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

    public sealed class InventoryReportData
    {
        public DateTime CreatedAt { get; set; }
        public List<InventoryReportLine> Items { get; set; } = new List<InventoryReportLine>();
    }

    public sealed class InventoryReportLine
    {
        public int InventoryItemId { get; set; }
        public string ItemName { get; set; }
        public string UnitName { get; set; }
        public decimal QuantityOnHand { get; set; }
        public decimal MinQuantity { get; set; }
        public string StatusText { get; set; }
    }
}
