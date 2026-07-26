using System.Globalization;

namespace StatStudio.Core.Data;

/// <summary>
/// Evaluates a calculator expression over a worksheet, producing one value per row.
/// Supports column references (Cn or column names, names with spaces in single quotes),
/// + - * / ^, parentheses, unary minus, per-row functions, and column aggregates.
/// </summary>
public static class Calculator
{
    public static double[] Evaluate(string expression, Worksheet ws)
    {
        var parser = new Parser(expression, ws);
        var node = parser.Parse();
        int n = ws.RowCount;
        var result = new double[n];
        for (int r = 0; r < n; r++) result[r] = node(r);
        return result;
    }

    private static readonly Dictionary<string, Func<double, double>> Unary = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sqrt"] = Math.Sqrt, ["abs"] = Math.Abs, ["exp"] = Math.Exp,
        ["log"] = Math.Log, ["ln"] = Math.Log, ["loge"] = Math.Log,
        ["log10"] = Math.Log10, ["logten"] = Math.Log10,
        ["sin"] = Math.Sin, ["cos"] = Math.Cos, ["tan"] = Math.Tan,
        ["asin"] = Math.Asin, ["acos"] = Math.Acos, ["atan"] = Math.Atan,
        ["round"] = x => Math.Round(x), ["floor"] = Math.Floor,
        ["ceil"] = Math.Ceiling, ["ceiling"] = Math.Ceiling, ["int"] = Math.Truncate,
    };

    private static readonly Dictionary<string, Func<IReadOnlyList<double>, double>> Aggregate = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mean"] = v => v.Average(),
        ["sum"] = v => v.Sum(),
        ["min"] = v => v.Min(),
        ["max"] = v => v.Max(),
        ["n"] = v => v.Count,
        ["median"] = v => { var s = v.OrderBy(x => x).ToArray(); int m = s.Length; return m == 0 ? double.NaN : m % 2 == 1 ? s[m / 2] : (s[m / 2 - 1] + s[m / 2]) / 2; },
        ["stdev"] = StdDev, ["std"] = StdDev,
    };

    private static double StdDev(IReadOnlyList<double> v)
    {
        int n = v.Count;
        if (n < 2) return double.NaN;
        double m = v.Average(), ss = 0;
        foreach (var x in v) ss += (x - m) * (x - m);
        return Math.Sqrt(ss / (n - 1));
    }

    private sealed class Parser
    {
        private readonly string _s;
        private readonly Worksheet _ws;
        private readonly int _n;
        private readonly Dictionary<int, double[]> _colCache = new();
        private int _pos;

        public Parser(string s, Worksheet ws) { _s = s; _ws = ws; _n = ws.RowCount; }

        public Func<int, double> Parse()
        {
            var node = ParseExpr();
            SkipWs();
            if (_pos < _s.Length) throw new FormatException($"Unexpected '{_s[_pos]}' at position {_pos}.");
            return node;
        }

        private Func<int, double> ParseExpr()
        {
            var left = ParseTerm();
            while (true)
            {
                SkipWs();
                char c = Peek();
                if (c == '+' || c == '-') { _pos++; var right = ParseTerm(); var l = left; var op = c; left = r => op == '+' ? l(r) + right(r) : l(r) - right(r); }
                else return left;
            }
        }

        private Func<int, double> ParseTerm()
        {
            var left = ParseUnary();
            while (true)
            {
                char c = Peek();
                if (c == '*' || c == '/') { _pos++; var right = ParseUnary(); var l = left; var op = c; left = r => op == '*' ? l(r) * right(r) : l(r) / right(r); }
                else return left;
            }
        }

        // Unary minus binds looser than ^, so -x^2 = -(x^2) (mathematical convention).
        private Func<int, double> ParseUnary()
        {
            char c = Peek();
            if (c == '-') { _pos++; var o = ParseUnary(); return r => -o(r); }
            if (c == '+') { _pos++; return ParseUnary(); }
            return ParsePower();
        }

        private Func<int, double> ParsePower()
        {
            var b = ParseAtom();
            if (Peek() == '^') { _pos++; var e = ParseUnary(); return r => Math.Pow(b(r), e(r)); }  // right-assoc; exponent may be unary
            return b;
        }

        private Func<int, double> ParseAtom()
        {
            SkipWs();
            char c = Peek();
            if (c == '(') { _pos++; var e = ParseExpr(); SkipWs(); Expect(')'); return e; }
            if (c == '\'') return Column(ReadQuoted());
            if (char.IsDigit(c) || c == '.') return Number();
            if (char.IsLetter(c)) return Identifier();
            throw new FormatException($"Unexpected character '{c}' at position {_pos}.");
        }

        private Func<int, double> Number()
        {
            int start = _pos;
            while (_pos < _s.Length && (char.IsDigit(_s[_pos]) || _s[_pos] == '.' || _s[_pos] == 'e' || _s[_pos] == 'E' ||
                ((_s[_pos] == '+' || _s[_pos] == '-') && _pos > start && (_s[_pos - 1] == 'e' || _s[_pos - 1] == 'E')))) _pos++;
            double v = double.Parse(_s[start.._pos], CultureInfo.InvariantCulture);
            return _ => v;
        }

        private Func<int, double> Identifier()
        {
            int start = _pos;
            while (_pos < _s.Length && (char.IsLetterOrDigit(_s[_pos]) || _s[_pos] == '_')) _pos++;
            string name = _s[start.._pos];
            SkipWs();
            if (Peek() == '(') return FunctionCall(name);
            return Column(name);
        }

        private Func<int, double> FunctionCall(string name)
        {
            Expect('(');
            var arg = ParseExpr();
            SkipWs();
            Expect(')');
            if (Unary.TryGetValue(name, out var f)) return r => f(arg(r));
            if (Aggregate.TryGetValue(name, out var agg))
            {
                var vals = new List<double>(_n);
                for (int r = 0; r < _n; r++) { double v = arg(r); if (!double.IsNaN(v)) vals.Add(v); }
                double result = vals.Count > 0 ? agg(vals) : double.NaN;
                return _ => result;
            }
            throw new FormatException($"Unknown function '{name}'.");
        }

        private Func<int, double> Column(string name)
        {
            int idx = ResolveColumn(name);
            var vals = ColumnValues(idx);
            return r => r < vals.Length ? vals[r] : double.NaN;
        }

        private int ResolveColumn(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new FormatException("Column name cannot be empty.");
            if ((name[0] == 'C' || name[0] == 'c') && int.TryParse(name[1..], out int cn) && cn >= 1 && cn <= _ws.ColumnCount)
                return cn - 1;
            int byName = _ws.IndexOf(name);
            if (byName >= 0) return byName;
            throw new FormatException($"Unknown column '{name}'.");
        }

        private double[] ColumnValues(int idx)
        {
            if (_colCache.TryGetValue(idx, out var cached)) return cached;
            var col = _ws[idx];
            var vals = new double[_n];
            for (int r = 0; r < _n; r++) vals[r] = !col.IsMissing(r) && DataColumn.TryParse(col[r], out var v) ? v : double.NaN;
            _colCache[idx] = vals;
            return vals;
        }

        private string ReadQuoted()
        {
            _pos++; // opening quote
            int start = _pos;
            while (_pos < _s.Length && _s[_pos] != '\'') _pos++;
            string name = _s[start.._pos];
            Expect('\'');
            return name;
        }

        private char Peek() { SkipWs(); return _pos < _s.Length ? _s[_pos] : '\0'; }
        private void SkipWs() { while (_pos < _s.Length && char.IsWhiteSpace(_s[_pos])) _pos++; }
        private void Expect(char c) { SkipWs(); if (_pos >= _s.Length || _s[_pos] != c) throw new FormatException($"Expected '{c}' at position {_pos}."); _pos++; }
    }
}
