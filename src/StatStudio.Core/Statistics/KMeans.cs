namespace StatStudio.Core.Statistics;

public sealed record KMeansResult(
    int K, IReadOnlyList<string> Variables, int[] Assignments, double[][] Centroids,
    int[] Sizes, double[] WithinSS, double TotalWithinSS, int Iterations, bool Converged = true);

public static class KMeans
{
    /// <summary>k-means clustering (Lloyd's algorithm, k-means++ seeding) on n×p data.</summary>
    public static KMeansResult Cluster(double[][] data, int k, IReadOnlyList<string> names,
        int maxIter = 100, int seed = 12345, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(names);
        int n = data.Length, p = names.Count;
        if (k < 2 || k > n) throw new ArgumentException("k must be between 2 and the number of observations.");
        if (p < 1) throw new ArgumentException("Select at least one clustering variable.", nameof(names));
        if (maxIter < 1) throw new ArgumentOutOfRangeException(nameof(maxIter));
        if (data.Any(row => row is null || row.Length != p))
            throw new ArgumentException("Every k-means row must contain one value per variable.", nameof(data));
        if (data.SelectMany(row => row).Any(v => !double.IsFinite(v)))
            throw new ArgumentException("K-means values must be finite.", nameof(data));

        var rnd = new Random(seed);
        var centroids = SeedPlusPlus(data, k, p, rnd, cancellationToken);
        var assign = new int[n];
        int iter = 0;
        bool converged = false;

        for (; iter < maxIter; iter++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool changed = false;
            for (int i = 0; i < n; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int best = 0; double bestD = double.MaxValue;
                for (int c = 0; c < k; c++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    double d = Dist2(data[i], centroids[c]);
                    if (d < bestD) { bestD = d; best = c; }
                }
                if (assign[i] != best) { assign[i] = best; changed = true; }
            }

            var sum = new double[k][];
            var cnt = new int[k];
            for (int c = 0; c < k; c++) sum[c] = new double[p];
            for (int i = 0; i < n; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                cnt[assign[i]]++;
                for (int j = 0; j < p; j++) sum[assign[i]][j] += data[i][j];
            }
            for (int c = 0; c < k; c++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (cnt[c] != 0) continue;
                // Some cluster always holds >= 2 points here (n >= k and this one is empty),
                // so MaxBy is safe — and O(n) rather than the O(n log n) full sort it replaces.
                int farthest = Enumerable.Range(0, n)
                    .Where(i => cnt[assign[i]] > 1)
                    .MaxBy(i => Dist2(data[i], centroids[assign[i]]));
                int previous = assign[farthest];
                assign[farthest] = c;
                cnt[previous]--;
                cnt[c] = 1;
                for (int j = 0; j < p; j++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    sum[previous][j] -= data[farthest][j];
                    sum[c][j] = data[farthest][j];
                }
                changed = true;
            }
            for (int c = 0; c < k; c++)
                for (int j = 0; j < p; j++) centroids[c][j] = sum[c][j] / cnt[c];

            if (!changed) { converged = true; iter++; break; }
        }

        var within = new double[k];
        var sizes = new int[k];
        for (int i = 0; i < n; i++) { within[assign[i]] += Dist2(data[i], centroids[assign[i]]); sizes[assign[i]]++; }
        return new KMeansResult(k, names, assign, centroids, sizes, within, within.Sum(), iter, converged);
    }

    private static double[][] SeedPlusPlus(double[][] data, int k, int p, Random rnd, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int n = data.Length;
        var centroids = new double[k][];
        centroids[0] = (double[])data[rnd.Next(n)].Clone();
        var d2 = new double[n];
        for (int c = 1; c < k; c++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double total = 0;
            for (int i = 0; i < n; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double best = double.MaxValue;
                for (int j = 0; j < c; j++) best = Math.Min(best, Dist2(data[i], centroids[j]));
                d2[i] = best; total += best;
            }
            if (!(total > 0) || !double.IsFinite(total))
                throw new ArgumentException("The data contain fewer than k distinct observations.", nameof(data));
            double target = rnd.NextDouble() * total, acc = 0;
            int chosen = n - 1;
            for (int i = 0; i < n; i++) { acc += d2[i]; if (acc >= target) { chosen = i; break; } }
            centroids[c] = (double[])data[chosen].Clone();
        }
        return centroids;
    }

    private static double Dist2(double[] a, double[] b)
    {
        double s = 0;
        for (int j = 0; j < a.Length; j++) { double d = a[j] - b[j]; s += d * d; }
        return s;
    }
}
