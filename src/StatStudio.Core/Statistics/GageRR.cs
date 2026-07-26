namespace StatStudio.Core.Statistics;

public sealed record VarianceComponent(string Source, double Variance, double PctContribution, double StudyVar, double PctStudyVar);

public sealed record GageRRResult(
    int Parts, int Operators, int Replicates, bool InteractionInModel,
    double InteractionP, double StudyVarMultiplier, int DistinctCategories,
    IReadOnlyList<VarianceComponent> Components);

/// <summary>Crossed Gage R&amp;R (measurement systems analysis) by the ANOVA method.</summary>
public static class GageRR
{
    public static GageRRResult Analyze(double[] measurement, string[] partLabels, string[] operatorLabels,
        double studyVarMultiplier = 6.0, double alphaToRemoveInteraction = 0.05)
    {
        StatGuard.Finite(measurement, nameof(measurement));
        if (!double.IsFinite(studyVarMultiplier) || studyVarMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(studyVarMultiplier), "The study variation multiplier must be positive and finite.");
        StatGuard.Probability(alphaToRemoveInteraction, nameof(alphaToRemoveInteraction));

        int p = partLabels.Distinct().Count();
        int o = operatorLabels.Distinct().Count();
        int n = measurement.Length;
        int r = (p * o) > 0 ? n / (p * o) : 0;
        if (r < 2) throw new ArgumentException("Gage R&R needs at least 2 replicate measurements per part-operator cell.");

        var tw = AnovaExtensions.TwoWay(measurement, partLabels, operatorLabels, "Part", "Operator");

        bool useInteraction = tw.PAB <= alphaToRemoveInteraction;
        double msErr = useInteraction ? tw.MsError : (tw.SsAB + tw.SsError) / (tw.DfAB + tw.DfError);
        double opBaseline = useInteraction ? tw.MsAB : msErr;
        double partBaseline = useInteraction ? tw.MsAB : msErr;

        double repeat = msErr;
        double interactionVar = useInteraction ? Math.Max(0, (tw.MsAB - tw.MsError) / r) : 0;
        double operatorVar = Math.Max(0, (tw.MsB - opBaseline) / (p * r));
        double partVar = Math.Max(0, (tw.MsA - partBaseline) / (o * r));
        double reproducibility = operatorVar + interactionVar;
        double gageRR = repeat + reproducibility;
        double total = gageRR + partVar;

        VarianceComponent Comp(string name, double v) =>
            new(name, v, total > 0 ? 100 * v / total : double.NaN,
                studyVarMultiplier * Math.Sqrt(v),
                total > 0 ? 100 * Math.Sqrt(v / total) : double.NaN);

        var comps = new List<VarianceComponent>
        {
            Comp("Total Gage R&R", gageRR),
            Comp("  Repeatability", repeat),
            Comp("  Reproducibility", reproducibility),
        };
        if (useInteraction)
        {
            comps.Add(Comp("    Operator", operatorVar));
            comps.Add(Comp("    Operator*Part", interactionVar));
        }
        else
        {
            comps.Add(Comp("    Operator", operatorVar));
        }
        comps.Add(Comp("Part-to-Part", partVar));
        comps.Add(Comp("Total Variation", total));

        int ndc = gageRR > 0 ? Math.Max(1, (int)(1.41421356 * Math.Sqrt(partVar / gageRR))) : 0;
        return new GageRRResult(p, o, r, useInteraction, tw.PAB, studyVarMultiplier, ndc, comps);
    }
}
