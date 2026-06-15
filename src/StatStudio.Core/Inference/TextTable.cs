using System.Text;

namespace StatStudio.Core.Inference;

/// <summary>
/// Builds a monospaced, column-aligned text table for the Session output. Numeric
/// columns are right-aligned by default; call <see cref="LeftAlign"/> for label columns.
/// Shared by every analysis so Smoke and the WPF UI format identically.
/// </summary>
public sealed class TextTable
{
    private readonly string[] _headers;
    private readonly bool[] _left;
    private readonly List<string[]> _rows = new();
    private readonly string _gap;

    public TextTable(params string[] headers)
    {
        _headers = headers;
        _left = new bool[headers.Length];
        _gap = "  ";
    }

    public TextTable LeftAlign(params int[] cols)
    {
        foreach (var c in cols) if (c >= 0 && c < _left.Length) _left[c] = true;
        return this;
    }

    public void Add(params string[] cells) => _rows.Add(cells);

    public override string ToString()
    {
        int cols = _headers.Length;
        var w = new int[cols];
        for (int j = 0; j < cols; j++) w[j] = _headers[j].Length;
        foreach (var r in _rows)
            for (int j = 0; j < cols && j < r.Length; j++)
                w[j] = Math.Max(w[j], (r[j] ?? string.Empty).Length);

        var sb = new StringBuilder();
        AppendRow(sb, _headers, w);
        foreach (var r in _rows) AppendRow(sb, r, w);
        return sb.ToString().TrimEnd('\n');
    }

    private void AppendRow(StringBuilder sb, string[] cells, int[] w)
    {
        for (int j = 0; j < w.Length; j++)
        {
            string c = j < cells.Length ? (cells[j] ?? string.Empty) : string.Empty;
            sb.Append(_left[j] ? c.PadRight(w[j]) : c.PadLeft(w[j]));
            if (j < w.Length - 1) sb.Append(_gap);
        }
        sb.Append('\n');
    }
}
