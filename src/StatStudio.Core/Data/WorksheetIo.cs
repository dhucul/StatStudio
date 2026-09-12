using System.Text;
using ClosedXML.Excel;
using StatStudio.Core.Statistics;

namespace StatStudio.Core.Data;

/// <summary>
/// Reads and writes a <see cref="Worksheet"/> as delimited text (CSV / TSV).
/// Delimiter and a header row are auto-detected. (xlsx support is added in Phase 5.)
/// </summary>
public static class WorksheetIo
{
    private static readonly char[] Delimiters = { ',', '\t', ';' };

    /// <summary>Above this row count the xlsx writer skips column auto-fit, which measures every cell.</summary>
    private const int AutoFitRowLimit = 5_000;

    /// <summary>
    /// Leading characters that make a spreadsheet treat a cell as a formula. Imported data is
    /// untrusted, so exported cells are prefixed with an apostrophe to keep them literal text
    /// (formula injection: =cmd|'/c ...'!A1, @SUM(...), +/-DDE payloads).
    /// </summary>
    private const string FormulaLeadIn = "=+-@\t\r";

    /// <summary>
    /// True when a cell would be evaluated as a formula. Numbers are exempt — "-5.2" and "+3e4"
    /// start with a lead-in character but are ordinary values, and must round-trip untouched.
    /// </summary>
    private static bool NeedsFormulaGuard(string value) =>
        value.Length > 0 &&
        FormulaLeadIn.IndexOf(value[0]) >= 0 &&
        !DataColumn.TryParse(value, out _);

    /// <summary>Reverses <see cref="NeedsFormulaGuard"/>'s apostrophe so our own exports re-import unchanged.</summary>
    private static string? StripFormulaGuard(string? value) =>
        value is { Length: >= 2 } && value[0] == '\'' && (value[1] == '\'' || FormulaLeadIn.IndexOf(value[1]) >= 0)
            ? value[1..]
            : value;

    public static Worksheet ReadCsv(string path, bool? hasHeader = null, bool decodeFormulaGuards = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireFileSize(path);
        using var reader = new StreamReader(path);
        var ws = ReadCsv(reader, hasHeader, decodeFormulaGuards, cancellationToken);
        ws.Name = Path.GetFileNameWithoutExtension(path);
        return ws;
    }

    public static Worksheet ReadCsv(TextReader reader, bool? hasHeader = null, bool decodeFormulaGuards = false, CancellationToken cancellationToken = default)
    {
        var text = ReadText(reader, cancellationToken);
        if (text.Length == 0) return new Worksheet();

        char delim = DetectDelimiter(FirstRecord(text));
        var rows = ParseRecords(text, delim, cancellationToken);
        if (rows.Count == 0) return new Worksheet();

        bool header = hasHeader ?? LooksLikeHeader(rows);
        return FromRows(rows, header, decodeFormulaGuards, cancellationToken);
    }

    public static void WriteCsv(Worksheet ws, string path, char delim = ',', CancellationToken cancellationToken = default)
    {
        string temporaryPath = AtomicFile.CreateTemporaryPath(path);
        try
        {
            using (var writer = new StreamWriter(temporaryPath, false, new UTF8Encoding(false)))
                WriteCsv(ws, writer, delim, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            AtomicFile.Commit(temporaryPath, path);
        }
        finally
        {
            AtomicFile.DeleteIfPresent(temporaryPath);
        }
    }

    public static void WriteCsv(Worksheet ws, TextWriter writer, char delim = ',', CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireDimensions(ws.RowCount, ws.ColumnCount);
        int rowCount = ws.RowCount;   // Worksheet.RowCount is a Max() over every column — never re-evaluate per row.
        writer.WriteLine(string.Join(delim, ws.Columns.Select(c => Escape(c.Name, delim))));
        for (int r = 0; r < rowCount; r++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cells = ws.Columns.Select(c => Escape(c[r] ?? string.Empty, delim));
            writer.WriteLine(string.Join(delim, cells));
        }
    }

    // ---- Excel (.xlsx) -----------------------------------------------------

    public static Worksheet ReadXlsx(string path, bool? hasHeader = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireFileSize(path);
        using var wb = new XLWorkbook(path);
        var sheet = wb.Worksheets.FirstOrDefault()
                    ?? throw new InvalidDataException("The workbook contains no worksheets.");
        var range = sheet.RangeUsed();
        var result = new Worksheet { Name = Path.GetFileNameWithoutExtension(path) };
        if (range is null) return result;

        int nRows = range.RowCount(), nCols = range.ColumnCount();
        RequireDimensions(Math.Max(0, nRows - 1), nCols);
        var rows = new List<List<string>>(nRows);
        for (int r = 1; r <= nRows; r++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cells = new List<string>(nCols);
            for (int c = 1; c <= nCols; c++) cells.Add(range.Cell(r, c).GetString());
            rows.Add(cells);
        }
        bool header = hasHeader ?? LooksLikeHeader(rows);
        var ws = FromRows(rows, header, cancellationToken: cancellationToken);
        ws.Name = result.Name;
        return ws;
    }

    public static void WriteXlsx(Worksheet ws, string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireDimensions(ws.RowCount, ws.ColumnCount);
        string temporaryPath = AtomicFile.CreateTemporaryPath(path);
        try
        {
            using var wb = new XLWorkbook();
            var sheet = wb.AddWorksheet(SafeSheetName(ws.Name));
            int rowCount = ws.RowCount, columnCount = ws.ColumnCount;
            for (int j = 0; j < columnCount; j++)
                WriteLiteral(sheet.Cell(1, j + 1), ws.Columns[j].Name);

            for (int r = 0; r < rowCount; r++)
                for (int j = 0; j < columnCount; j++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var raw = ws.Columns[j][r];
                    if (string.IsNullOrEmpty(raw)) continue;
                    if (ws.Columns[j].Type == ColumnType.Numeric && DataColumn.TryParse(raw, out var num))
                    {
                        sheet.Cell(r + 2, j + 1).Value = num;
                    }
                    else
                    {
                        var cell = sheet.Cell(r + 2, j + 1);
                        WriteLiteral(cell, raw);
                    }
                }
            sheet.Row(1).Style.Font.Bold = true;
            // AdjustToContents measures every cell in every column — the dominant cost on a large export.
            if (rowCount <= AutoFitRowLimit) sheet.Columns().AdjustToContents();
            wb.SaveAs(temporaryPath);
            cancellationToken.ThrowIfCancellationRequested();
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

    private static void WriteLiteral(IXLCell cell, string text)
    {
        // ClosedXML's value setter consumes one leading apostrophe. Supply an
        // extra one to preserve literal text; quote-prefix is stored as metadata.
        cell.SetValue(text.StartsWith('\'') ? "'" + text : text);
        if (NeedsFormulaGuard(text) || text.StartsWith('\'')) cell.Style.IncludeQuotePrefix = true;
    }

    // ---- helpers -----------------------------------------------------------

    private static Worksheet FromRows(List<List<string>> rows, bool header, bool decodeFormulaGuards = false, CancellationToken cancellationToken = default)
    {
        var ws = new Worksheet();
        if (rows.Count == 0) return ws;

        int cols = rows.Max(r => r.Count);
        RequireDimensions(rows.Count - (header ? 1 : 0), cols);
        int dataStart = header ? 1 : 0;

        for (int j = 0; j < cols; j++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = header && j < rows[0].Count && rows[0][j].Length > 0
                ? (decodeFormulaGuards ? StripFormulaGuard(rows[0][j])! : rows[0][j])
                : Worksheet.DefaultName(j + 1);
            var col = ws.AddColumn(name);
            for (int r = dataStart; r < rows.Count; r++)
                col.Add(j < rows[r].Count ? (decodeFormulaGuards ? StripFormulaGuard(rows[r][j]) : rows[r][j]) : null);
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
        if (rows.Count == 0) return false;
        // Missing values are observations, never evidence of a heading. Ambiguous
        // text/numeric headings can be selected explicitly through hasHeader.
        if (rows[0].Any(cell => string.IsNullOrWhiteSpace(cell) || cell.Trim() == "*")) return false;
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
    private static List<List<string>> ParseRecords(string text, char delim, CancellationToken cancellationToken)
    {
        var rows = new List<List<string>>();
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        long cells = 0;

        void EndField()
        {
            if (++cells > AnalysisLimits.MaxWorksheetCells + AnalysisLimits.MaxWorksheetColumns)
                throw new InvalidDataException("The file exceeds the 2,000,000-cell limit.");
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
            if (i % 8192 == 0) cancellationToken.ThrowIfCancellationRequested();
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
        // Escape an original apostrophe too, so decoding an explicitly identified
        // StatStudio CSV export cannot collapse two distinct original strings.
        if (NeedsFormulaGuard(value) || value.StartsWith('\''))
            return "\"'" + value.Replace("\"", "\"\"") + "\"";
        if (value.IndexOf(delim) < 0 && value.IndexOf('"') < 0 &&
            value.IndexOf('\n') < 0 && value.IndexOf('\r') < 0)
            return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    internal static void RequireFileSize(string path)
    {
        if (new FileInfo(path).Length > 64L * 1024 * 1024)
            throw new InvalidDataException("Data files are limited to 64 MB.");
    }

    internal static void RequireDimensions(int rows, int columns)
    {
        if (columns > AnalysisLimits.MaxWorksheetColumns)
            throw new InvalidDataException("Worksheets are limited to 512 columns.");
        if ((long)Math.Max(1, rows) * columns > AnalysisLimits.MaxWorksheetCells)
            throw new InvalidDataException("The worksheet exceeds the 2,000,000-cell limit.");
    }

    private static string ReadText(TextReader reader, CancellationToken cancellationToken)
    {
        var result = new StringBuilder();
        var buffer = new char[8192];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = reader.Read(buffer, 0, buffer.Length);
            if (count == 0) return result.ToString();
            if ((long)result.Length + count > 64L * 1024 * 1024)
                throw new InvalidDataException("Delimited text is limited to 64 million characters.");
            result.Append(buffer, 0, count);
        }
    }
}
