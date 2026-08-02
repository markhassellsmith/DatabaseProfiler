namespace DatabaseProfiler.App.Services.Reporting;

public sealed class TableReportJobStoreOptions
{
    public TimeSpan CompletedJobRetention { get; set; } = TimeSpan.FromDays(7);

    public TimeSpan FailedJobRetention { get; set; } = TimeSpan.FromDays(2);
}