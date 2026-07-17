namespace DataProfiler.App.Models.Reporting;

public sealed class TableReportProgressModel
{
    public string? ContentType { get; init; }

    public int EstimatedDurationSeconds { get; init; }

    public DateTimeOffset? CurrentStageStartedOnUtc { get; init; }

    public bool IsComplete { get; init; }

    public bool IsFailed { get; init; }

    public bool IsRunning => !IsComplete && !IsFailed;

    public string? FileName { get; init; }

    public string JobId { get; init; } = string.Empty;

    public string? Message { get; init; }

    public int PercentComplete { get; init; }

    public DateTimeOffset? StartedOnUtc { get; init; }

    public string Stage { get; init; } = string.Empty;

    public DateTimeOffset UpdatedOnUtc { get; init; }
}

public sealed record TableReportEstimateModel(int SelectedTableCount, int MinimumSeconds, int MaximumSeconds)
{
    public string DisplayText => MinimumSeconds == MaximumSeconds
        ? $"Estimated time: about {FormatDuration(TimeSpan.FromSeconds(MinimumSeconds))} for {SelectedTableCount} selected table{(SelectedTableCount == 1 ? string.Empty : "s")}."
        : $"Estimated time: about {FormatDuration(TimeSpan.FromSeconds(MinimumSeconds))} to {FormatDuration(TimeSpan.FromSeconds(MaximumSeconds))} for {SelectedTableCount} selected table{(SelectedTableCount == 1 ? string.Empty : "s")}.";

    private static string FormatDuration(TimeSpan duration)
    {
        var parts = new List<string>();

        if (duration.Days > 0)
        {
            parts.Add($"{duration.Days} day{(duration.Days == 1 ? string.Empty : "s")}");
        }

        if (duration.Days > 0 || duration.Hours > 0)
        {
            parts.Add($"{duration.Hours} hour{(duration.Hours == 1 ? string.Empty : "s")}");
        }

        if (duration.Days > 0 || duration.Hours > 0 || duration.Minutes > 0)
        {
            parts.Add($"{duration.Minutes} minute{(duration.Minutes == 1 ? string.Empty : "s")}");
        }

        parts.Add($"{duration.Seconds} second{(duration.Seconds == 1 ? string.Empty : "s")}");

        return string.Join(" ", parts);
    }
}
