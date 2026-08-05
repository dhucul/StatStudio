using StatStudio.Core.Inference;

namespace StatStudio.Core.Statistics;

public static class DoeFormatters
{
    public static string Factorial(FactorialResult r)
    {
        var t = new TextTable("Term", "Effect", "Coef", "SE Coef", "T-Value", "P-Value").LeftAlign(0);
        foreach (var term in r.Terms)
            t.Add(term.Name,
                double.IsNaN(term.Effect) ? "" : Fmt.N(term.Effect, 4),
                Fmt.N(term.Coef, 4),
                double.IsNaN(term.SeCoef) ? "" : Fmt.N(term.SeCoef, 4),
                double.IsNaN(term.T) ? "" : Fmt.N(term.T, 2),
                double.IsNaN(term.P) ? "" : Fmt.P(term.P));

        string model = $"S = {(double.IsNaN(r.S) ? "*" : Fmt.N(r.S, 4))}   R-sq = {Fmt.PctFixed(r.RSquared)}";
        string note = r.DfError <= 0
            ? "\n\n[saturated design: no pure-error df — judge significance from the Pareto/normal plot of effects]"
            : "";
        // In a fractional design the estimates are alias sums, not isolated effects; report which.
        string alias = r.Aliases.Count == 0
            ? ""
            : "\n\nAlias Structure (effects this design cannot separate)\n  " +
              string.Join("\n  ", r.Aliases);
        return $"Factorial Analysis: {r.Response} versus {string.Join(", ", r.Factors)}\n\n" +
               "Estimated Effects and Coefficients (coded units)\n" + t + "\n\n" + model + note + alias;
    }

    public static string Mixture(MixtureDesignResult d) =>
        $"Created {d.Type} mixture design: {d.Components} components, {d.Runs} runs.\n" +
        $"Components {string.Join(", ", d.ComponentNames)} written to the worksheet (each row's proportions sum to 1).";

    public static string MixtureModel(MixtureResult r)
    {
        var t = new TextTable("Term", "Coef", "SE Coef", "T-Value", "P-Value").LeftAlign(0);
        foreach (var term in r.Terms)
            t.Add(term.Name, Fmt.N(term.Coef, 4),
                double.IsNaN(term.SeCoef) ? "" : Fmt.N(term.SeCoef, 4),
                double.IsNaN(term.T) ? "" : Fmt.N(term.T, 2),
                double.IsNaN(term.P) ? "" : Fmt.P(term.P));
        var model = new TextTable("S", "R-sq", "R-sq(adj)");
        model.Add(Fmt.N(r.S, 4), Fmt.PctFixed(r.RSquared), Fmt.PctFixed(r.RSquaredAdj));
        return $"Mixture Regression: {r.Response} ({(r.Quadratic ? "quadratic" : "linear")} Scheffé model, no intercept)\n\n" +
               "Estimated Coefficients (component proportions)\n" + t + "\n\n" + "Model Summary\n" + model;
    }

    public static string Fractional(FractionalDesign d)
    {
        int pp = d.Factors - (int)Math.Round(Math.Log2(d.Runs));
        return $"Created 2^({d.Factors}-{pp}) fractional factorial: {d.Runs} runs, Resolution {Roman(d.Resolution)}.\n" +
               $"Generators: {string.Join(", ", d.Generators)}\n" +
               $"Defining relation: {d.DefiningRelation}\n" +
               $"Factors {string.Join(", ", d.FactorNames)} written to the worksheet (coded ±1).";
    }

    public static string Rsm(RsmDesign d)
    {
        string alpha = d.Type.StartsWith("Central", StringComparison.Ordinal) ? $", axial α = {Fmt.G(d.Alpha)}" : "";
        // ToLowerInvariant: the Turkish locale maps 'I' to a dotless 'ı' in ToLower().
        var pts = d.RunList.GroupBy(r => r.PointType).Select(g => $"{g.Count()} {g.Key.ToLowerInvariant()}");
        return $"Created {d.Type} design: {d.Factors} factors, {d.Runs} runs ({string.Join(", ", pts)}){alpha}.\n" +
               $"Factors {string.Join(", ", d.FactorNames)} and a PtType column written to the worksheet.";
    }

    private static string Roman(int n) => n switch
    {
        2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", _ => n.ToString(),
    };

    public static string Design(FactorialDesign d) =>
        $"Created full factorial design: {d.Factors} factors, {d.Runs} runs " +
        $"({d.Replicates} replicate(s){(d.CenterPoints > 0 ? $", {d.CenterPoints} center point(s)/replicate" : "")}). " +
        $"Factors {string.Join(", ", d.FactorNames)} written to the worksheet in coded units.";

    public static string GageRR(GageRRResult g)
    {
        var vc = new TextTable("Source", "VarComp", "%Contribution").LeftAlign(0);
        foreach (var c in g.Components)
            vc.Add(c.Source, Fmt.N(c.Variance, 5), Fmt.N(c.PctContribution, 2));

        var sv = new TextTable("Source", $"StudyVar ({Fmt.G(g.StudyVarMultiplier)}×SD)", "%Study Var").LeftAlign(0);
        foreach (var c in g.Components)
            sv.Add(c.Source, Fmt.N(c.StudyVar, 4), Fmt.N(c.PctStudyVar, 2));

        string model = g.InteractionInModel
            ? $"Interaction kept in model (p = {Fmt.P(g.InteractionP)})."
            : $"Interaction removed (p = {Fmt.P(g.InteractionP)} > α); pooled into repeatability.";

        return $"Gage R&R Study (ANOVA Method)\n\n" +
               $"{g.Parts} parts × {g.Operators} operators × {g.Replicates} replicates. {model}\n\n" +
               "Variance Components\n" + vc + "\n\n" +
               "Gage Evaluation (Study Variation)\n" + sv + "\n\n" +
               $"Number of Distinct Categories = {g.DistinctCategories}";
    }
}
