namespace StatStudio.Core.Statistics.Spc;

/// <summary>Standard Shewhart control-chart constants by subgroup size (n = 2..10).</summary>
public static class SpcConstants
{
    // n -> (A2, A3, d2, D3, D4, B3, B4, c4)
    private static readonly Dictionary<int, double[]> Table = new()
    {
        [2] = new[] { 1.880, 2.659, 1.128, 0.000, 3.267, 0.000, 3.267, 0.7979 },
        [3] = new[] { 1.023, 1.954, 1.693, 0.000, 2.574, 0.000, 2.568, 0.8862 },
        [4] = new[] { 0.729, 1.628, 2.059, 0.000, 2.282, 0.000, 2.266, 0.9213 },
        [5] = new[] { 0.577, 1.427, 2.326, 0.000, 2.114, 0.000, 2.089, 0.9400 },
        [6] = new[] { 0.483, 1.287, 2.534, 0.000, 2.004, 0.030, 1.970, 0.9515 },
        [7] = new[] { 0.419, 1.182, 2.704, 0.076, 1.924, 0.118, 1.882, 0.9594 },
        [8] = new[] { 0.373, 1.099, 2.847, 0.136, 1.864, 0.185, 1.815, 0.9650 },
        [9] = new[] { 0.337, 1.032, 2.970, 0.184, 1.816, 0.239, 1.761, 0.9693 },
        [10] = new[] { 0.308, 0.975, 3.078, 0.223, 1.777, 0.284, 1.716, 0.9727 },
    };

    private static double[] Row(int n)
    {
        if (Table.TryGetValue(n, out var r)) return r;
        throw new ArgumentOutOfRangeException(nameof(n),
            $"Subgroup size {n} is outside the supported range (2..10).");
    }

    public static double A2(int n) => Row(n)[0];
    public static double A3(int n) => Row(n)[1];
    public static double D2(int n) => Row(n)[2];
    public static double D3(int n) => Row(n)[3];
    public static double D4(int n) => Row(n)[4];
    public static double B3(int n) => Row(n)[5];
    public static double B4(int n) => Row(n)[6];
    public static double C4(int n) => Row(n)[7];

    public static bool Supports(int n) => Table.ContainsKey(n);
}
