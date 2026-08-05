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

        Check.Section("DOE — fractional factorial 2^(4-1), D=ABC");
        var frac = DoeDesign.FractionalFactorial(4, 8, randomize: false);
        Check.Equal(frac.Runs, 8, "8 runs (half fraction)");
        Check.Equal(frac.Resolution, 4, "resolution IV");
        bool dIsAbc = frac.RunList.All(r => Math.Abs(r.Factors[3] - r.Factors[0] * r.Factors[1] * r.Factors[2]) < 1e-9);
        Check.True(dIsAbc, "D = A·B·C for every run");
        Check.True(frac.DefiningRelation.Contains("ABCD"), "defining relation I = ABCD");

        var frac3 = DoeDesign.FractionalFactorial(3, 4, randomize: false);
        Check.Equal(frac3.Resolution, 3, "2^(3-1) is resolution III");
        Check.True(frac3.RunList.All(r => Math.Abs(r.Factors[2] - r.Factors[0] * r.Factors[1]) < 1e-9), "C = A·B");

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
        Check.Close(T("A*B").Effect, 2, "effect A*B");
        Check.Close(T("A").Coef, 3, "coef A");
        // Interaction terms are separated by '*' (as in RSM/mixture models) so multi-character
        // factor names stay unambiguous, and terms are ordered by interaction order, not name length.
        Check.Equal(string.Join(", ", fa.Terms.Select(t => t.Name)), "Constant, A, B, A*B", "term order");

        Check.Section("RSM — central composite design (k=2)");
        var ccd = ResponseSurface.CentralComposite(2, centerPoints: 3, faceCentered: false, randomize: false);
        Check.Equal(ccd.Runs, 11, "runs = 4 cube + 4 axial + 3 center");
        Check.Close(ccd.Alpha, Math.Sqrt(2), "alpha = 2^(1/2) (rotatable)", 1e-6);
        Check.True(ccd.RunList.Any(r => Math.Abs(r.Factors[0] - ccd.Alpha) < 1e-9 && Math.Abs(r.Factors[1]) < 1e-9),
            "an axial point at (+alpha, 0) exists");

        Check.Section("RSM — Box-Behnken (k=3)");
        var bbd = ResponseSurface.BoxBehnken(3, centerPoints: 3, randomize: false);
        Check.Equal(bbd.Runs, 15, "runs = 3 pairs × 4 + 3 center");
        Check.True(bbd.RunList.Where(r => r.PointType == "Edge").All(r => r.Factors.Count(v => v == 0) == 1),
            "each edge point has exactly one factor at 0");

        Check.Section("RSM — analyze quadratic (exact recovery)");
        var cube = ResponseSurface.CentralComposite(2, centerPoints: 3, faceCentered: false, randomize: false);
        var ca = cube.RunList.Select(r => r.Factors[0]).ToArray();
        var cb = cube.RunList.Select(r => r.Factors[1]).ToArray();
        var cy = new double[ca.Length];
        for (int i = 0; i < cy.Length; i++)
            cy[i] = 5 + 2 * ca[i] + 3 * cb[i] + 1 * ca[i] * ca[i] + 0.5 * cb[i] * cb[i] + 1.5 * ca[i] * cb[i];
        var rsm = ResponseSurface.Analyze(cy, new[] { ca, cb }, new[] { "A", "B" });
        RegressionTerm RT(string n) => rsm.Terms.First(t => t.Name == n);
        Check.Close(RT("Constant").Coef, 5, "b0");
        Check.Close(RT("A").Coef, 2, "linear A");
        Check.Close(RT("B").Coef, 3, "linear B");
        Check.Close(RT("A*A").Coef, 1, "square A");
        Check.Close(RT("B*B").Coef, 0.5, "square B");
        Check.Close(RT("A*B").Coef, 1.5, "interaction A*B");

        Check.Section("Mixture — simplex designs");
        var cen = MixtureDesign.SimplexCentroid(3, randomize: false);
        Check.Equal(cen.Runs, 7, "simplex-centroid(3) = 2^3 - 1 runs");
        Check.True(cen.RunList.All(r => Math.Abs(r.Components.Sum() - 1.0) < 1e-9), "components sum to 1");
        var lat = MixtureDesign.SimplexLattice(3, 2, randomize: false);
        Check.Equal(lat.Runs, 6, "simplex-lattice {3,2} = 6 runs");
        Check.True(lat.RunList.All(r => Math.Abs(r.Components.Sum() - 1.0) < 1e-9), "lattice sums to 1");

        Check.Section("Mixture — Scheffé model recovery");
        var ma = cen.RunList.Select(r => r.Components[0]).ToArray();
        var mb = cen.RunList.Select(r => r.Components[1]).ToArray();
        var mc = cen.RunList.Select(r => r.Components[2]).ToArray();
        var my = new double[ma.Length];
        for (int i = 0; i < my.Length; i++) my[i] = 2 * ma[i] + 3 * mb[i] + 5 * mc[i] + 4 * ma[i] * mb[i];
        var mfit = MixtureAnalysis.Fit(my, new[] { ma, mb, mc }, new[] { "A", "B", "C" }, quadratic: true);
        RegressionTerm MT(string n) => mfit.Terms.First(t => t.Name == n);
        Check.Close(MT("A").Coef, 2, "beta A");
        Check.Close(MT("B").Coef, 3, "beta B");
        Check.Close(MT("C").Coef, 5, "beta C");
        Check.Close(MT("A*B").Coef, 4, "beta A*B");
        Check.Close(MT("A*C").Coef, 0, "beta A*C ~ 0", 1e-6);

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
