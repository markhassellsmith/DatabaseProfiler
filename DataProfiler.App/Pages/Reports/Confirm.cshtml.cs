using DataProfiler.App.Models;
using DataProfiler.App.Models.Reporting;
using DataProfiler.App.Services.Connections;
using DataProfiler.App.Services.Reporting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DataProfiler.App.Pages.Reports;

public class ConfirmModel : PageModel
{
    private readonly TableReportJobStore _jobStore;
    private readonly ITableReportJobQueue _jobQueue;

    public ConfirmModel(ITableReportJobQueue jobQueue, TableReportJobStore jobStore)
    {
        _jobQueue = jobQueue;
        _jobStore = jobStore;
    }

    public string? DatabaseName { get; private set; }

    public TableReportEstimateModel? Estimate { get; private set; }

    public string? ReportJobId { get; private set; }

    public TableReportProgressModel? ReportProgress { get; private set; }

    public string? ServerName { get; private set; }

    public IReadOnlyList<SchemaTableModel> SelectedTables { get; private set; } = Array.Empty<SchemaTableModel>();

    public string? StatusMessage { get; private set; }

    [BindProperty]
    public bool IncludeTableProfileInfo { get; set; } = true;

    public async Task<IActionResult> OnGetAsync(string? jobId, bool? includeTableProfileInfo)
    {
        var connection = HttpContext.Session.GetConnection();
        IncludeTableProfileInfo = includeTableProfileInfo ?? connection?.IncludeTableProfileInfo ?? true;
        HttpContext.Session.SetReportTableSelection(connection?.SelectedReportTableValues ?? Array.Empty<string>(), IncludeTableProfileInfo);

        await LoadSelectionAsync();
        ReportJobId = ResolveJobId(jobId);
        Estimate = CreateEstimate(SelectedTables);

        if (!string.IsNullOrWhiteSpace(ReportJobId))
        {
            ReportProgress = _jobStore.GetProgress(ReportJobId);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostGenerateAsync(CancellationToken cancellationToken)
    {
        var connection = HttpContext.Session.GetConnection();
        if (connection is not null)
        {
            IncludeTableProfileInfo = connection.IncludeTableProfileInfo;
        }

        await LoadSelectionAsync();

        if (string.IsNullOrWhiteSpace(ServerName) || string.IsNullOrWhiteSpace(DatabaseName) || SelectedTables.Count == 0)
        {
            return Page();
        }

        connection = HttpContext.Session.GetConnection();
        var selectedValues = connection?.SelectedReportTableValues ?? Array.Empty<string>();
        var includeTableProfileInfo = IncludeTableProfileInfo;
        HttpContext.Session.SetReportTableSelection(selectedValues, includeTableProfileInfo);
        Estimate = CreateEstimate(SelectedTables);
        Estimate = includeTableProfileInfo
            ? Estimate
            : new TableReportEstimateModel(Estimate?.SelectedTableCount ?? SelectedTables.Count, 5, 10);
        var estimatedDurationSeconds = Estimate?.MaximumSeconds ?? 0;
        var jobId = _jobStore.CreateJob(estimatedDurationSeconds, connection!, DatabaseName!, selectedValues);
        HttpContext.Session.SetActiveReportJobId(jobId);

        await _jobQueue.QueueAsync(new TableReportJobRequest(
            JobId: jobId,
            Connection: connection!,
            DatabaseName: DatabaseName!,
            SelectedValues: selectedValues,
            EstimatedDurationSeconds: estimatedDurationSeconds,
            IncludeTableProfileInfo: includeTableProfileInfo,
            IncludeTableDetailSheets: true), CancellationToken.None);
        return RedirectToPage(new { jobId });
    }

    public IActionResult OnGetStatus(string? jobId)
    {
        var resolvedJobId = ResolveJobId(jobId);
        if (string.IsNullOrWhiteSpace(resolvedJobId))
        {
            return NotFound();
        }

        var progress = _jobStore.GetProgress(resolvedJobId);
        return progress is null ? NotFound() : new JsonResult(progress);
    }

    public IActionResult OnGetDownload(string? jobId)
    {
        var resolvedJobId = ResolveJobId(jobId);
        if (string.IsNullOrWhiteSpace(resolvedJobId))
        {
            return NotFound();
        }

        var report = _jobStore.GetResult(resolvedJobId);
        if (report is null)
        {
            var progress = _jobStore.GetProgress(resolvedJobId);
            if (progress?.IsFailed == true)
            {
                return BadRequest(progress.Message ?? "Report generation failed.");
            }

            return StatusCode(StatusCodes.Status409Conflict, "The report is not ready yet.");
        }

        HttpContext.Session.ClearActiveReportJobId();

        return File(report.Content, report.ContentType, report.FileName);
    }

    private Task LoadSelectionAsync()
    {
        var connection = HttpContext.Session.GetConnection();
        ServerName = connection?.ServerName;
        DatabaseName = connection?.SelectedDatabaseName;

        var selectedValues = connection?.SelectedReportTableValues ?? Array.Empty<string>();
        SelectedTables = selectedValues
            .Select(value => ParseSelectionValue(value))
            .Where(table => table is not null)
            .Select(table => table!)
            .ToArray();

        StatusMessage = SelectedTables.Count == 0
            ? "No tables are selected yet. Go back and choose one or more tables."
            : null;

        return Task.CompletedTask;
    }

    private TableReportEstimateModel? CreateEstimate(IReadOnlyList<SchemaTableModel> selectedTables)
    {
        if (selectedTables.Count <= 0)
        {
            return null;
        }

        var tableCount = selectedTables.Count;
        var totalRowCount = selectedTables.Sum(table => Math.Max(0L, table.RowCount));
        var totalColumnCount = selectedTables.Sum(table => Math.Max(0, table.ColumnCount));
        var rowBlocks = (int)Math.Ceiling(totalRowCount / 250_000d);

        var minimumSeconds = Math.Max(10, (tableCount * 8) + (rowBlocks * 8) + totalColumnCount);
        var maximumSeconds = Math.Max(minimumSeconds, minimumSeconds + (tableCount * 4) + (rowBlocks * 6) + Math.Max(4, totalColumnCount / 2));
        return new TableReportEstimateModel(tableCount, minimumSeconds, maximumSeconds);
    }

    private string? ResolveJobId(string? jobId)
    {
        if (!string.IsNullOrWhiteSpace(jobId))
        {
            return jobId;
        }

        return HttpContext.Session.GetActiveReportJobId();
    }

    private static SchemaTableModel? ParseSelectionValue(string selectionValue)
    {
        if (string.IsNullOrWhiteSpace(selectionValue))
        {
            return null;
        }

        var parts = selectionValue.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        return new SchemaTableModel
        {
            SchemaName = parts[0],
            Name = parts[1]
        };
    }
}