using FreeRedis;
using Netcorext.Extensions.Threading;
using Netcorext.Worker;
using Serilog.Sinks.Queuing.Redis.Extensions;

namespace Serilog.Sinks.Queuing.Redis;

internal class RedisConsumerRunner : IWorkerRunner<RedisQueuingWorker>
{
    private static KeyCountLocker _locker = null!;
    private IDisposable? _subscription;
    private readonly RedisClient _redis;
    private readonly IEnumerable<ILogStore> _stores;
    private readonly RedisQueuingSinkOptions _options;

    public RedisConsumerRunner(IEnumerable<ILogStore> stores, RedisQueuingSinkOptions options)
    {
        _locker = options.WorkerTaskLimit.HasValue
                      ? new KeyCountLocker(maximum: options.WorkerTaskLimit.Value)
                      : new KeyCountLocker();

        _redis = new RedisClient(options.RedisConnectionString);
        _stores = stores;
        _options = options;
    }

    public async Task InvokeAsync(RedisQueuingWorker worker, CancellationToken cancellationToken = default)
    {
        if (!_stores.Any())
            return;

        await _redis.RegisterConsumerAsync(_options);

        _subscription?.Dispose();

        _subscription = _redis.Subscribe(_options.StreamKey, (s, o) =>
                                                             {
                                                                 ReadStreamAsync(o.ToString()!, cancellationToken)
                                                                    .GetAwaiter()
                                                                    .GetResult();
                                                             });
    }

    private async Task ReadStreamAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await _locker.IncrementAsync(key, cancellationToken)) return;

            while (!cancellationToken.IsCancellationRequested)
            {
                var entries = (await _redis.XReadGroupAsync(_options.StreamGroup,
                                                            _options.MachineName,
                                                            _options.StreamBatchSize ?? RedisQueuingSinkOptions.DEFAULT_STREAM_BATCH_SIZE,
                                                            0,
                                                            false,
                                                            new Dictionary<string, string> { { key, ">" } }
                                                           ))
                             .SelectMany(t => t.ToStreamData())
                             .ToArray();

                if (entries.Length == 0) break;

                var ids = new List<string>();
                
                foreach (var store in _stores)
                {
                    ids.AddRange(await store.InvokeAsync(entries, cancellationToken));
                }
                
                await _redis.XAckAsync(_options.StreamKey, _options.StreamGroup, ids.Distinct().ToArray());
            }

            await _locker.DecrementAsync(key, cancellationToken);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync(e.ToString());
        }
        finally
        {
            await _locker.DecrementAsync(key, cancellationToken);
        }
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _redis.Dispose();
    }
}