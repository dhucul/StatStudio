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

        string model = $"S = {(double.IsNaN(r.S) ? "*" : Fmt.N(r.S, 4))}   R-sq = {r.RSquared * 100:0.00}%";
        string note = r.DfError <= 0
            ? "\n\n[saturated design: no pure-error df — judge significance from the Pareto/normal plot of effects]"
            : "";
        return $"Factorial Analysis: {r.Response} versus {string.Join(", ", r.Factors)}\n\n" +
               "Estimated Effects and Coefficients (coded units)\n" + t + "\n\n" + model + note;
    }

    public static string Fractional(FractionalDesign d)
    {
        int pp = d.Factors - (int)Math.Round(Math.Log2(d.Runs));
        return $"Created 2^({d.Factors}-{pp}) fractional factorial: {d.Runs} runs, Resolution {Roman(d.Resolution)}.\n" +
               $"Generators: {string.Join(", ", d.Generators)}\n" +
               $"Defining relation: {d.DefiningRelation}\n" +
               $"Factors {string.Join(", ", d.FactorNames)} written to the worksheet (coded ±1).";
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

        var sv = new TextTable("Source", $"StudyVar ({g.StudyVarMultiplier:0.#}×SD)", "%Study Var").LeftAlign(0);
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
