using StatStudio.Core.Statistics.Spc;

namespace StatStudio.Smoke;

internal static class SpcTests
{
    public static void Run()
    {
        var data = new double[] { 2, 4, 4, 4, 5, 5, 7, 9 };

        Check.Section("I-MR chart  {2,4,4,4,5,5,7,9}");
        var (ind, mr) = ControlCharts.IMR(data);
        Check.Close(ind.Center, 5.0, "I center");
        Check.Close(ind.Ucl[0], 7.65957, "I UCL (mean + 3·MRbar/d2)", 1e-3);
        Check.Close(ind.Lcl[0], 2.34043, "I LCL", 1e-3);
        Check.True(ind.OutOfControl[0], "point 1 (=2) out of control");
        Check.True(ind.OutOfControl[7], "point 8 (=9) out of control");
        Check.Close(mr.Center, 1.0, "MR center (MRbar)");
        Check.Close(mr.Ucl[0], 3.267, "MR UCL (D4·MRbar)", 1e-3);

        Check.Section("Xbar-R chart  [[2,4],[4,4],[5,5],[7,9]]");
        var (xbar, r) = ControlCharts.XbarR(new[]
        {
            new double[] { 2, 4 }, new double[] { 4, 4 },
            new double[] { 5, 5 }, new double[] { 7, 9 },
        });
        Check.Close(xbar.Center, 5.0, "Xbar center");
        Check.Close(xbar.Ucl[0], 6.88, "Xbar UCL (+A2·Rbar)", 1e-3);
        Check.Close(xbar.Lcl[0], 3.12, "Xbar LCL", 1e-3);
        Check.Close(r.Center, 1.0, "R center (Rbar)");
        Check.Close(r.Ucl[0], 3.267, "R UCL (D4·Rbar)", 1e-3);

        Check.Section("P chart  def={2,3,5,4,6}, n=50");
        var p = ControlCharts.PChart(new[] { 2, 3, 5, 4, 6 }, new[] { 50, 50, 50, 50, 50 });
        Check.Close(p.Center, 0.08, "p-bar");
        Check.Close(p.Ucl[0], 0.195104, "P UCL", 1e-3);
        Check.Close(p.Lcl[0], 0.0, "P LCL (clamped at 0)");
        var boundedP = ControlCharts.PChart(new[] { 0, 1 }, new[] { 1, 1 });
        Check.Close(boundedP.Ucl[0], 1, "P UCL clamped at 1");
        var boundedNp = ControlCharts.NPChart(new[] { 0, 1 }, 1);
        Check.Close(boundedNp.Ucl[0], 1, "NP UCL clamped at subgroup size");

        Check.Section("C chart  {5,3,4,6,2}");
        var c = ControlCharts.CChart(new[] { 5, 3, 4, 6, 2 });
        Check.Close(c.Center, 4.0, "c-bar");
        Check.Close(c.Ucl[0], 10.0, "C UCL (cbar + 3·sqrt(cbar))");
        Check.Close(c.Lcl[0], 0.0, "C LCL (clamped)");

        var (centerline, _) = ControlCharts.IMR(Enumerable.Repeat(5.0, 10).ToArray());
        Check.True(centerline.OutOfControl.All(flag => !flag), "centerline points do not trigger Nelson rule 2");
        Check.Throws<ArgumentException>(
            () => ControlCharts.PChart(new[] { 1, 2 }, new[] { 1, 1 }), "defectives greater than size rejected");
        Check.Throws<ArgumentException>(
            () => ControlCharts.UChart(new[] { 1, 2 }, new[] { 1 }), "attribute chart length mismatch rejected");

        Check.Section("Capability  LSL=0, USL=10");
        var cap = Capability.FromIndividuals(data, 0, 10);
        Check.Close(cap.Mean, 5.0, "mean");
        Check.Close(cap.SigmaWithin, 0.886525, "within sigma (MRbar/d2)", 1e-3);
        Check.Close(cap.SigmaOverall, 2.138090, "overall sigma (sample SD)", 1e-4);
        Check.Close(cap.Cp, 1.880193, "Cp", 2e-3);
        Check.Close(cap.Cpk, 1.880193, "Cpk", 2e-3);
        Check.Close(cap.Pp, 0.779513, "Pp", 1e-3);
        Check.Close(cap.Ppk, 0.779513, "Ppk", 1e-3);
    }
}
