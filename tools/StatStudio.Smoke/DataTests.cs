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

        var quoted = WorksheetIo.ReadCsv(new StringReader("name,note\n\"Doe, J\",\"a \"\"b\"\" c\""));
        Check.Equal(quoted.Columns[0][0]!, "Doe, J", "quoted field with comma");
        Check.Equal(quoted.Columns[1][0]!, "a \"b\" c", "escaped quotes");

        Check.Section("Excel (.xlsx) round-trip");
        var xw = new Worksheet { Name = "X" };
        var v1 = xw.AddColumn("Val");
        foreach (var v in new[] { "1.5", "2.5", "3.5" }) v1.Add(v);
        var v2 = xw.AddColumn("Tag", ColumnType.Text);
        foreach (var v in new[] { "p", "q", "r" }) v2.Add(v);

        var xlsx = Path.Combine(Path.GetTempPath(), "statstudio_smoke.xlsx");
        WorksheetIo.WriteXlsx(xw, xlsx);
        var xback = WorksheetIo.ReadXlsx(xlsx);
        Check.Equal(xback.ColumnCount, 2, "xlsx columns");
        Check.Equal(xback.Columns[0].Name, "Val", "xlsx header");
        Check.Close(xback.Columns[0].NumericValues()[1], 2.5, "xlsx numeric value");
        Check.Equal(xback.Columns[1][0]!, "p", "xlsx text value");
        File.Delete(xlsx);

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

        Check.Section("Project (.ssproj) round-trip");
        var proj = Path.Combine(Path.GetTempPath(), "statstudio_smoke.ssproj");
        ProjectStore.Save(xw, proj);
        var pback = ProjectStore.Load(proj);
        Check.Equal(pback.ColumnCount, 2, "ssproj columns");
        Check.Equal(pback.Name, "X", "ssproj name");
        Check.Equal(pback.Columns[1][0]!, "p", "ssproj cell");
        File.Delete(proj);
    }
}
