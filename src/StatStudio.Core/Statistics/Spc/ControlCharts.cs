namespace StatStudio.Core.Statistics.Spc;

/// <summary>
/// A computed control chart: plotted points with a center line and (possibly
/// per-point) control limits, plus out-of-control signals.
/// </summary>
public sealed record SpcChart(
    string Title, string YLabel, double Center,
    double[] Values, double[] Ucl, double[] Lcl, bool[] OutOfControl, string[] Signals);

public static class ControlCharts
{
    // ---- variables charts --------------------------------------------------

    public static (SpcChart Mean, SpcChart Range) XbarR(IReadOnlyList<double[]> subgroups)
    {
        int n = RequireEqualSize(subgroups);
        var means = subgroups.Select(g => g.Average()).ToArray();
        var ranges = subgroups.Select(g => g.Max() - g.Min()).ToArray();
        double xbb = means.Average();
        double rbar = ranges.Average();

        double a2 = SpcConstants.A2(n);
        var xbar = Build("Xbar Chart", "Sample Mean", xbb, means,
            xbb + a2 * rbar, xbb - a2 * rbar);
        var r = Build("R Chart", "Sample Range", rbar, ranges,
            SpcConstants.D4(n) * rbar, SpcConstants.D3(n) * rbar);
        return (xbar, r);
    }

    public static (SpcChart Mean, SpcChart StDev) XbarS(IReadOnlyList<double[]> subgroups)
    {
        int n = RequireEqualSize(subgroups);
        var means = subgroups.Select(g => g.Average()).ToArray();
        var sds = subgroups.Select(StdDev).ToArray();
        double xbb = means.Average();
        double sbar = sds.Average();

        double a3 = SpcConstants.A3(n);
        var xbar = Build("Xbar Chart", "Sample Mean", xbb, means,
            xbb + a3 * sbar, xbb - a3 * sbar);
        var s = Build("S Chart", "Sample StDev", sbar, sds,
            SpcConstants.B4(n) * sbar, SpcConstants.B3(n) * sbar);
        return (xbar, s);
    }

    public static (SpcChart Individuals, SpcChart MovingRange) IMR(double[] values)
    {
        int m = values.Length;
        var mr = new double[m - 1];
        for (int i = 1; i < m; i++) mr[i - 1] = Math.Abs(values[i] - values[i - 1]);
        double mean = values.Average();
        double mrBar = mr.Average();
        double d2 = SpcConstants.D2(2);          // moving range of length 2
        double sigma = mrBar / d2;

        var ind = Build("I Chart", "Individual Value", mean, values,
            mean + 3 * sigma, mean - 3 * sigma);
        var mrChart = Build("MR Chart", "Moving Range", mrBar, mr,
            SpcConstants.D4(2) * mrBar, SpcConstants.D3(2) * mrBar);
        return (ind, mrChart);
    }

    // ---- attributes charts -------------------------------------------------

    public static SpcChart PChart(int[] defectives, int[] sizes)
    {
        int k = defectives.Length;
        double totalD = defectives.Sum();
        double totalN = sizes.Sum();
        double pbar = totalD / totalN;

        var p = new double[k];
        var ucl = new double[k];
        var lcl = new double[k];
        for (int i = 0; i < k; i++)
        {
            p[i] = (double)defectives[i] / sizes[i];
            double sigma = Math.Sqrt(pbar * (1 - pbar) / sizes[i]);
            ucl[i] = pbar + 3 * sigma;
            lcl[i] = Math.Max(0, pbar - 3 * sigma);
        }
        return Build("P Chart", "Proportion", pbar, p, ucl, lcl);
    }

    public static SpcChart NPChart(int[] defectives, int n)
    {
        int k = defectives.Length;
        var counts = defectives.Select(d => (double)d).ToArray();
        double npbar = counts.Average();
        double pbar = npbar / n;
        double sigma = Math.Sqrt(npbar * (1 - pbar));
        return Build("NP Chart", "Count", npbar, counts,
            npbar + 3 * sigma, Math.Max(0, npbar - 3 * sigma));
    }

    public static SpcChart CChart(int[] counts)
    {
        var c = counts.Select(v => (double)v).ToArray();
        double cbar = c.Average();
        double sigma = Math.Sqrt(cbar);
        return Build("C Chart", "Count", cbar, c,
            cbar + 3 * sigma, Math.Max(0, cbar - 3 * sigma));
    }

    public static SpcChart UChart(int[] counts, int[] sizes)
    {
        int k = counts.Length;
        double ubar = (double)counts.Sum() / sizes.Sum();
        var u = new double[k];
        var ucl = new double[k];
        var lcl = new double[k];
        for (int i = 0; i < k; i++)
        {
            u[i] = counts[i] / (double)sizes[i];
            double sigma = Math.Sqrt(ubar / sizes[i]);
            ucl[i] = ubar + 3 * sigma;
            lcl[i] = Math.Max(0, ubar - 3 * sigma);
        }
        return Build("U Chart", "Count per Unit", ubar, u, ucl, lcl);
    }

    // ---- builders & rules --------------------------------------------------

    private static SpcChart Build(string title, string yLabel, double center,
        double[] values, double ucl, double lcl)
    {
        int k = values.Length;
        var u = Enumerable.Repeat(ucl, k).ToArray();
        var l = Enumerable.Repeat(lcl, k).ToArray();
        return Build(title, yLabel, center, values, u, l);
    }

    private static SpcChart Build(string title, string yLabel, double center,
        double[] values, double[] ucl, double[] lcl)
    {
        int k = values.Length;
        var ooc = new bool[k];
        var signals = new string[k];
        for (int i = 0; i < k; i++) signals[i] = "";

        // Nelson Test 1: a point beyond the 3-sigma control limits.
        for (int i = 0; i < k; i++)
        {
            if (values[i] > ucl[i] || values[i] < lcl[i]) { ooc[i] = true; signals[i] = AddSignal(signals[i], "1"); }
        }

        // Nelson Test 2: nine consecutive points on the same side of the center line.
        int run = 0; bool above = false;
        for (int i = 0; i < k; i++)
        {
            bool a = values[i] > center;
            if (i == 0 || a != above) { run = 1; above = a; }
            else run++;
            if (run >= 9)
                for (int j = i - 8; j <= i; j++) { ooc[j] = true; signals[j] = AddSignal(signals[j], "2"); }
        }

        return new SpcChart(title, yLabel, center, values, ucl, lcl, ooc, signals);
    }

    private static string AddSignal(string existing, string code) =>
        string.IsNullOrEmpty(existing) ? code : existing + "," + code;

    private static int RequireEqualSize(IReadOnlyList<double[]> subgroups)
    {
        if (subgroups.Count < 2) throw new ArgumentException("Need at least 2 subgroups.");
        int n = subgroups[0].Length;
        if (n < 2) throw new ArgumentException("Subgroups must have size >= 2.");
        if (subgroups.Any(g => g.Length != n))
            throw new ArgumentException("All subgroups must have the same size for this chart.");
        if (!SpcConstants.Supports(n))
            throw new ArgumentException($"Subgroup size {n} is unsupported (2..10).");
        return n;
    }

    private static double StdDev(double[] x)
    {
        int n = x.Length;
        if (n < 2) return 0;
        double m = x.Average(), ss = 0;
        foreach (var v in x) ss += (v - m) * (v - m);
        return Math.Sqrt(ss / (n - 1));
    }
}
