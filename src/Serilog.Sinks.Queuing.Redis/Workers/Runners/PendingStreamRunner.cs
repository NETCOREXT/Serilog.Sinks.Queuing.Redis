using FreeRedis;
using Netcorext.Extensions.Linq;
using Netcorext.Worker;
using Serilog.Sinks.Queuing.Redis.Extensions;

namespace Serilog.Sinks.Queuing.Redis;

public class PendingStreamRunner : IWorkerRunner<RedisQueuingWorker>
{
    private readonly RedisClient _redis;
    private readonly IEnumerable<ILogStore> _stores;
    private readonly RedisQueuingSinkOptions _options;
    private static bool _isDetecting;

    public PendingStreamRunner(IEnumerable<ILogStore> stores, RedisQueuingSinkOptions options)
    {
        _redis = new RedisClient(options.RedisConnectionString);
        _stores = stores;
        _options = options;
    }

    public async Task InvokeAsync(RedisQueuingWorker worker, CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(_options.StreamIdleTime ?? RedisQueuingSinkOptions.DEFAULT_STREAM_IDLE_TIME, cancellationToken);

            if (_isDetecting) continue;

            _isDetecting = true;

            await _redis.RegisterConsumerAsync(_options);

            try
            {
                await ClaimPendingStreamAsync(_options.StreamKey, cancellationToken);
            }
            finally
            {
                _isDetecting = false;
            }
        }
    }

    private async Task ClaimPendingStreamAsync(string streamKey, CancellationToken cancellationToken = default)
    {
        var nextId = "-";

        while (!cancellationToken.IsCancellationRequested)
        {
            var pendingResult = (await _redis.XPendingAsync(streamKey, _options.StreamGroup, nextId, "+", _options.StreamBatchSize ?? RedisQueuingSinkOptions.DEFAULT_STREAM_BATCH_SIZE))
                               .Where(t => t.idle > _options.StreamIdleTime)
                               .ToArray();

            if (!pendingResult.Any()) break;

            var pendingIds = pendingResult.Select(t => t.id).ToArray();

            nextId = "(" + pendingResult.Last().id;

            var entries = (await _redis.XClaimAsync(streamKey, _options.StreamGroup, _options.MachineName, _options.StreamIdleTime ?? RedisQueuingSinkOptions.DEFAULT_STREAM_IDLE_TIME, pendingIds))
                         .Select(t => t.ToStreamData())
                         .ToArray();

            if (entries.Length == 0) break;

            var ids = new List<string>();

            foreach (var store in _stores)
            {
                ids.AddRange(await store.InvokeAsync(entries, cancellationToken));
            }

            await _redis.XAckAsync(_options.StreamKey, _options.StreamGroup, ids.Distinct().ToArray());
        }

        var consumers = await _redis.XInfoConsumersAsync(streamKey, _options.StreamGroup);

        var pendingConsumers = consumers.Where(t => t.name != _options.MachineName && t.idle > _options.StreamIdleTime)
                                        .ToArray();

        if (pendingConsumers.Any())
            pendingConsumers.ForEach(async t => await _redis.XGroupDelConsumerAsync(streamKey, _options.StreamGroup, t.name));
    }

    public void Dispose()
    {
        _redis.Dispose();
    }
}