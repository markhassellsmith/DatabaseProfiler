using DataProfiler.App.Models;
using DataProfiler.App.Models.Reporting;
using DataProfiler.App.Services.Connections;
using DataProfiler.App.Services.Discovery;
using DataProfiler.App.Services.Reporting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DataProfiler.App.Pages.Reports;

public class DatabaseModel : PageModel
{
    private readonly TableReportJobStore _jobStore;
    private readonly ITableReportJobQueue _jobQueue;
    private readonly SchemaDiscoveryService _schemaDiscoveryService;

    public DatabaseModel(ITableReportJobQueue jobQueue, TableReportJobStore jobStore, SchemaDiscoveryService schemaDiscoveryService)
    {
        _jobQueue = jobQueue;
        _jobStore = jobStore;
        _schemaDiscoveryService = schemaDiscoveryService;
    }

    public string? DatabaseName { get; private set; }

    public TableReportEstimateModel? Estimate { get; private set; }

    public string? ReportJobId { get; private set; }

    public TableReportProgressModel? ReportProgress { get; private set; }

    public string? ServerName { get; private set; }

    public string? StatusMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? jobId)
    {
        await LoadSelectionAsync();
        ReportJobId = ResolveJobId(jobId);
        Estimate = CreateEstimate();

        if (!string.IsNullOrWhiteSpace(ReportJobId))
        {
            ReportProgress = _jobStore.GetProgress(ReportJobId);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostGenerateAsync(CancellationToken cancellationToken)
    {
        await LoadSelectionAsync();

        if (string.IsNullOrWhiteSpace(ServerName) || string.IsNullOrWhiteSpace(DatabaseName))
        {
            return Page();
        }

        var connection = HttpContext.Session.GetConnection();
        if (connection is null)
        {
            return Page();
        }

        var selectedValues = await GetAllTableValuesAsync(cancellationToken);
        if (selectedValues.Count == 0)
        {
            StatusMessage = "No tables were found in the selected database.";
            return Page();
        }

        const bool includeTableProfileInfo = false;
        const bool includeTableDetailSheets = false;
        var estimate = CreateEstimate(selectedValues.Count);
        if (estimate is null)
        {
            return Page();
        }

        var estimatedDurationSeconds = estimate.MaximumSeconds;
        var jobId = _jobStore.CreateJob(estimatedDurationSeconds, connection, DatabaseName!, selectedValues, includeTableDetailSheets);
        HttpContext.Session.SetActiveReportJobId(jobId);

        await _jobQueue.QueueAsync(new TableReportJobRequest(jobId, connection, DatabaseName!, selectedValues, estimatedDurationSeconds, includeTableProfileInfo, includeTableDetailSheets), CancellationToken.None);
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

    private async Task LoadSelectionAsync()
    {
        var connection = HttpContext.Session.GetConnection();
        ServerName = connection?.ServerName;
        DatabaseName = connection?.SelectedDatabaseName;
        StatusMessage = string.IsNullOrWhiteSpace(ServerName) || string.IsNullOrWhiteSpace(DatabaseName)
            ? "Connect to a SQL Server instance and choose a database first."
            : null;
        await Task.CompletedTask;
    }

    private TableReportEstimateModel? CreateEstimate(int? tableCount = null)
    {
        if (string.IsNullOrWhiteSpace(DatabaseName))
        {
            return null;
        }

        var count = tableCount ?? 1;
        var minimumSeconds = Math.Max(10, count * 3);
        var maximumSeconds = Math.Max(minimumSeconds, minimumSeconds + Math.Max(5, count));
        return new TableReportEstimateModel(count, minimumSeconds, maximumSeconds);
    }

    private string? ResolveJobId(string? jobId)
    {
        if (!string.IsNullOrWhiteSpace(jobId))
        {
            return jobId;
        }

        return HttpContext.Session.GetActiveReportJobId();
    }

    private async Task<IReadOnlyCollection<string>> GetAllTableValuesAsync(CancellationToken cancellationToken)
    {
        var connection = HttpContext.Session.GetConnection();
        if (connection is null || string.IsNullOrWhiteSpace(DatabaseName))
        {
            return Array.Empty<string>();
        }

        var discovered = await _schemaDiscoveryService.DiscoverObjectBrowserAsync(connection, DatabaseName!, cancellationToken);
        return discovered.Tables.Select(table => table.SelectionValue).ToArray();
    }
}
