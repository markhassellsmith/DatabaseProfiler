using DataProfiler.App.Models.Reporting;
using DataProfiler.App.Services.Connections;
using Microsoft.Extensions.Hosting;

namespace DataProfiler.App.Services.Reporting;

public sealed class TableReportBackgroundService : BackgroundService
{
    private readonly ITableReportJobQueue _jobQueue;
    private readonly TableReportJobStore _jobStore;
    private readonly TableReportService _tableReportService;

    public TableReportBackgroundService(
        ITableReportJobQueue jobQueue,
        TableReportJobStore jobStore,
        TableReportService tableReportService)
    {
        _jobQueue = jobQueue;
        _jobStore = jobStore;
        _tableReportService = tableReportService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            TableReportJobRequest request;
            try
            {
                request = await _jobQueue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await ExecuteJobAsync(request, stoppingToken);
        }
    }

    private async Task ExecuteJobAsync(TableReportJobRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var progress = new Progress<TableReportProgressModel>(state => _jobStore.UpdateProgress(request.JobId, state));
            _jobStore.UpdateProgress(request.JobId, new TableReportProgressModel
            {
                EstimatedDurationSeconds = Math.Max(0, request.EstimatedDurationSeconds),
                CurrentStageStartedOnUtc = DateTimeOffset.UtcNow,
                JobId = request.JobId,
                Message = "Starting report generation.",
                PercentComplete = 1,
                StartedOnUtc = DateTimeOffset.UtcNow,
                Stage = "Queued",
                UpdatedOnUtc = DateTimeOffset.UtcNow
            });

            var report = await _tableReportService.GenerateExcelReportAsync(
                request.Connection,
                request.DatabaseName,
                request.SelectedValues,
                request.JobId,
                progress,
                cancellationToken);

            _jobStore.Complete(request.JobId, report);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _jobStore.Fail(request.JobId, "Report generation was stopped because the application is shutting down.");
        }
        catch (Exception ex)
        {
            _jobStore.Fail(request.JobId, ex.Message);
        }
    }
}