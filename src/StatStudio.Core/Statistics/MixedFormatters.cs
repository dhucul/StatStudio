using StatStudio.Core.Inference;

namespace StatStudio.Core.Statistics;

public static class MixedFormatters
{
    public static string OneWayRandom(OneWayRandomResult r, string response, string factor)
    {
        var vc = new TextTable("Source", "Variance", "% of Total").LeftAlign(0);
        vc.Add($"{factor} (between)", Fmt.N(r.VarBetween, 5), Fmt.N(r.PctBetween, 2));
        vc.Add("Error (within)", Fmt.N(r.VarWithin, 5), Fmt.N(r.PctWithin, 2));

        var eff = new TextTable("Group", "N", "Mean", "Random Effect (BLUP)", "Fitted").LeftAlign(0);
        foreach (var g in r.GroupEffects)
            eff.Add(g.Name, g.N.ToString(), Fmt.N(g.Mean), Fmt.N(g.Blup), Fmt.N(g.Fitted));

        return $"Random-Effects Model: {response} versus {factor} (random)\n\n" +
               $"Estimation method: ANOVA (method of moments).  Groups = {r.Groups}, N = {r.NTotal}.\n\n" +
               "Variance Components\n" + vc + "\n" +
               $"  Intraclass correlation (ICC) = {Fmt.N(r.Icc, 4)}\n" +
               $"  Grand mean = {Fmt.N(r.GrandMean)}\n\n" +
               "Estimated Random Effects (shrinkage BLUPs)\n" + eff;
    }
}
