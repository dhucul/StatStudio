using System.Text;
using ClosedXML.Excel;

namespace StatStudio.Core.Data;

/// <summary>
/// Reads and writes a <see cref="Worksheet"/> as delimited text (CSV / TSV).
/// Delimiter and a header row are auto-detected. (xlsx support is added in Phase 5.)
/// </summary>
public static class WorksheetIo
{
    private static readonly char[] Delimiters = { ',', '\t', ';' };

    public static Worksheet ReadCsv(string path, bool? hasHeader = null)
    {
        using var reader = new StreamReader(path);
        var ws = ReadCsv(reader, hasHeader);
        ws.Name = Path.GetFileNameWithoutExtension(path);
        return ws;
    }

    public static Worksheet ReadCsv(TextReader reader, bool? hasHeader = null)
    {
        var text = reader.ReadToEnd();
        if (text.Length == 0) return new Worksheet();

        char delim = DetectDelimiter(FirstRecord(text));
        var rows = ParseRecords(text, delim);
        if (rows.Count == 0) return new Worksheet();

        bool header = hasHeader ?? LooksLikeHeader(rows);
        return FromRows(rows, header);
    }

    public static void WriteCsv(Worksheet ws, string path, char delim = ',')
    {
        string temporaryPath = AtomicFile.CreateTemporaryPath(path);
        try
        {
            using (var writer = new StreamWriter(temporaryPath, false, new UTF8Encoding(false)))
                WriteCsv(ws, writer, delim);
            AtomicFile.Commit(temporaryPath, path);
        }
        finally
        {
            AtomicFile.DeleteIfPresent(temporaryPath);
        }
    }

    public static void WriteCsv(Worksheet ws, TextWriter writer, char delim = ',')
    {
        writer.WriteLine(string.Join(delim, ws.Columns.Select(c => Escape(c.Name, delim))));
        for (int r = 0; r < ws.RowCount; r++)
        {
            var cells = ws.Columns.Select(c => Escape(c[r] ?? string.Empty, delim));
            writer.WriteLine(string.Join(delim, cells));
        }
    }

    // ---- Excel (.xlsx) -----------------------------------------------------

    public static Worksheet ReadXlsx(string path, bool? hasHeader = null)
    {
        using var wb = new XLWorkbook(path);
        var sheet = wb.Worksheets.First();
        var range = sheet.RangeUsed();
        var result = new Worksheet { Name = Path.GetFileNameWithoutExtension(path) };
        if (range is null) return result;

        int nRows = range.RowCount(), nCols = range.ColumnCount();
        var rows = new List<List<string>>(nRows);
        for (int r = 1; r <= nRows; r++)
        {
            var cells = new List<string>(nCols);
            for (int c = 1; c <= nCols; c++) cells.Add(range.Cell(r, c).GetString());
            rows.Add(cells);
        }
        bool header = hasHeader ?? LooksLikeHeader(rows);
        var ws = FromRows(rows, header);
        ws.Name = result.Name;
        return ws;
    }

    public static void WriteXlsx(Worksheet ws, string path)
    {
        string temporaryPath = AtomicFile.CreateTemporaryPath(path);
        try
        {
            using var wb = new XLWorkbook();
            var sheet = wb.AddWorksheet(SafeSheetName(ws.Name));
            for (int j = 0; j < ws.ColumnCount; j++)
                sheet.Cell(1, j + 1).Value = ws.Columns[j].Name;

            for (int r = 0; r < ws.RowCount; r++)
                for (int j = 0; j < ws.ColumnCount; j++)
                {
                    var raw = ws.Columns[j][r];
                    if (string.IsNullOrEmpty(raw)) continue;
                    if (ws.Columns[j].Type == ColumnType.Numeric && DataColumn.TryParse(raw, out var num))
                        sheet.Cell(r + 2, j + 1).Value = num;
                    else
                        sheet.Cell(r + 2, j + 1).Value = raw;
                }
            sheet.Row(1).Style.Font.Bold = true;
            sheet.Columns().AdjustToContents();
            wb.SaveAs(temporaryPath);
            AtomicFile.Commit(temporaryPath, path);
        }
        finally
        {
            AtomicFile.DeleteIfPresent(temporaryPath);
        }
    }

    private static string SafeSheetName(string name)
    {
        var clean = new string((name ?? "Sheet1").Where(c => "[]:*?/\\".IndexOf(c) < 0).ToArray());
        if (clean.Length == 0) clean = "Sheet1";
        return clean.Length > 31 ? clean[..31] : clean;
    }

    // ---- helpers -----------------------------------------------------------

    private static Worksheet FromRows(List<List<string>> rows, bool header)
    {
        var ws = new Worksheet();
        if (rows.Count == 0) return ws;

        int cols = rows.Max(r => r.Count);
        int dataStart = header ? 1 : 0;

        for (int j = 0; j < cols; j++)
        {
            string name = header && j < rows[0].Count && rows[0][j].Length > 0
                ? rows[0][j]
                : Worksheet.DefaultName(j + 1);
            var col = ws.AddColumn(name);
            for (int r = dataStart; r < rows.Count; r++)
                col.Add(j < rows[r].Count ? rows[r][j] : null);
            col.Type = col.LooksNumeric() ? ColumnType.Numeric : ColumnType.Text;
        }
        return ws;
    }

    /// <summary>
    /// The first row is treated as a header when either (a) some column has a
    /// non-numeric heading sitting above numeric data, or (b) the whole first row
    /// is non-numeric labels (the conventional default, also covers all-text data).
    /// </summary>
    private static bool LooksLikeHeader(List<List<string>> rows)
    {
        if (rows.Count < 2) return false;
        int cols = rows.Max(r => r.Count);

        // (a) strong signal: a text heading above a numeric column.
        for (int j = 0; j < cols; j++)
        {
            if (j >= rows[0].Count) continue;
            bool headerNonNumeric = rows[0][j].Length > 0 && !DataColumn.TryParse(rows[0][j], out _);
            if (!headerNonNumeric) continue;
            for (int r = 1; r < rows.Count; r++)
                if (j < rows[r].Count && DataColumn.TryParse(rows[r][j], out _)) return true;
        }

        // (b) first row is entirely non-numeric labels.
        bool anyValue = false;
        foreach (var cell in rows[0])
        {
            if (cell.Length == 0) continue;
            anyValue = true;
            if (DataColumn.TryParse(cell, out _)) return false;
        }
        return anyValue;
    }

    private static char DetectDelimiter(string line)
    {
        char best = ',';
        int bestCount = -1;
        foreach (var d in Delimiters)
        {
            int c = CountOutsideQuotes(line, d);
            if (c > bestCount) { bestCount = c; best = d; }
        }
        return best;
    }

    private static string FirstRecord(string text)
    {
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"') { i++; continue; }
                inQuotes = !inQuotes;
            }
            else if (!inQuotes && (c == '\r' || c == '\n'))
            {
                return text[..i];
            }
        }
        return text;
    }

    private static int CountOutsideQuotes(string record, char delim)
    {
        bool inQuotes = false;
        int count = 0;
        for (int i = 0; i < record.Length; i++)
        {
            char c = record[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < record.Length && record[i + 1] == '"') { i++; continue; }
                inQuotes = !inQuotes;
            }
            else if (!inQuotes && c == delim) count++;
        }
        return count;
    }

    /// <summary>Parses delimited records while preserving CR/LF inside quoted fields.</summary>
    private static List<List<string>> ParseRecords(string text, char delim)
    {
        var rows = new List<List<string>>();
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        void EndField()
        {
            fields.Add(sb.ToString());
            sb.Clear();
        }

        void EndRecord()
        {
            EndField();
            rows.Add(new List<string>(fields));
            fields.Clear();
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delim)
            {
                EndField();
            }
            else if (c == '\r' || c == '\n')
            {
                EndRecord();
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            }
            else
            {
                sb.Append(c);
            }
        }

        if (inQuotes) throw new FormatException("Delimited text contains an unterminated quoted field.");
        if (sb.Length > 0 || fields.Count > 0 || (text.Length > 0 && text[^1] != '\r' && text[^1] != '\n'))
            EndRecord();
        while (rows.Count > 0 && rows[^1].Count == 1 && rows[^1][0].Length == 0)
            rows.RemoveAt(rows.Count - 1);
        return rows;
    }

    private static string Escape(string value, char delim)
    {
        if (value.IndexOf(delim) < 0 && value.IndexOf('"') < 0 &&
            value.IndexOf('\n') < 0 && value.IndexOf('\r') < 0)
            return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
