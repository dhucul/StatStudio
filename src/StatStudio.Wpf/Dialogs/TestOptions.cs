using System.Globalization;
using StatStudio.Core.Statistics;

namespace StatStudio.Wpf.Dialogs;

/// <summary>Shared parsing for the alternative-hypothesis and confidence-level inputs.</summary>
internal static class TestOptions
{
    public static readonly string[] AltLabels = { "Two-sided  (≠)", "Less than  (<)", "Greater than  (>)" };

    public static Alternative ParseAlt(int index) => index switch
    {
        1 => Alternative.Less,
        2 => Alternative.Greater,
        _ => Alternative.TwoSided,
    };

    /// <summary>Accepts "95" or "0.95" and requires a confidence strictly between zero and one.</summary>
    public static bool TryParseConf(string? text, out double confidence)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            confidence = v > 1 ? v / 100.0 : v;
            return double.IsFinite(confidence) && confidence > 0 && confidence < 1;
        }
        confidence = default;
        return false;
    }

    public static bool ParseDouble(string? text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
        double.IsFinite(value);

    public static bool ParseInt(string? text, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
}
