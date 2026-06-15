namespace StatStudio.Core.Data;

/// <summary>A Minitab-style worksheet: an ordered collection of named columns (C1, C2, ...).</summary>
public sealed class Worksheet
{
    private readonly List<DataColumn> _columns = new();

    public string Name { get; set; } = "Worksheet 1";

    public IReadOnlyList<DataColumn> Columns => _columns;
    public int ColumnCount => _columns.Count;
    public int RowCount => _columns.Count == 0 ? 0 : _columns.Max(c => c.Count);

    public DataColumn this[int index] => _columns[index];

    public DataColumn AddColumn(string? name = null, ColumnType type = ColumnType.Numeric)
    {
        var col = new DataColumn(name ?? DefaultName(_columns.Count + 1), type);
        _columns.Add(col);
        return col;
    }

    public void AddColumn(DataColumn column) => _columns.Add(column);

    public DataColumn? Find(string name) =>
        _columns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    public int IndexOf(string name) =>
        _columns.FindIndex(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    public void RemoveAt(int index) => _columns.RemoveAt(index);
    public void Clear() => _columns.Clear();

    /// <summary>Columns whose non-missing cells are all numeric.</summary>
    public IEnumerable<DataColumn> NumericColumns() => _columns.Where(c => c.LooksNumeric());

    /// <summary>The Minitab default name for the 1-based column position (C1, C2, ...).</summary>
    public static string DefaultName(int oneBasedIndex) => $"C{oneBasedIndex}";
}
