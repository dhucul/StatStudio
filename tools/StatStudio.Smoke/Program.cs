namespace StatStudio.Smoke;

internal static class Program
{
    private static int Main()
    {
        Check.Reset();
        Console.WriteLine("StatStudio smoke tests");

        // Each suite is isolated so one throwing suite is reported as a failure rather than
        // aborting the run and hiding every result after it.
        var suites = new (string Name, Action Run)[]
        {
            ("Data", DataTests.Run),
            ("Stat", StatTests.Run),
            ("Hypothesis", HypoTests.Run),
            ("Regression", RegrTests.Run),
            ("SPC", SpcTests.Run),
            ("Nonparametric", NonparTests.Run),
            ("Advanced", AdvancedTests.Run),
            ("Time series", TimeSeriesTests.Run),
            ("Multivariate", MultiTests.Run),
            ("DOE", DoeTests.Run),
            ("Factor / reliability", FaRelTests.Run),
            ("Bayes / mixed", BayesMixedTests.Run),
            ("Edge cases", EdgeTests.Run),
        };

        foreach (var (name, run) in suites)
        {
            try { run(); }
            catch (Exception ex) { Check.True(false, $"{name} suite threw {ex.GetType().Name}: {ex.Message}"); }
        }

        return Check.Summary();
    }
}
