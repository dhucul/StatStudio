using System.Data;
using CoreData = StatStudio.Core.Data;

namespace StatStudio.Wpf;

/// <summary>
/// Bridges the Core <see cref="CoreData.Worksheet"/> (the single source of truth
/// for analyses) and the WPF <see cref="DataTable"/> that backs the editable grid.
/// The grid stores everything as strings; numeric/text typing is sniffed when a
/// Core worksheet is rebuilt for an analysis.
/// </summary>
internal static class WorksheetGrid
{
    public static DataTable ToDataTable(CoreData.Worksheet ws)
    {
        var table = new DataTable(ws.Name);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in ws.Columns)
        {
            string baseName = string.IsNullOrEmpty(col.Name) ? "C" : col.Name;
            string name = baseName;
            int n = 2;
            while (!used.Add(name)) name = $"{baseName}_{n++}";
            var dc = table.Columns.Add(name, typeof(string));
            dc.Caption = name;
        }

        int rows = ws.RowCount;
        for (int r = 0; r < rows; r++)
        {
            var row = table.NewRow();
            for (int j = 0; j < ws.ColumnCount; j++)
                row[j] = ws.Columns[j][r] ?? string.Empty;
            table.Rows.Add(row);
        }
        return table;
    }

    public static CoreData.Worksheet ToWorksheet(DataTable table)
    {
        var ws = new CoreData.Worksheet
        {
            Name = string.IsNullOrEmpty(table.TableName) ? "Worksheet 1" : table.TableName,
        };

        var rows = table.Rows.Cast<DataRow>().Where(r => r.RowState != DataRowState.Deleted).ToList();

        // Index of the last row that holds any value, so trailing blank rows (the empty
        // editing padding) are dropped and don't count as missing observations. Interior
        // blank cells are preserved as genuine missing values.
        // Scanned from the bottom: this runs on every analysis, and the forward scan visited
        // every cell of the grid even though only the final non-empty row is needed.
        int last = -1;
        for (int r = rows.Count - 1; r >= 0 && last < 0; r--)
            foreach (DataColumn dc in table.Columns)
            {
                var v = rows[r][dc];
                if (v != DBNull.Value && !string.IsNullOrEmpty(v?.ToString())) { last = r; break; }
            }

        foreach (DataColumn dc in table.Columns)
        {
            string name = dc.ColumnName;
            var col = ws.AddColumn(name);
            for (int r = 0; r <= last; r++)
            {
                var v = rows[r][dc];
                col.Add(v == DBNull.Value ? null : v?.ToString());
            }
            col.Type = col.LooksNumeric() ? CoreData.ColumnType.Numeric : CoreData.ColumnType.Text;
        }
        return ws;
    }

    public static DataTable NewEmpty(int cols = 8, int rows = 20)
    {
        var ws = new CoreData.Worksheet { Name = "Worksheet 1" };
        for (int j = 1; j <= cols; j++) ws.AddColumn(CoreData.Worksheet.DefaultName(j));
        var table = ToDataTable(ws);
        for (int r = 0; r < rows; r++) table.Rows.Add(table.NewRow());
        return table;
    }
}
