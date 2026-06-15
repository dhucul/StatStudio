using StatStudio.Core.Statistics;

namespace StatStudio.Smoke;

internal static class DoeTests
{
    public static void Run()
    {
        Check.Section("DOE — full factorial 2^3");
        var d = DoeDesign.FullFactorial(3, replicates: 1, centerPoints: 0, randomize: false);
        Check.Equal(d.Runs, 8, "run count");
        for (int j = 0; j < 3; j++)
        {
            double sum = d.RunList.Sum(r => r.Factors[j]);
            Check.Close(sum, 0, $"factor {j} balanced (sum 0)");
        }
        double dot01 = d.RunList.Sum(r => r.Factors[0] * r.Factors[1]);
        double dot12 = d.RunList.Sum(r => r.Factors[1] * r.Factors[2]);
        Check.Close(dot01, 0, "factors A,B orthogonal");
        Check.Close(dot12, 0, "factors B,C orthogonal");

        Check.Section("DOE — analyze 2^2 (y = 10 + 3A + 2B + 1AB)");
        var a = new double[] { -1, -1, 1, 1 };
        var b = new double[] { -1, 1, -1, 1 };
        var y = new double[4];
        for (int i = 0; i < 4; i++) y[i] = 10 + 3 * a[i] + 2 * b[i] + 1 * a[i] * b[i];
        var fa = FactorialAnalysis.Analyze(y, new[] { a, b }, new[] { "A", "B" }, "Y");
        FactorialTerm T(string name) => fa.Terms.First(t => t.Name == name);
        Check.Close(T("Constant").Coef, 10, "constant");
        Check.Close(T("A").Effect, 6, "effect A");
        Check.Close(T("B").Effect, 4, "effect B");
        Check.Close(T("AB").Effect, 2, "effect AB");
        Check.Close(T("A").Coef, 3, "coef A");

        Check.Section("Gage R&R (2 parts × 2 operators × 2 reps)");
        var meas = new double[] { 10, 12, 11, 13, 20, 22, 21, 23 };
        var parts = new[] { "P1", "P1", "P1", "P1", "P2", "P2", "P2", "P2" };
        var opers = new[] { "O1", "O1", "O2", "O2", "O1", "O1", "O2", "O2" };
        var g = GageRR.Analyze(meas, parts, opers);
        double Comp(string name) => g.Components.First(c => c.Source.Trim() == name).Variance;
        Check.True(!g.InteractionInModel, "interaction dropped (p>0.05)");
        Check.Close(Comp("Repeatability"), 1.6, "repeatability variance");
        Check.Close(Comp("Reproducibility"), 0.1, "reproducibility variance");
        Check.Close(Comp("Total Gage R&R"), 1.7, "total gage R&R variance");
        Check.Close(Comp("Part-to-Part"), 49.6, "part-to-part variance");
        Check.Close(Comp("Total Variation"), 51.3, "total variation");
        Check.Equal(g.DistinctCategories, 7, "number of distinct categories");
    }
}
