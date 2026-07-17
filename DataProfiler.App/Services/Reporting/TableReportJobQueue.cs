using System.Threading.Channels;

namespace DataProfiler.App.Services.Reporting;

public interface ITableReportJobQueue
{
    ValueTask QueueAsync(TableReportJobRequest request, CancellationToken cancellationToken);

    ValueTask<TableReportJobRequest> DequeueAsync(CancellationToken cancellationToken);
}

public sealed class TableReportJobQueue : ITableReportJobQueue
{
    private readonly Channel<TableReportJobRequest> _queue = Channel.CreateUnbounded<TableReportJobRequest>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });

    public ValueTask QueueAsync(TableReportJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _queue.Writer.WriteAsync(request, cancellationToken);
    }

    public ValueTask<TableReportJobRequest> DequeueAsync(CancellationToken cancellationToken)
    {
        return _queue.Reader.ReadAsync(cancellationToken);
    }
}