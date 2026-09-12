using System.Globalization;

namespace StatStudio.Core.Data;

/// <summary>The kind of data a worksheet column holds.</summary>
public enum ColumnType
{
    Numeric,
    Text,
    DateTime,
}

/// <summary>
/// One worksheet column: an ordered list of raw cell strings plus a declared
/// <see cref="ColumnType"/>. Missing values are null / empty / "*" (Minitab uses
/// "*"). Numeric access parses on demand with the invariant culture so results are
/// deterministic regardless of the machine locale.
/// </summary>
public sealed class DataColumn
{
    private readonly List<string?> _cells = new();

    public DataColumn(string name, ColumnType type = ColumnType.Numeric)
    {
        Name = name;
        Type = type;
    }

    public string Name { get; set; }
    public ColumnType Type { get; set; }

    public int Count => _cells.Count;
    public IReadOnlyList<string?> Cells => _cells;

    public string? this[int row]
    {
        get => row >= 0 && row < _cells.Count ? _cells[row] : null;
        set => Set(row, value);
    }

    public void Add(string? raw) => _cells.Add(Clean(raw));

    /// <summary>Store numeric results without applying display rounding.</summary>
    public void AddNumber(double value)
    {
        if (double.IsInfinity(value)) throw new ArithmeticException("Cannot store an infinite numeric result.");
        Add(double.IsNaN(value) ? null : value.ToString("R", CultureInfo.InvariantCulture));
    }

    public void Set(int row, string? raw)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        while (_cells.Count <= row) _cells.Add(null);
        _cells[row] = Clean(raw);
    }

    public void Clear() => _cells.Clear();

    /// <summary>True when a cell holds no value (null, blank, or the Minitab "*" marker).</summary>
    public bool IsMissing(int row)
    {
        var s = this[row];
        return string.IsNullOrWhiteSpace(s) || s == "*";
    }

    /// <summary>Parsed numeric values with missing cells skipped (order preserved).</summary>
    public double[] NumericValues()
    {
        var list = new List<double>(_cells.Count);
        for (int i = 0; i < _cells.Count; i++)
        {
            if (IsMissing(i)) continue;
            if (TryParse(_cells[i], out var v)) list.Add(v);
        }
        return list.ToArray();
    }

    /// <summary>Equally spaced observations; trailing worksheet padding is ignored,
    /// but gaps are rejected so subsequent periods are never silently renumbered.</summary>
    public double[] SeriesValues()
    {
        int last = Count - 1;
        while (last >= 0 && IsMissing(last)) last--;
        var values = new double[last + 1];
        for (int i = 0; i <= last; i++)
            if (IsMissing(i) || !TryParse(this[i], out values[i]))
                throw new ArgumentException($"'{Name}' has a missing or nonnumeric observation in row {i + 1}. Fill the gap before running an ordered analysis.");
        return values;
    }

    /// <summary>Non-missing cell text (used for categorical / grouping columns).</summary>
    public string[] TextValues()
    {
        var list = new List<string>(_cells.Count);
        for (int i = 0; i < _cells.Count; i++)
            if (!IsMissing(i)) list.Add(_cells[i]!);
        return list.ToArray();
    }

    public int MissingCount()
    {
        int m = 0;
        for (int i = 0; i < _cells.Count; i++) if (IsMissing(i)) m++;
        return m;
    }

    /// <summary>True when there is at least one value and every non-missing cell parses as a number.</summary>
    public bool LooksNumeric()
    {
        bool any = false;
        for (int i = 0; i < _cells.Count; i++)
        {
            if (IsMissing(i)) continue;
            any = true;
            if (!TryParse(_cells[i], out _)) return false;
        }
        return any;
    }

    internal static bool TryParse(string? s, out double value)
    {
        if (!double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out value) ||
            !double.IsFinite(value))
        {
            value = default;
            return false;
        }
        return true;
    }

    private static string? Clean(string? raw) => raw?.Trim();
}
