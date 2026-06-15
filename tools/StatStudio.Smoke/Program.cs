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
        return Check.Summary();
    }
}
