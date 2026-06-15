namespace StatStudio.Smoke;

internal static class Program
{
    private static int Main()
    {
        Console.WriteLine("StatStudio smoke tests");
        DataTests.Run();
        StatTests.Run();
        HypoTests.Run();
        RegrTests.Run();
        SpcTests.Run();
        NonparTests.Run();
        AdvancedTests.Run();
        TimeSeriesTests.Run();
        MultiTests.Run();
        DoeTests.Run();
        return Check.Summary();
    }
}
