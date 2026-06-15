using StatStudio.Core.Data;

namespace StatStudio.Core.Statistics;

/// <summary>Row-aligned extraction across columns (skips rows missing in any input).</summary>
public static class Columns
{
    /// <summary>Pairwise (x, y) for rows where both columns hold a number.</summary>
    public static (double[] X, double[] Y) Pairwise(DataColumn x, DataColumn y)
    {
        int n = Math.Max(x.Count, y.Count);
        var xs = new List<double>(n);
        var ys = new List<double>(n);
        for (int r = 0; r < n; r++)
        {
            if (x.IsMissing(r) || y.IsMissing(r)) continue;
            if (DataColumn.TryParse(x[r], out var xv) && DataColumn.TryParse(y[r], out var yv))
            {
                xs.Add(xv);
                ys.Add(yv);
            }
        }
        return (xs.ToArray(), ys.ToArray());
    }

    /// <summary>
    /// Each row across the given columns as a subgroup (rows where every column holds
    /// a number). Used by the variables control charts (Xbar-R / Xbar-S).
    /// </summary>
    public static List<double[]> Rows(IReadOnlyList<DataColumn> cols)
    {
        int n = cols.Count == 0 ? 0 : cols.Max(c => c.Count);
        var result = new List<double[]>(n);
        for (int r = 0; r < n; r++)
        {
            var row = new double[cols.Count];
            bool ok = true;
            for (int j = 0; j < cols.Count; j++)
                if (cols[j].IsMissing(r) || !DataColumn.TryParse(cols[j][r], out row[j])) { ok = false; break; }
            if (ok) result.Add(row);
        }
        return result;
    }

    /// <summary>
    /// Row-aligned response/predictor matrix for rows where the response and every
    /// predictor hold a number. Returns y plus one array per predictor column.
    /// </summary>
    public static (double[] Y, double[][] X) Design(DataColumn response, IReadOnlyList<DataColumn> predictors)
    {
        int n = response.Count;
        foreach (var p in predictors) n = Math.Max(n, p.Count);

        var y = new List<double>(n);
        var x = new List<double>[predictors.Count];
        for (int j = 0; j < predictors.Count; j++) x[j] = new List<double>(n);

        for (int r = 0; r < n; r++)
        {
            if (response.IsMissing(r) || !DataColumn.TryParse(response[r], out var yv)) continue;
            var row = new double[predictors.Count];
            bool ok = true;
            for (int j = 0; j < predictors.Count; j++)
            {
                if (predictors[j].IsMissing(r) || !DataColumn.TryParse(predictors[j][r], out row[j])) { ok = false; break; }
            }
            if (!ok) continue;
            y.Add(yv);
            for (int j = 0; j < predictors.Count; j++) x[j].Add(row[j]);
        }

        return (y.ToArray(), x.Select(c => c.ToArray()).ToArray());
    }
}
