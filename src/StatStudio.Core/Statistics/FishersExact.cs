using MathNet.Numerics;

namespace StatStudio.Core.Statistics;

public sealed record FisherResult(
    int A, int B, int C, int D, double PTwoSided, double PLess, double PGreater, double OddsRatio);

public static class FishersExact
{
    /// <summary>Fisher's exact test for a 2×2 table [[a,b],[c,d]] (hypergeometric).</summary>
    public static FisherResult Test(int a, int b, int c, int d)
    {
        if (a < 0 || b < 0 || c < 0 || d < 0)
            throw new ArgumentOutOfRangeException(nameof(a), "Cell counts must be nonnegative.");

        long total = (long)a + b + c + d;
        if (total > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(a), "The total count must not exceed 2,147,483,647.");
        int r1 = a + b, r2 = c + d, c1 = a + c, n = (int)total;
        if (n == 0)
            throw new ArgumentException("The contingency table must contain at least one observation.");
        int lo = Math.Max(0, c1 - r2), hi = Math.Min(r1, c1);
        if ((long)hi - lo > 1_000_000)
            throw new ArgumentException("This table requires more than 1,000,000 exact probabilities. Use a chi-square test.");

        double pObs = HyperProb(a, r1, r2, c1, n);
        double pTwo = 0, pLess = 0, pGreater = 0;
        for (long value = lo; value <= hi; value++)
        {
            int x = (int)value;
            double px = HyperProb(x, r1, r2, c1, n);
            if (px <= pObs * (1 + 1e-7)) pTwo += px;
            if (x <= a) pLess += px;
            if (x >= a) pGreater += px;
        }
        double or = (b == 0 || c == 0) ? double.PositiveInfinity : (double)a * d / ((double)b * c);
        return new FisherResult(a, b, c, d, Math.Min(1, pTwo), Math.Min(1, pLess), Math.Min(1, pGreater), or);
    }

    // P(X = x) for the hypergeometric with the table's margins fixed.
    private static double HyperProb(int x, int r1, int r2, int c1, int n)
    {
        double ln = LnChoose(r1, x) + LnChoose(r2, c1 - x) - LnChoose(n, c1);
        return Math.Exp(ln);
    }

    private static double LnChoose(int n, int k)
    {
        if (k < 0 || k > n) return double.NegativeInfinity;
        return SpecialFunctions.FactorialLn(n) - SpecialFunctions.FactorialLn(k) - SpecialFunctions.FactorialLn(n - k);
    }
}
