namespace StatStudio.Core.Statistics;

public sealed record RandomEffectGroup(string Name, int N, double Mean, double Blup, double Fitted);

public sealed record OneWayRandomResult(
    double GrandMean, double VarBetween, double VarWithin, double Icc,
    double PctBetween, double PctWithin, double N0, int Groups, int NTotal,
    IReadOnlyList<RandomEffectGroup> GroupEffects);

/// <summary>
/// One-way random-effects (random-intercept) model y_ij = μ + a_i + ε_ij, estimated by the
/// ANOVA method of moments. The canonical two-level hierarchical/multilevel model.
/// </summary>
public static class MixedModel
{
    public static OneWayRandomResult OneWayRandom(IReadOnlyList<(string Name, double[] Values)> groups)
    {
        var anova = Anova.OneWay(groups);
        int k = anova.Groups.Count;
        int nTotal = anova.Groups.Sum(g => g.N);
        double sumNi2 = anova.Groups.Sum(g => (double)g.N * g.N);
        double n0 = (nTotal - sumNi2 / nTotal) / (k - 1);

        double varWithin = anova.MsError;
        double varBetween = Math.Max(0, (anova.MsFactor - anova.MsError) / n0);
        double totalVar = varBetween + varWithin;
        double icc = totalVar > 0 ? varBetween / totalVar : double.NaN;

        double grand = groups.SelectMany(g => g.Values).Average();
        var effects = new List<RandomEffectGroup>();
        foreach (var g in anova.Groups)
        {
            double shrink = (g.N * varBetween) / (g.N * varBetween + varWithin);
            double blup = double.IsNaN(shrink) ? 0 : shrink * (g.Mean - grand);
            effects.Add(new RandomEffectGroup(g.Name, g.N, g.Mean, blup, grand + blup));
        }

        return new OneWayRandomResult(grand, varBetween, varWithin, icc,
            totalVar > 0 ? 100 * varBetween / totalVar : double.NaN,
            totalVar > 0 ? 100 * varWithin / totalVar : double.NaN,
            n0, k, nTotal, effects);
    }
}
