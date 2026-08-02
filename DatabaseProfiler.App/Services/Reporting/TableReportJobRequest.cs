using DatabaseProfiler.App.Services.Connections;

namespace DatabaseProfiler.App.Services.Reporting;

public sealed record TableReportJobRequest(
    string JobId,
    ConnectionSessionModel Connection,
    string DatabaseName,
    IReadOnlyCollection<string> SelectedValues,
    int EstimatedDurationSeconds,
    bool IncludeTableProfileInfo,
    bool IncludeTableDetailSheets);
