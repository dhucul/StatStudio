using StatStudio.Core.Data;

namespace StatStudio.Smoke;

internal static class DataTests
{
    public static void Run()
    {
        Check.Section("Worksheet CSV round-trip");
        var ws = new Worksheet { Name = "T" };
        var a = ws.AddColumn("Temp");
        foreach (var v in new[] { "20.5", "21.0", "*", "19.8" }) a.Add(v);
        var b = ws.AddColumn("Site");
        foreach (var v in new[] { "North", "South", "East", "West" }) b.Add(v);

        var sw = new StringWriter();
        WorksheetIo.WriteCsv(ws, sw);
        var back = WorksheetIo.ReadCsv(new StringReader(sw.ToString()));

        Check.Equal(back.ColumnCount, 2, "column count preserved");
        Check.Equal(back.Columns[0].Name, "Temp", "header 0 name");
        Check.Equal(back.Columns[1].Name, "Site", "header 1 name");
        Check.True(back.Columns[0].LooksNumeric(), "Temp detected numeric");
        Check.True(!back.Columns[1].LooksNumeric(), "Site detected text");

        var nums = back.Columns[0].NumericValues();
        Check.Equal(nums.Length, 3, "Temp non-missing count (1 missing skipped)");
        Check.Close(nums[0], 20.5, "Temp[0]");
        Check.Equal(back.Columns[0].MissingCount(), 1, "Temp missing count");

        Check.Section("Header / delimiter detection");
        var noHeader = WorksheetIo.ReadCsv(new StringReader("1,2\n3,4\n5,6"));
        Check.Equal(noHeader.Columns[0].Name, "C1", "no-header -> C1 naming");
        Check.Equal(noHeader.RowCount, 3, "no-header row count");

        var tsv = WorksheetIo.ReadCsv(new StringReader("x\ty\n1\t2\n3\t4"));
        Check.Equal(tsv.ColumnCount, 2, "tab delimiter detected");
        Check.Equal(tsv.Columns[0].Name, "x", "tab header name");

        var tsvPath = Path.Combine(Path.GetTempPath(), $"statstudio_smoke_{Guid.NewGuid():N}.tsv");
        try
        {
            WorksheetIo.WriteCsv(ws, tsvPath, '\t');
            Check.True(File.ReadLines(tsvPath).First().Contains('\t'), "TSV path export uses tabs");
            Check.Equal(WorksheetIo.ReadCsv(tsvPath).ColumnCount, 2, "TSV path export round-trip");
        }
        finally { if (File.Exists(tsvPath)) File.Delete(tsvPath); }

        var quoted = WorksheetIo.ReadCsv(new StringReader("name,note\n\"Doe, J\",\"a \"\"b\"\" c\""));
        Check.Equal(quoted.Columns[0][0]!, "Doe, J", "quoted field with comma");
        Check.Equal(quoted.Columns[1][0]!, "a \"b\" c", "escaped quotes");

        var multilineSource = new Worksheet { Name = "Multiline" };
        multilineSource.AddColumn("Text", ColumnType.Text).Add("first line\nsecond line");
        multilineSource.AddColumn("Value").Add("42");
        var multilineText = new StringWriter();
        WorksheetIo.WriteCsv(multilineSource, multilineText);
        var multiline = WorksheetIo.ReadCsv(new StringReader(multilineText.ToString()));
        Check.Equal(multiline.RowCount, 1, "quoted newline remains one record");
        Check.Equal(multiline.Columns[0][0]!, "first line\nsecond line", "quoted newline round-trip");

        var quotedDelimiter = WorksheetIo.ReadCsv(new StringReader("\"label,detail\";value\nalpha;1"));
        Check.Equal(quotedDelimiter.ColumnCount, 2, "delimiter detection ignores quoted separators");
        Check.Equal(quotedDelimiter.Columns[0].Name, "label,detail", "quoted delimiter header");

        var finite = new Worksheet().AddColumn("Finite boundary");
        foreach (var value in new[] { "1", "NaN", "Infinity" }) finite.Add(value);
        Check.Equal(finite.NumericValues().Length, 1, "non-finite worksheet values are excluded");
        Check.True(!finite.LooksNumeric(), "non-finite cells do not make a numeric column");
        Check.Throws<ArgumentOutOfRangeException>(() => finite.Set(-1, "2"), "negative row index rejected");

        Check.Section("Excel (.xlsx) round-trip");
        var xw = new Worksheet { Name = "X" };
        var v1 = xw.AddColumn("Val");
        foreach (var v in new[] { "1.5", "2.5", "3.5" }) v1.Add(v);
        var v2 = xw.AddColumn("Tag", ColumnType.Text);
        foreach (var v in new[] { "p", "q", "r" }) v2.Add(v);

        var xlsx = Path.Combine(Path.GetTempPath(), $"statstudio_smoke_{Guid.NewGuid():N}.xlsx");
        try
        {
            File.WriteAllText(xlsx, "old");
            WorksheetIo.WriteXlsx(xw, xlsx);
            var xback = WorksheetIo.ReadXlsx(xlsx);
            Check.Equal(xback.ColumnCount, 2, "xlsx columns");
            Check.Equal(xback.Columns[0].Name, "Val", "xlsx header");
            Check.Close(xback.Columns[0].NumericValues()[1], 2.5, "xlsx numeric value");
            Check.Equal(xback.Columns[1][0]!, "p", "xlsx text value");
        }
        finally { if (File.Exists(xlsx)) File.Delete(xlsx); }

        Check.Section("Worksheet calculator");
        var cw2 = new Worksheet();
        var c1 = cw2.AddColumn("C1"); foreach (var v in new[] { "1", "2", "3" }) c1.Add(v);
        var c2 = cw2.AddColumn("C2"); foreach (var v in new[] { "10", "20", "30" }) c2.Add(v);
        var c3 = cw2.AddColumn("Sq"); foreach (var v in new[] { "1", "4", "9" }) c3.Add(v);

        Check.Close(Calculator.Evaluate("C1 + C2", cw2)[1], 22, "C1+C2 row 2");
        Check.Close(Calculator.Evaluate("C1 * 2 + 1", cw2)[2], 7, "C1*2+1 row 3");
        Check.Close(Calculator.Evaluate("C1 ^ 2", cw2)[2], 9, "C1^2 row 3");
        Check.Close(Calculator.Evaluate("SQRT(Sq)", cw2)[2], 3, "SQRT(Sq) row 3");
        Check.Close(Calculator.Evaluate("1 + 2 * 3", cw2)[0], 7, "operator precedence");
        Check.Close(Calculator.Evaluate("MEAN(C1)", cw2)[0], 2, "MEAN(C1) broadcast");
        Check.Close(Calculator.Evaluate("LOG(EXP(C1))", cw2)[1], 2, "LOG(EXP(C1)) row 2");
        Check.Close(Calculator.Evaluate("(C1 + C2) / 2", cw2)[0], 5.5, "parentheses + divide");
        Check.Close(Calculator.Evaluate("-C1 + 5", cw2)[0], 4, "unary minus");
        Check.Close(Calculator.Evaluate("'Sq' / C1", cw2)[2], 3, "quoted column name");
        Check.Close(Calculator.Evaluate("-2^2", cw2)[0], -4, "unary minus looser than ^ : -(2^2)");
        Check.Close(Calculator.Evaluate("2^-2", cw2)[0], 0.25, "negative exponent");
        Check.Close(Calculator.Evaluate("2 * -3", cw2)[0], -6, "unary minus after operator");
        Check.Close(Calculator.Evaluate("2^3^2", cw2)[0], 512, "power is right-associative");
        Check.Throws<FormatException>(() => Calculator.Evaluate("''", cw2), "empty quoted calculator identifier rejected");

        Check.Section("Sample datasets build");
        foreach (var ds in SampleData.All)
        {
            var dsw = ds.Build();
            Check.True(dsw.ColumnCount > 0 && dsw.RowCount > 0, $"{ds.Name} non-empty ({dsw.ColumnCount}x{dsw.RowCount})");
        }
        var conc = SampleData.All.First(d => d.Name.StartsWith("Concrete")).Build();
        double rowSum = conc.Find("Cement")![0]!.Length > 0
            ? double.Parse(conc.Find("Cement")![0]!, System.Globalization.CultureInfo.InvariantCulture)
            + double.Parse(conc.Find("Water")![0]!, System.Globalization.CultureInfo.InvariantCulture)
            + double.Parse(conc.Find("Aggregate")![0]!, System.Globalization.CultureInfo.InvariantCulture) : 0;
        Check.Close(rowSum, 1.0, "concrete mixture row sums to 1", 1e-3);

        Check.Section("Project (.ssproj) round-trip");
        var proj = Path.Combine(Path.GetTempPath(), $"statstudio_smoke_{Guid.NewGuid():N}.ssproj");
        try
        {
            File.WriteAllText(proj, "old");
            ProjectStore.Save(xw, proj);
            var pback = ProjectStore.Load(proj);
            Check.Equal(pback.ColumnCount, 2, "ssproj columns");
            Check.Equal(pback.Name, "X", "ssproj name");
            Check.Equal(pback.Columns[1][0]!, "p", "ssproj cell");

            File.WriteAllText(proj, """{"Name":"Bad","Columns":null}""");
            Check.Throws<InvalidDataException>(() => ProjectStore.Load(proj), "null project columns rejected");
        }
        finally { if (File.Exists(proj)) File.Delete(proj); }
    }
}
