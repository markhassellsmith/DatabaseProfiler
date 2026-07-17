using System.Collections.Concurrent;
using DataProfiler.App.Models.Reporting;

namespace DataProfiler.App.Services.Reporting;

public sealed class TableReportJobStore
{
    private readonly ConcurrentDictionary<string, TableReportJobState> _jobs = new(StringComparer.OrdinalIgnoreCase);

    public string CreateJob(int estimatedDurationSeconds)
    {
        var jobId = Guid.NewGuid().ToString("N");
        _jobs[jobId] = new TableReportJobState(jobId, estimatedDurationSeconds);
        return jobId;
    }

    public TableReportProgressModel? GetProgress(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return null;
        }

        return job.Progress;
    }

    public TableReportExportResult? GetResult(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var job) ? job.Result : null;
    }

    public void UpdateProgress(string jobId, TableReportProgressModel progress)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Progress = new TableReportProgressModel
            {
                ContentType = progress.ContentType ?? job.Progress.ContentType,
                EstimatedDurationSeconds = progress.EstimatedDurationSeconds > 0 ? progress.EstimatedDurationSeconds : job.Progress.EstimatedDurationSeconds,
                CurrentStageStartedOnUtc = progress.CurrentStageStartedOnUtc ?? job.Progress.CurrentStageStartedOnUtc,
                FileName = progress.FileName ?? job.Progress.FileName,
                IsComplete = progress.IsComplete,
                IsFailed = progress.IsFailed,
                JobId = progress.JobId,
                Message = progress.Message,
                PercentComplete = progress.PercentComplete,
                StartedOnUtc = progress.StartedOnUtc ?? job.Progress.StartedOnUtc,
                Stage = progress.Stage,
                UpdatedOnUtc = progress.UpdatedOnUtc
            };
        }
    }

    public void Complete(string jobId, TableReportExportResult result)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Result = result;
            job.Progress = new TableReportProgressModel
            {
                ContentType = result.ContentType,
                EstimatedDurationSeconds = job.Progress.EstimatedDurationSeconds,
                CurrentStageStartedOnUtc = job.Progress.CurrentStageStartedOnUtc,
                FileName = result.FileName,
                IsComplete = true,
                JobId = jobId,
                Message = "Report is ready to download.",
                PercentComplete = 100,
                StartedOnUtc = job.Progress.StartedOnUtc,
                Stage = "Completed",
                UpdatedOnUtc = DateTimeOffset.UtcNow
            };
        }
    }

    public void Fail(string jobId, string message)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Progress = new TableReportProgressModel
            {
                IsFailed = true,
                EstimatedDurationSeconds = job.Progress.EstimatedDurationSeconds,
                CurrentStageStartedOnUtc = job.Progress.CurrentStageStartedOnUtc,
                JobId = jobId,
                Message = message,
                PercentComplete = 100,
                StartedOnUtc = job.Progress.StartedOnUtc,
                Stage = "Failed",
                UpdatedOnUtc = DateTimeOffset.UtcNow
            };
        }
    }

    private sealed class TableReportJobState
    {
        public TableReportJobState(string jobId, int estimatedDurationSeconds)
        {
            Progress = new TableReportProgressModel
            {
                EstimatedDurationSeconds = estimatedDurationSeconds,
                CurrentStageStartedOnUtc = DateTimeOffset.UtcNow,
                JobId = jobId,
                Message = "Queued",
                PercentComplete = 0,
                StartedOnUtc = DateTimeOffset.UtcNow,
                Stage = "Queued",
                UpdatedOnUtc = DateTimeOffset.UtcNow
            };
        }

        public TableReportExportResult? Result { get; set; }

        public TableReportProgressModel Progress { get; set; }
    }
}