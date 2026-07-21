using DataProfiler.App.Services.Connections;

namespace DataProfiler.App.Services.Reporting;

public sealed record TableReportJobRequest(
    string JobId,
    ConnectionSessionModel Connection,
    string DatabaseName,
    IReadOnlyCollection<string> SelectedValues,
    int EstimatedDurationSeconds,
    bool IncludeTableProfileInfo,
    bool IncludeTableDetailSheets);
