using Netcorext.Worker;

namespace Serilog.Sinks.Queuing.Redis;

public class RedisQueuingWorker : BackgroundWorker
{
    private readonly RedisQueuingSinkOptions _options;
    private readonly IEnumerable<IWorkerRunner<RedisQueuingWorker>> _runners;
    private int _retryCount;
    
    public RedisQueuingWorker(RedisQueuingSinkOptions options, IEnumerable<IWorkerRunner<RedisQueuingWorker>> runners)
    {
        _options = options;
        _runners = runners;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.EnableWorker || !_runners.Any())
            return;

        var ct = new CancellationTokenSource();

        var tasks = _runners.Select(t => t.InvokeAsync(this, ct.Token))
                            .ToArray();

        try
        {
            await Task.WhenAll(tasks);

            _retryCount = 0;
        }
        catch (Exception e)
        {
            Log.Error(e, "${Message}", e.Message);

            ct.Cancel();

            _retryCount++;
            
            if (_retryCount <= _options.RetryLimit)
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