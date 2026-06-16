using System.Globalization;

namespace StatStudio.Core.Data;

public sealed record SampleDataset(string Name, string Description, Func<Worksheet> Build);

/// <summary>Built-in example datasets, each tuned to demonstrate a set of analyses.</summary>
public static class SampleData
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static IReadOnlyList<SampleDataset> All { get; } = new List<SampleDataset>
    {
        new("Body Measurements", "Height/Weight/Age/Gender/Pulse — descriptives, t-tests, regression, correlation", BodyMeasurements),
        new("Crop Yield (3 fertilizers)", "Unstacked groups A/B/C — one-way ANOVA, Tukey, Kruskal-Wallis, equal variances", CropYield),
        new("Coating Experiment (2x2)", "Response by Temp × Pressure — two-way ANOVA, factorial analysis", Coating),
        new("Monthly Sales", "48 months with trend + seasonality — trend, decomposition, ARIMA/SARIMA, ACF", MonthlySales),
        new("Piston Diameters", "50 individual measurements — I-MR chart, capability analysis", Pistons),
        new("Gage Study", "10 parts × 3 operators × 2 reps (stacked) — Gage R&R", GageStudy),
        new("Bulb Lifetimes", "Failure hours + censor flag — distribution analysis, Kaplan-Meier", Bulbs),
        new("Flower Measurements", "4 measurements × 3 species — PCA, factor analysis, k-means clustering", Flowers),
        new("Concrete Mixture", "3 components (sum 1) + strength — mixture model", Concrete),
        new("Treatment Outcome", "Dose + 0/1 response — binary logistic regression, proportion", Treatment),
    };

    // ---- builders ----------------------------------------------------------

    private static Worksheet BodyMeasurements()
    {
        var ws = new Worksheet { Name = "Body Measurements" };
        var rnd = new Random(101);
        var height = new List<double>(); var weight = new List<double>();
        var age = new List<double>(); var gender = new List<string>(); var pulse = new List<double>();
        for (int i = 0; i < 40; i++)
        {
            bool male = i % 2 == 0;
            double h = (male ? 176 : 163) + Gauss(rnd) * 7;
            height.Add(h);
            weight.Add(0.9 * h - 90 + Gauss(rnd) * 6);
            age.Add(Math.Round(22 + rnd.NextDouble() * 40));
            gender.Add(male ? "M" : "F");
            pulse.Add(Math.Round(72 + Gauss(rnd) * 8));
        }
        Num(ws, "Height", height, 1); Num(ws, "Weight", weight, 1);
        Num(ws, "Age", age, 0); Txt(ws, "Gender", gender); Num(ws, "Pulse", pulse, 0);
        return ws;
    }

    private static Worksheet CropYield()
    {
        var ws = new Worksheet { Name = "Crop Yield" };
        var rnd = new Random(202);
        Num(ws, "Fertilizer A", Series(rnd, 12, 20.0, 2.0), 2);
        Num(ws, "Fertilizer B", Series(rnd, 12, 24.5, 2.5), 2);
        Num(ws, "Fertilizer C", Series(rnd, 12, 22.0, 2.2), 2);
        return ws;
    }

    private static Worksheet Coating()
    {
        var ws = new Worksheet { Name = "Coating Experiment" };
        var rnd = new Random(303);
        var resp = new List<double>(); var temp = new List<string>(); var pres = new List<string>();
        foreach (var t in new[] { "Low", "High" })
            foreach (var p in new[] { "Low", "High" })
                for (int r = 0; r < 3; r++)
                {
                    double y = 30 + (t == "High" ? 8 : 0) + (p == "High" ? 4 : 0)
                        + (t == "High" && p == "High" ? 3 : 0) + Gauss(rnd) * 1.5;
                    resp.Add(y); temp.Add(t); pres.Add(p);
                }
        Txt(ws, "Temperature", temp); Txt(ws, "Pressure", pres); Num(ws, "Hardness", resp, 2);
        return ws;
    }

    private static Worksheet MonthlySales()
    {
        var ws = new Worksheet { Name = "Monthly Sales" };
        var rnd = new Random(404);
        var sales = new List<double>();
        for (int t = 0; t < 48; t++)
            sales.Add(100 + 1.5 * t + 20 * Math.Sin(2 * Math.PI * t / 12) + Gauss(rnd) * 5);
        Num(ws, "Month", Enumerable.Range(1, 48).Select(i => (double)i), 0);
        Num(ws, "Sales", sales, 1);
        return ws;
    }

    private static Worksheet Pistons()
    {
        var ws = new Worksheet { Name = "Piston Diameters" };
        var rnd = new Random(505);
        var d = new List<double>();
        for (int i = 0; i < 50; i++) d.Add(10.00 + Gauss(rnd) * 0.02 + (i > 35 ? 0.01 : 0));
        Num(ws, "Diameter", d, 4);
        return ws;
    }

    private static Worksheet GageStudy()
    {
        var ws = new Worksheet { Name = "Gage Study" };
        var rnd = new Random(606);
        var partTrue = Enumerable.Range(0, 10).Select(_ => 50 + Gauss(rnd) * 5).ToArray();
        var opBias = new Dictionary<string, double> { ["A"] = 0.0, ["B"] = 0.4, ["C"] = -0.3 };
        var part = new List<string>(); var oper = new List<string>(); var meas = new List<double>();
        for (int p = 0; p < 10; p++)
            foreach (var o in new[] { "A", "B", "C" })
                for (int r = 0; r < 2; r++)
                {
                    part.Add($"P{p + 1}"); oper.Add(o);
                    meas.Add(partTrue[p] + opBias[o] + Gauss(rnd) * 0.5);
                }
        Txt(ws, "Part", part); Txt(ws, "Operator", oper); Num(ws, "Measurement", meas, 3);
        return ws;
    }

    private static Worksheet Bulbs()
    {
        var ws = new Worksheet { Name = "Bulb Lifetimes" };
        var rnd = new Random(707);
        var hours = new List<double>(); var censored = new List<double>();
        for (int i = 0; i < 30; i++)
        {
            double u = 1 - rnd.NextDouble();
            double life = 1000 * Math.Pow(-Math.Log(u), 1.0 / 2.0); // Weibull(shape 2, scale 1000)
            bool cens = life > 1500;                                 // study truncated at 1500 h
            hours.Add(cens ? 1500 : life);
            censored.Add(cens ? 1 : 0);
        }
        Num(ws, "Hours", hours, 1); Num(ws, "Censored", censored, 0);
        return ws;
    }

    private static Worksheet Flowers()
    {
        var ws = new Worksheet { Name = "Flower Measurements" };
        var rnd = new Random(808);
        var sl = new List<double>(); var sw = new List<double>(); var pl = new List<double>();
        var pw = new List<double>(); var sp = new List<string>();
        var means = new[] { (5.0, 3.4, 1.5, 0.25), (5.9, 2.8, 4.3, 1.3), (6.6, 3.0, 5.6, 2.0) };
        var names = new[] { "Setosa", "Versicolor", "Virginica" };
        for (int s = 0; s < 3; s++)
            for (int i = 0; i < 12; i++)
            {
                var (a, b, c, d) = means[s];
                sl.Add(a + Gauss(rnd) * 0.35); sw.Add(b + Gauss(rnd) * 0.3);
                pl.Add(c + Gauss(rnd) * 0.35); pw.Add(d + Gauss(rnd) * 0.2);
                sp.Add(names[s]);
            }
        Num(ws, "SepalLen", sl, 2); Num(ws, "SepalWid", sw, 2);
        Num(ws, "PetalLen", pl, 2); Num(ws, "PetalWid", pw, 2); Txt(ws, "Species", sp);
        return ws;
    }

    private static Worksheet Concrete()
    {
        var ws = new Worksheet { Name = "Concrete Mixture" };
        var rnd = new Random(909);
        // Simplex-centroid (3 components) + a few extra blends.
        var pts = new List<double[]>
        {
            new[]{1.0,0,0}, new[]{0,1.0,0}, new[]{0,0,1.0},
            new[]{0.5,0.5,0}, new[]{0.5,0,0.5}, new[]{0,0.5,0.5},
            new[]{1.0/3,1.0/3,1.0/3}, new[]{0.6,0.2,0.2}, new[]{0.2,0.6,0.2}, new[]{0.2,0.2,0.6},
        };
        var cem = new List<double>(); var wat = new List<double>(); var agg = new List<double>(); var str = new List<double>();
        foreach (var p in pts)
        {
            cem.Add(p[0]); wat.Add(p[1]); agg.Add(p[2]);
            str.Add(50 * p[0] + 25 * p[1] + 40 * p[2] + 30 * p[0] * p[2] + Gauss(rnd) * 0.8);
        }
        Num(ws, "Cement", cem, 4); Num(ws, "Water", wat, 4); Num(ws, "Aggregate", agg, 4); Num(ws, "Strength", str, 2);
        return ws;
    }

    private static Worksheet Treatment()
    {
        var ws = new Worksheet { Name = "Treatment Outcome" };
        var rnd = new Random(110);
        var dose = new List<double>(); var resp = new List<double>();
        for (int i = 0; i < 50; i++)
        {
            double d = 1 + rnd.NextDouble() * 9;
            double prob = 1.0 / (1 + Math.Exp(-(-3 + 0.7 * d)));
            dose.Add(d); resp.Add(rnd.NextDouble() < prob ? 1 : 0);
        }
        Num(ws, "Dose", dose, 2); Num(ws, "Responded", resp, 0);
        return ws;
    }

    // ---- helpers -----------------------------------------------------------

    private static IEnumerable<double> Series(Random rnd, int n, double mean, double sd) =>
        Enumerable.Range(0, n).Select(_ => mean + Gauss(rnd) * sd);

    private static void Num(Worksheet ws, string name, IEnumerable<double> values, int dp)
    {
        var c = ws.AddColumn(name);
        string fmt = "F" + dp;
        foreach (var v in values) c.Add(v.ToString(fmt, Inv));
        c.Type = ColumnType.Numeric;
    }

    private static void Txt(Worksheet ws, string name, IEnumerable<string> values)
    {
        var c = ws.AddColumn(name, ColumnType.Text);
        foreach (var v in values) c.Add(v);
    }

    private static double Gauss(Random rnd)
    {
        double u1 = 1.0 - rnd.NextDouble(), u2 = 1.0 - rnd.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
