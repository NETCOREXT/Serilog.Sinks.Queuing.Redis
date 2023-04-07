using Netcorext.Worker;

namespace Serilog.Sinks.Queuing.Redis;

public class RedisQueuingWorker : BackgroundWorker
{
    private readonly IEnumerable<IWorkerRunner<RedisQueuingWorker>> _runners;

    public RedisQueuingWorker(IEnumerable<IWorkerRunner<RedisQueuingWorker>> runners)
    {
        _runners = runners;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_runners.Any())
            return;

        var ct = new CancellationTokenSource();

        var tasks = _runners.Select(t => t.InvokeAsync(this, ct.Token))
                            .ToArray();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception e)
        {
            Log.Error(e, "${Message}", e.Message);

            ct.Cancel();
        }
        finally
        {
            await ExecuteAsync(cancellationToken);
        }
    }

    public override void Dispose()
    {
        foreach (var disposable in _runners)
        {
            disposable.Dispose();
        }

        base.Dispose();
    }
}