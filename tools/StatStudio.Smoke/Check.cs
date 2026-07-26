namespace StatStudio.Smoke;

/// <summary>Tiny assertion harness for the deterministic numeric self-tests.</summary>
internal static class Check
{
    public static int Passed;
    public static int Failed;

    public static void Reset()
    {
        Passed = 0;
        Failed = 0;
    }

    public static void Section(string name) => Console.WriteLine($"\n== {name} ==");

    public static void True(bool cond, string label)
    {
        if (cond) { Passed++; Console.WriteLine($"  PASS  {label}"); }
        else { Failed++; Console.WriteLine($"  FAIL  {label}"); }
    }

    /// <summary>Relative+absolute tolerance compare, suitable for stats values.</summary>
    public static void Close(double actual, double expected, string label, double tol = 1e-4)
    {
        bool ok = Math.Abs(actual - expected) <= tol * (1 + Math.Abs(expected));
        if (ok) { Passed++; Console.WriteLine($"  PASS  {label}  ({Fmt(actual)} ~ {Fmt(expected)})"); }
        else { Failed++; Console.WriteLine($"  FAIL  {label}  (got {Fmt(actual)}, expected {Fmt(expected)})"); }
    }

    public static void Equal(int actual, int expected, string label) =>
        True(actual == expected, $"{label}  ({actual} == {expected})");

    public static void Equal(string actual, string expected, string label) =>
        True(actual == expected, $"{label}  ('{actual}' == '{expected}')");

    public static void Throws<TException>(Action action, string label) where TException : Exception
    {
        try
        {
            action();
            True(false, $"{label} (no exception)");
        }
        catch (TException)
        {
            True(true, label);
        }
        catch (Exception ex)
        {
            True(false, $"{label} (threw {ex.GetType().Name})");
        }
    }

    public static int Summary()
    {
        Console.WriteLine($"\n{Passed} passed, {Failed} failed.");
        return Failed == 0 ? 0 : 1;
    }

    private static string Fmt(double v) => v.ToString("G6", System.Globalization.CultureInfo.InvariantCulture);
}
