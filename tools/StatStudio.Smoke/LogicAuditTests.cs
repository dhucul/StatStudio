using System.Globalization;
using StatStudio.Core.Data;
using StatStudio.Core.Statistics;
using StatStudio.Core.Statistics.Spc;
using Xunit;

namespace StatStudio.Smoke;

public sealed class LogicAuditTests
{
    [Fact]
    public void FractionalDesignsAreInvariantToResponseOffset()
    {
        for (int k = 3; k <= 7; k++)
            foreach (int runs in DoeDesign.AvailableFractions(k))
            {
                var design = DoeDesign.FractionalFactorial(k, runs, false);
                var x = Enumerable.Range(0, k).Select(j => design.RunList.Select(r => r.Factors[j]).ToArray()).ToArray();
                var result = FactorialAnalysis.Analyze(x[0].Select(v => 10 + 2 * v).ToArray(), x, design.FactorNames);
                Assert.Equal(1, result.RSquared, 10);
                Assert.Equal(4, result.Terms.Single(t => t.Name == "A").Effect, 10);
                Assert.Contains(result.Aliases, a => a.StartsWith("Constant ="));
                Assert.All(result.Terms.Where(t => t.Name != "Constant" && t.Name != "A"), t => Assert.Equal(0, t.Effect, 10));
            }
    }

    [Fact]
    public void SignedAliasesAndCenterShiftAreHandledSeparately()
    {
        var a = new double[] { -1, 1, -1, 1, 0, 0 };
        var b = new double[] { -1, -1, 1, 1, 0, 0 };
        var c = a.Zip(b, (x, y) => -x * y).ToArray();
        var y = new double[] { 8, 12, 8, 12, 15, 15 };
        var r = FactorialAnalysis.Analyze(y, new[] { a, b, c }, new[] { "A", "B", "C" });
        Assert.Equal(1, r.RSquared, 12);
        Assert.Equal(10, r.Terms.Single(t => t.Name == "Constant").Coef, 12);
        Assert.Equal(5, r.Terms.Single(t => t.Name == "Center point").Coef, 12);
        Assert.Contains("Constant = -A*B*C", r.Aliases);
        Assert.Contains("A = -B*C", r.Aliases);
    }

    [Fact]
    public void ArbitraryPowerOfTwoCornerSubsetIsRejected()
    {
        var x = new[] { new double[] { -1, -1, -1, 1 }, new double[] { -1, -1, 1, -1 }, new double[] { -1, 1, -1, -1 } };
        Assert.Throws<ArgumentException>(() => FactorialAnalysis.Analyze(new double[] { 1, 2, 3, 4 }, x, new[] { "A", "B", "C" }));
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(2000)]
    [InlineData(5000)]
    public void LargeNormalityStatisticsRejectBinaryData(int n)
    {
        var result = Normality.AndersonDarling(Enumerable.Range(0, n).Select(i => (double)(i % 2)).ToArray());
        Assert.InRange(result.P, 0, 0.000001);
        Assert.Contains("p < 0.05: reject", NonparametricFormatters.AndersonDarling(result, "X"));
        Assert.Contains("inconclusive", NonparametricFormatters.AndersonDarling(result with { P = double.PositiveInfinity }, "X"));
    }

    [Fact]
    public void MixturesSurviveNumericStorageAndCsvRoundTrip()
    {
        var design = MixtureDesign.SimplexCentroid(3, false);
        var ws = new Worksheet();
        for (int j = 0; j < 3; j++)
        {
            var column = ws.AddColumn(design.ComponentNames[j]);
            foreach (var run in design.RunList) column.AddNumber(run.Components[j]);
        }
        var y = ws.AddColumn("Y");
        foreach (var run in design.RunList) y.AddNumber(2 * run.Components[0] + 3 * run.Components[1] + 5 * run.Components[2] + 4 * run.Components[0] * run.Components[1]);
        using var writer = new StringWriter();
        WorksheetIo.WriteCsv(ws, writer);
        var back = WorksheetIo.ReadCsv(new StringReader(writer.ToString()), hasHeader: true, decodeFormulaGuards: true);
        var (response, components) = Columns.Design(back.Find("Y")!, back.Columns.Take(3).ToArray());
        var result = MixtureAnalysis.Fit(response, components, design.ComponentNames, true);
        Assert.Equal(4, result.Terms.Single(t => t.Name == "A*B").Coef, 9);
        var sample = SampleData.All.Single(d => d.Name == "Concrete Mixture").Build();
        var inputs = Columns.Design(sample.Find("Strength")!, sample.Columns.Take(3).ToArray());
        Assert.True(double.IsFinite(MixtureAnalysis.Fit(inputs.Y, inputs.X, new[] { "A", "B", "C" }, true).S));
    }

    [Fact]
    public void OrderedInputRejectsGapsButAllowsTrailingPadding()
    {
        var col = new DataColumn("Y");
        foreach (var v in new[] { "1", "*", "3", "4" }) col.Add(v);
        Assert.Throws<ArgumentException>(() => col.SeriesValues());
        col[1] = "2";
        col.Add(null); col.Add("*");
        Assert.Equal(new double[] { 1, 2, 3, 4 }, col.SeriesValues());
        col[0] = null;
        Assert.Throws<ArgumentException>(() => col.SeriesValues());
    }

    [Fact]
    public void HeaderChoicesPreserveObservationsAndEmptyWorksheets()
    {
        var missingFirst = WorksheetIo.ReadCsv(new StringReader("*,10\n1,20\n2,30"));
        Assert.Equal(3, missingFirst.RowCount);
        Assert.Equal("10", missingFirst[1][0]);
        var one = WorksheetIo.ReadCsv(new StringReader("Height\n"));
        Assert.Equal("Height", one[0].Name);
        Assert.Equal(0, one.RowCount);
        var numericHeader = WorksheetIo.ReadCsv(new StringReader("123\n4\n5"), true);
        Assert.Equal("123", numericHeader[0].Name);
        Assert.Equal(2, numericHeader.RowCount);
        var textOnly = WorksheetIo.ReadCsv(new StringReader("North\nSouth"), false);
        Assert.Equal("North", textOnly[0][0]);
    }

    [Theory]
    [InlineData("=foo")]
    [InlineData("'=foo")]
    [InlineData("''=foo")]
    [InlineData("'literal")]
    [InlineData("'")]
    [InlineData("-5.2")]
    [InlineData("@SUM(A1)")]
    public void ExplicitCsvGuardModeIsReversibleForCellsAndHeaders(string value)
    {
        var ws = new Worksheet(); ws.AddColumn(value).Add(value);
        using var output = new StringWriter(); WorksheetIo.WriteCsv(ws, output);
        var back = WorksheetIo.ReadCsv(new StringReader(output.ToString()), true, true);
        Assert.Equal(value, back[0].Name); Assert.Equal(value, back[0][0]);
        var external = WorksheetIo.ReadCsv(new StringReader("Text\n'=foo"), true);
        Assert.Equal("'=foo", external[0][0]);
    }

    [Fact]
    public void XlsxLiteralApostrophesAreNotCsvDecoded()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
        try
        {
            var ws = new Worksheet(); ws.AddColumn("ID", ColumnType.Text).Add("00123");
            ws.AddColumn("Text", ColumnType.Text).Add("'=foo");
            WorksheetIo.WriteXlsx(ws, path);
            var back = WorksheetIo.ReadXlsx(path, true);
            Assert.Equal("00123", back[0][0]); Assert.Equal("'=foo", back[1][0]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CalculatorPreservesPrecisionAndDoesNotOverwriteOnFailure()
    {
        var ws = new Worksheet(); ws.AddColumn("Foo").Add("1e-12"); ws.AddColumn("C1").Add("5");
        Calculator.EvaluateIntoColumn("Foo", ws, "Foo");
        Assert.Equal(1e-12, ws[0].NumericValues()[0]);
        Assert.Equal(5, Calculator.Evaluate("'C1'", ws)[0]);
        Assert.Equal(1e-12, Calculator.Evaluate("C1", ws)[0]);
        Assert.Throws<ArithmeticException>(() => Calculator.EvaluateIntoColumn("1/0", ws, "Foo"));
        Assert.Equal(1e-12, ws[0].NumericValues()[0]);
        Assert.Throws<FormatException>(() => Calculator.Evaluate(string.Join("+", Enumerable.Repeat("1", 10000)), ws));
    }

    [Fact]
    public void SeasonalForecastValidatesHistoryBeforeFitting()
    {
        Assert.Throws<ArgumentException>(() => Sarima.Fit(new double[] { 1, 3, 2, 5, 4, 6 }, 0, 0, 0, 0, 0, 1, 12, 1, false));
        var y = Enumerable.Range(0, 40).Select(i => Math.Sin(i * 0.7) + Math.Cos(i * 0.31)).ToArray();
        Assert.All(Sarima.Fit(y, 0, 0, 0, 0, 0, 1, 4, 3, false).Forecasts, v => Assert.True(double.IsFinite(v)));
    }

    [Fact]
    public void PowerRespectsDirectionAndReturnsMinimumWholeSampleSize()
    {
        Assert.True(Power.OneProportionPower(100, .5, .8, .05, Alternative.Less) < 1e-10);
        Assert.True(Power.OneProportionPower(100, .5, .8, .05, Alternative.Greater) > .99);
        Assert.True(Power.OneSampleTPower(100, -.5, .05, Alternative.Greater) < .001);
        Assert.Throws<ArgumentException>(() => Power.OneProportionSampleSize(.8, .5, .8, .05, Alternative.Less));
        double n = Power.OneSampleTSampleSize(.8, .5, .05, Alternative.TwoSided);
        Assert.Equal(Math.Ceiling(n), n);
        Assert.True(Power.OneSampleTPower(n, .5, .05, Alternative.TwoSided) >= .8);
        Assert.True(Power.OneSampleTPower(n - 1, .5, .05, Alternative.TwoSided) < .8);
        double two = Power.OneProportionPower(10, .5, .500001, .05, Alternative.TwoSided);
        Assert.InRange(two, .04999, .05001);
    }

    [Fact]
    public void BayesianMomentsAreDistinctFromScaleAndIntervals()
    {
        var r = Bayes.NormalMeanUnknownVar(new double[] { 1, 2, 3 });
        Assert.True(double.IsPositiveInfinity(r.PosteriorSd));
        Assert.True(double.IsFinite(r.CredLow)); Assert.True(double.IsFinite(r.CredHigh));
        Assert.True(double.IsNaN(Bayes.NormalMeanUnknownVar(new double[] { 1, 2 }).PosteriorMean));
        var y = new double[] { 1, 2, 4, 3 }; var x = new[] { new double[] { 0, 1, 2, 3 } };
        var regression = Bayes.LinearRegression(y, x, new[] { "X" });
        Assert.All(regression.Terms, t => Assert.True(double.IsPositiveInfinity(t.PosteriorSd)));
    }

    [Fact]
    public void LargeCountTotalsDoNotWrap()
    {
        var r = HypothesisTests.TwoProportions(600000000, 1500000000, 500000000, 1500000000);
        Assert.True(double.IsFinite(r.Z)); Assert.InRange(r.P, 0, 1);
        Assert.True(double.IsFinite(ControlCharts.PChart(new[] { 1, 2 }, new[] { 1500000000, 1500000000 }).Center));
        Assert.True(double.IsFinite(ControlCharts.UChart(new[] { 1500000000, 1500000000 }, new[] { 1500000000, 1500000000 }).Center));
        Assert.Throws<ArgumentOutOfRangeException>(() => FishersExact.Test(int.MaxValue, 1, 1, 1));
    }

    [Fact]
    public void IterationLimitIsNotCalledConvergence()
    {
        var pts = Enumerable.Range(0, 100).Select(i => new[] { (double)i }).ToArray();
        var r = KMeans.Cluster(pts, 3, new[] { "X" }, maxIter: 1);
        Assert.False(r.Converged);
        Assert.Contains("not converged", MultivariateFormatters.KMeans(r));
        Assert.True(KMeans.Cluster(pts, 3, new[] { "X" }).Converged);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void MovingAverageIsTrailingForEveryWindowLength(int length)
    {
        var a = TimeSeries.MovingAverage(new double[] { 1, 2, 3, 4, 5, 6 }, length);
        var b = TimeSeries.MovingAverage(new double[] { 1, 2, 3, 4, 5, 999 }, length);
        Assert.Equal(a.Fitted[4], b.Fitted[4]);
        Assert.True(double.IsNaN(a.Fitted[length - 2]));
        Assert.Equal((length + 1) / 2.0, a.Fitted[length - 1], 12);
    }

    [Fact]
    public void CancelledWorkAndOversizedAllocationsAreRejected()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel(); var token = cts.Token;
        var y = new double[] { 1, 3, 2, 5, 4, 6, 8, 7 };
        Assert.Throws<OperationCanceledException>(() => Arima.Fit(y, 1, 0, 0, cancellationToken: token));
        Assert.Throws<OperationCanceledException>(() => Sarima.Fit(y, 0, 0, 0, 0, 0, 0, 2, cancellationToken: token));
        Assert.Throws<OperationCanceledException>(() => KMeans.Cluster(y.Select(v => new[] { v }).ToArray(), 2, new[] { "X" }, cancellationToken: token));
        Assert.Throws<OperationCanceledException>(() => TimeSeries.Autocorrelation(y, 3, token));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeSeries.SingleExp(y, .2, int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResponseSurface.CentralComposite(3, int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => DoeDesign.FullFactorial(7, int.MaxValue));
        Assert.Throws<ArgumentException>(() => TimeSeries.Winters(y, int.MaxValue, .2, .1, .1, false));
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            File.WriteAllText(path, "original"); var ws = new Worksheet(); ws.AddColumn("X").Add("1");
            Assert.Throws<OperationCanceledException>(() => WorksheetIo.WriteCsv(ws, path, cancellationToken: token));
            Assert.Equal("original", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task RunningModelObservesCancellation()
    {
        var series = Enumerable.Range(0, 100000).Select(i => Math.Sin(i * .31) + Math.Cos(i * .73)).ToArray();
        using var cts = new CancellationTokenSource();
        var task = Task.Run(() =>
        {
            cts.CancelAfter(TimeSpan.FromMilliseconds(50));
            return Arima.Fit(series, 3, 0, 3, 10, true, cts.Token);
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void CsvWriterObservesCancellationBetweenRows()
    {
        var ws = new Worksheet(); var col = ws.AddColumn("X");
        foreach (var value in new[] { 1.0, 2, 3 }) col.AddNumber(value);
        using var cts = new CancellationTokenSource();
        using var writer = new CancellingWriter(cts);
        Assert.Throws<OperationCanceledException>(() => WorksheetIo.WriteCsv(ws, writer, cancellationToken: cts.Token));
        Assert.DoesNotContain("3", writer.ToString());
    }

    private sealed class CancellingWriter(CancellationTokenSource source) : StringWriter
    {
        private int _lines;
        public override void WriteLine(string? value)
        {
            base.WriteLine(value);
            if (++_lines == 2) source.Cancel();
        }
    }
}
