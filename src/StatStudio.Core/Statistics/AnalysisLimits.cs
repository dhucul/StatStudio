namespace StatStudio.Core.Statistics;

/// <summary>Practical allocation limits shared by engines and input dialogs.</summary>
public static class AnalysisLimits
{
    public const int MaxForecasts = 10_000;
    public const int MaxSeason = 10_000;
    public const int MaxDesignRuns = 10_000;
    public const int MaxWorksheetCells = 2_000_000;
    public const int MaxWorksheetColumns = 512;
}
