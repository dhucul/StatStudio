namespace StatStudio.Core.Statistics;

public sealed record FactorAnalysisResult(
    IReadOnlyList<string> Variables, int NumFactors, bool Rotated,
    double[] Eigenvalues, double[] VarianceExplained, double[] Proportion,
    double[,] Loadings, double[] Communalities, double[] Uniqueness);

/// <summary>Factor analysis by principal-components extraction, with optional varimax rotation.</summary>
public static class FactorAnalysis
{
    public static FactorAnalysisResult Extract(double[][] data, IReadOnlyList<string> names,
        int numFactors, bool rotate = false)
    {
        int p = names.Count;
        if (numFactors < 1 || numFactors > p)
            throw new ArgumentException("Number of factors must be between 1 and the number of variables.");

        var pca = Pca.Compute(data, names, correlation: true);

        var loadings = new double[p, numFactors];
        for (int j = 0; j < numFactors; j++)
        {
            double sqrtEig = Math.Sqrt(Math.Max(0, pca.Eigenvalues[j]));
            for (int i = 0; i < p; i++) loadings[i, j] = pca.Loadings[i, j] * sqrtEig;
        }

        if (rotate && numFactors >= 2) Varimax(loadings);

        var comm = new double[p];
        var uniq = new double[p];
        for (int i = 0; i < p; i++)
        {
            double s = 0;
            for (int j = 0; j < numFactors; j++) s += loadings[i, j] * loadings[i, j];
            comm[i] = s;
            uniq[i] = 1 - s;
        }

        var varExpl = new double[numFactors];
        for (int j = 0; j < numFactors; j++)
        {
            double s = 0;
            for (int i = 0; i < p; i++) s += loadings[i, j] * loadings[i, j];
            varExpl[j] = s;
        }
        var prop = varExpl.Select(v => v / p).ToArray();

        return new FactorAnalysisResult(names, numFactors, rotate && numFactors >= 2,
            pca.Eigenvalues, varExpl, prop, loadings, comm, uniq);
    }

    /// <summary>Kaiser varimax rotation (in place) with communality normalization.</summary>
    private static void Varimax(double[,] L, int maxIter = 100, double eps = 1e-9)
    {
        int p = L.GetLength(0), m = L.GetLength(1);
        var h = new double[p];
        for (int i = 0; i < p; i++)
        {
            double s = 0;
            for (int j = 0; j < m; j++) s += L[i, j] * L[i, j];
            h[i] = Math.Sqrt(s);
            if (h[i] > 0) for (int j = 0; j < m; j++) L[i, j] /= h[i];
        }

        double prev = 0;
        for (int iter = 0; iter < maxIter; iter++)
        {
            for (int a = 0; a < m; a++)
                for (int b = a + 1; b < m; b++)
                {
                    double A = 0, B = 0, C = 0, D = 0;
                    for (int i = 0; i < p; i++)
                    {
                        double x = L[i, a], y = L[i, b];
                        double u = x * x - y * y, v = 2 * x * y;
                        A += u; B += v; C += u * u - v * v; D += 2 * u * v;
                    }
                    double num = D - 2 * A * B / p;
                    double den = C - (A * A - B * B) / p;
                    double angle = 0.25 * Math.Atan2(num, den);
                    if (Math.Abs(angle) < eps) continue;
                    double cos = Math.Cos(angle), sin = Math.Sin(angle);
                    for (int i = 0; i < p; i++)
                    {
                        double x = L[i, a], y = L[i, b];
                        L[i, a] = cos * x + sin * y;
                        L[i, b] = -sin * x + cos * y;
                    }
                }

            double crit = 0;
            for (int j = 0; j < m; j++)
            {
                double s2 = 0, s4 = 0;
                for (int i = 0; i < p; i++) { double l2 = L[i, j] * L[i, j]; s2 += l2; s4 += l2 * l2; }
                crit += p * s4 - s2 * s2;
            }
            if (Math.Abs(crit - prev) < eps) break;
            prev = crit;
        }

        for (int i = 0; i < p; i++)
            if (h[i] > 0) for (int j = 0; j < m; j++) L[i, j] *= h[i];
    }
}
