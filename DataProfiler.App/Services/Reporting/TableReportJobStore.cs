using System.Collections.Concurrent;
using System.Text.Json;
using DataProfiler.App.Models.Reporting;
using DataProfiler.App.Services.Connections;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DataProfiler.App.Services.Reporting;

public sealed class TableReportJobStore
{
    private const string DefaultContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string ProtectionsPurpose = "DataProfiler.App.Services.Reporting.TableReportJobStore/v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ConcurrentDictionary<string, TableReportJobRecord> _jobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDataProtector _protector;
    private readonly string _artifactsDirectory;
    private readonly string _jobsDirectory;
    private readonly TableReportJobStoreOptions _options;
    private readonly object _sync = new();

    public TableReportJobStore(IHostEnvironment environment, IDataProtectionProvider dataProtectionProvider, IOptions<TableReportJobStoreOptions> options)
    {
        _jobsDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "TableReports", "jobs");
        _artifactsDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "TableReports", "artifacts");
        Directory.CreateDirectory(_jobsDirectory);
        Directory.CreateDirectory(_artifactsDirectory);
        _protector = dataProtectionProvider.CreateProtector(ProtectionsPurpose);
        _options = options.Value;

        LoadJobsFromDisk();
        CleanupExpiredJobs();
    }

    public string CreateJob(int estimatedDurationSeconds, ConnectionSessionModel connection, string databaseName, IReadOnlyCollection<string> selectedValues, bool includeTableDetailSheets = true)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(selectedValues);

        var jobId = Guid.NewGuid().ToString("N");
        var job = new TableReportJobRecord
        {
            Connection = connection,
            DatabaseName = databaseName,
            EstimatedDurationSeconds = estimatedDurationSeconds,
            JobId = jobId,
            IncludeTableDetailSheets = includeTableDetailSheets,
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
            },
            SelectedValues = selectedValues.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };

        Save(job);
        return jobId;
    }

    public IReadOnlyList<TableReportJobRequest> GetPendingRequests()
    {
        CleanupExpiredJobs();

        return _jobs.Values
            .Where(job => !job.Progress.IsComplete && !job.Progress.IsFailed)
            .Select(job => new TableReportJobRequest(
                job.JobId,
                job.Connection,
                job.DatabaseName,
                job.SelectedValues,
                job.EstimatedDurationSeconds,
                job.Connection.IncludeTableProfileInfo,
                job.IncludeTableDetailSheets))
            .ToArray();
    }

    public TableReportProgressModel? GetProgress(string jobId)
    {
        CleanupExpiredJobs();

        var job = GetOrLoad(jobId);
        return job?.Progress;
    }

    public TableReportExportResult? GetResult(string jobId)
    {
        CleanupExpiredJobs();

        var job = GetOrLoad(jobId);
        if (job is null || !job.Progress.IsComplete || string.IsNullOrWhiteSpace(job.ResultFilePath) || !File.Exists(job.ResultFilePath))
        {
            return null;
        }

        return new TableReportExportResult(
            File.ReadAllBytes(job.ResultFilePath),
            job.ResultContentType ?? job.Progress.ContentType ?? DefaultContentType,
            job.ResultFileName ?? job.Progress.FileName ?? $"{job.JobId}.xlsx");
    }

    public void UpdateProgress(string jobId, TableReportProgressModel progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        lock (_sync)
        {
            var job = GetOrLoad(jobId);
            if (job is null)
            {
                return;
            }

            job.Progress = new TableReportProgressModel
            {
                ContentType = progress.ContentType ?? job.Progress.ContentType,
                EstimatedDurationSeconds = progress.EstimatedDurationSeconds > 0 ? progress.EstimatedDurationSeconds : job.Progress.EstimatedDurationSeconds,
                CurrentStageStartedOnUtc = progress.CurrentStageStartedOnUtc ?? job.Progress.CurrentStageStartedOnUtc,
                FileName = progress.FileName ?? job.Progress.FileName,
                IsComplete = progress.IsComplete,
                IsFailed = progress.IsFailed,
                JobId = job.JobId,
                Message = progress.Message,
                PercentComplete = progress.PercentComplete,
                StartedOnUtc = progress.StartedOnUtc ?? job.Progress.StartedOnUtc,
                Stage = progress.Stage,
                UpdatedOnUtc = progress.UpdatedOnUtc
            };

            Save(job);
        }
    }

    public int CleanupExpiredJobs()
    {
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            var removedCount = 0;

            foreach (var job in _jobs.Values.ToArray())
            {
                if (!ShouldRemove(job, now))
                {
                    continue;
                }

                RemoveJobFiles(job.JobId, job.ResultFilePath);
                if (_jobs.TryRemove(job.JobId, out _))
                {
                    removedCount++;
                }
            }

            CleanupOrphanArtifacts(now);
            return removedCount;
        }
    }

    public void Complete(string jobId, TableReportExportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_sync)
        {
            var job = GetOrLoad(jobId);
            if (job is null)
            {
                return;
            }

            var artifactPath = GetArtifactPath(jobId);
            File.WriteAllBytes(artifactPath, result.Content);

            job.ResultFilePath = artifactPath;
            job.ResultContentType = result.ContentType;
            job.ResultFileName = result.FileName;
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

            Save(job);
        }
    }

    public void Fail(string jobId, string message)
    {
        lock (_sync)
        {
            var job = GetOrLoad(jobId);
            if (job is null)
            {
                return;
            }

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

            Save(job);
        }
    }

    private TableReportJobRecord? GetOrLoad(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            return job;
        }

        var loadedJob = Load(jobId);
        if (loadedJob is not null)
        {
            _jobs[loadedJob.JobId] = loadedJob;
        }

        return loadedJob;
    }

    private void LoadJobsFromDisk()
    {
        foreach (var filePath in Directory.EnumerateFiles(_jobsDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            var job = LoadFromFile(filePath);
            if (job is not null)
            {
                _jobs[job.JobId] = job;
            }
        }
    }

    private TableReportJobRecord? Load(string jobId)
    {
        var filePath = GetJobPath(jobId);
        return File.Exists(filePath) ? LoadFromFile(filePath) : null;
    }

    private TableReportJobRecord? LoadFromFile(string filePath)
    {
        try
        {
            var protectedPayload = File.ReadAllText(filePath);
            var payload = _protector.Unprotect(protectedPayload);
            return JsonSerializer.Deserialize<TableReportJobRecord>(payload, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private void Save(TableReportJobRecord job)
    {
        var json = JsonSerializer.Serialize(job, JsonOptions);
        var protectedPayload = _protector.Protect(json);
        var filePath = GetJobPath(job.JobId);
        var tempPath = filePath + ".tmp";

        File.WriteAllText(tempPath, protectedPayload);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        File.Move(tempPath, filePath);
        _jobs[job.JobId] = job;
    }

    private void CleanupOrphanArtifacts(DateTimeOffset now)
    {
        var retention = GetRetentionWindow();
        if (retention <= TimeSpan.Zero)
        {
            return;
        }

        foreach (var artifactPath in Directory.EnumerateFiles(_artifactsDirectory, "*.xlsx", SearchOption.TopDirectoryOnly))
        {
            var jobId = Path.GetFileNameWithoutExtension(artifactPath);
            if (_jobs.ContainsKey(jobId) || File.Exists(GetJobPath(jobId)))
            {
                continue;
            }

            if (IsFileOlderThan(artifactPath, now, retention))
            {
                TryDeleteFile(artifactPath);
            }
        }
    }

    private void RemoveJobFiles(string jobId, string? artifactPath)
    {
        TryDeleteFile(GetJobPath(jobId));
        if (!string.IsNullOrWhiteSpace(artifactPath))
        {
            TryDeleteFile(artifactPath);
        }
        else
        {
            TryDeleteFile(GetArtifactPath(jobId));
        }
    }

    private bool ShouldRemove(TableReportJobRecord job, DateTimeOffset now)
    {
        if (!job.Progress.IsComplete && !job.Progress.IsFailed)
        {
            return false;
        }

        var retention = job.Progress.IsComplete ? _options.CompletedJobRetention : _options.FailedJobRetention;
        if (retention <= TimeSpan.Zero)
        {
            return true;
        }

        var updatedOnUtc = job.Progress.UpdatedOnUtc == default ? now : job.Progress.UpdatedOnUtc;
        return updatedOnUtc.Add(retention) <= now;
    }

    private TimeSpan GetRetentionWindow()
    {
        var completed = _options.CompletedJobRetention;
        var failed = _options.FailedJobRetention;

        return completed > failed ? completed : failed;
    }

    private static bool IsFileOlderThan(string filePath, DateTimeOffset now, TimeSpan retention)
    {
        var lastWriteTimeUtc = File.GetLastWriteTimeUtc(filePath);
        if (lastWriteTimeUtc == DateTime.MinValue)
        {
            return false;
        }

        return new DateTimeOffset(lastWriteTimeUtc, TimeSpan.Zero).Add(retention) <= now;
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
        }
    }

    private string GetArtifactPath(string jobId)
    {
        return Path.Combine(_artifactsDirectory, $"{jobId}.xlsx");
    }

    private string GetJobPath(string jobId)
    {
        return Path.Combine(_jobsDirectory, $"{jobId}.json");
    }

    private sealed class TableReportJobRecord
    {
        public ConnectionSessionModel Connection { get; set; } = new();

        public string DatabaseName { get; set; } = string.Empty;

        public int EstimatedDurationSeconds { get; set; }

        public string JobId { get; set; } = string.Empty;

        public string? ResultContentType { get; set; }

        public string? ResultFileName { get; set; }

        public string? ResultFilePath { get; set; }

        public TableReportProgressModel Progress { get; set; } = new();

        public string[] SelectedValues { get; set; } = Array.Empty<string>();

        public bool IncludeTableDetailSheets { get; set; } = true;
    }
}
