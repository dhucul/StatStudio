namespace StatStudio.Core.Statistics;

internal static class StatGuard
{
    public static void Finite(IReadOnlyList<double> values, string paramName)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Any(v => !double.IsFinite(v)))
            throw new ArgumentException("Values must be finite.", paramName);
    }

    public static void Probability(double value, string paramName)
    {
        if (!double.IsFinite(value) || value <= 0 || value >= 1)
            throw new ArgumentOutOfRangeException(paramName, "Value must be strictly between 0 and 1.");
    }

    public static void UnitInterval(double value, string paramName)
    {
        if (!double.IsFinite(value) || value < 0 || value > 1)
            throw new ArgumentOutOfRangeException(paramName, "Value must be between 0 and 1.");
    }

    public static void Design(double[] response, double[][] columns, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(names);
        if (names.Count != columns.Length)
            throw new ArgumentException("The number of names must match the number of columns.", nameof(names));
        Finite(response, nameof(response));
        for (int i = 0; i < columns.Length; i++)
        {
            if (columns[i] is null || columns[i].Length != response.Length)
                throw new ArgumentException("Every design column must match the response length.", nameof(columns));
            Finite(columns[i], nameof(columns));
        }
    }
}
