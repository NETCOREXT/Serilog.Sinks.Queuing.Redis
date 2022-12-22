using System.Collections.Concurrent;
using FreeRedis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Netcorext.Extensions.Linq;
using Serilog.Sinks.Queuing.Redis.Extensions;

namespace Serilog.Sinks.Queuing.Redis.Internals;

internal class RedisQueuingSinkWorker : IHostedService, IDisposable
{
    private Task? _executingTask;
    private CancellationTokenSource _cancellationToken = new ();

    private readonly RedisClient _redis;
    private readonly IRedisStreamHook _hook;
    private readonly ILogger<RedisQueuingSinkWorker> _logger;
    private IDisposable? _subscribe;

    private readonly string _channelKey;
    private readonly string _streamKey;
    private readonly string _groupName;
    private readonly string _consumerName;
    private readonly int _streamBatchSize;
    private readonly int _streamIdleTime;
    private readonly int _healthCheckInterval;

    private static readonly ConcurrentDictionary<string, string> ReadStreamDictionary = new();
    private static bool _isDetecting;
    
    public RedisQueuingSinkWorker(RedisClient redis, IRedisStreamHook hook, RedisQueuingSinkOptions options, ILogger<RedisQueuingSinkWorker> logger)
    {
        _redis = redis;
        _hook = hook;
        _logger = logger;

        _channelKey = options.NotificationChannel;
        _streamKey = options.StreamKey;
        _groupName = options.StreamGroup;
        _consumerName = options.MachineName;
        _streamBatchSize = options.StreamBatchSize ?? RedisQueuingSinkOptions.DEFAULT_STREAM_BATCH_SIZE;
        _streamIdleTime = options.StreamIdleTime ?? RedisQueuingSinkOptions.DEFAULT_STREAM_IDLE_TIME;
        _healthCheckInterval = options.HealthCheckInterval ?? RedisQueuingSinkOptions.DEFAULT_HEALTH_CHECK_INTERVAL;
    }
    
    public async Task InvokeAsync(RedisQueuingSinkWorker worker, CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();

        if (!await _redis.ExistsAsync(_streamKey) || (await _redis.XInfoGroupsAsync(_streamKey)).All(x => x.name != _groupName))
            await _redis.XGroupCreateAsync(_streamKey, _groupName, "$", true);

        var consumers = await _redis.XInfoConsumersAsync(_streamKey, _groupName);

        if (consumers.All(x => x.name != _consumerName))
            await _redis.XGroupCreateConsumerAsync(_streamKey, _groupName, _consumerName);

        tasks.Add(Task.Run(() =>
                           {
                               _subscribe?.Dispose();

                               _subscribe = _redis.Subscribe(_channelKey, async (s, o) =>
                                                                          {
                                                                              try
                                                                              {
                                                                                  await ReadStreamAsync(cancellationToken);
                                                                              }
                                                                              catch
                                                                              {
                                                                                  // ignored
                                                                              }
                                                                          });
                           }, cancellationToken));

        tasks.Add(DetectPendingStreamAsync(cancellationToken));
        tasks.Add(HealthCheckAsync(cancellationToken));

        try
        {
            await Task.WhenAll(tasks.ToArray());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Message}", e);

            await InvokeAsync(worker, cancellationToken);
        }
    }

    private async Task ReadStreamAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ReadStreamDictionary.TryAdd(_streamKey, ">")) return;

            while (!cancellationToken.IsCancellationRequested)
            {
                var entries = (await _redis.XReadGroupAsync(_groupName,
                                                            _consumerName,
                                                            _streamBatchSize,
                                                            0,
                                                            false,
                                                            new Dictionary<string, string> { { _streamKey, ">" } }
                                                           ))
                             .SelectMany(t => t.ToLogData())
                             .ToArray();

                if (entries.Length == 0) break;

                var streamIds = await _hook.InvokeAsync(entries, cancellationToken);

                if (streamIds.Any())
                    await _redis.XAckAsync(_streamKey, _groupName, streamIds);
            }

            ReadStreamDictionary.TryRemove(_streamKey, out _);
        }
        catch (Exception e)
        {
            ReadStreamDictionary.TryRemove(_streamKey, out _);

            _logger.LogError(e, "{Message}", e);
        }
    }

    private async Task ClaimPendingStreamAsync(CancellationToken cancellationToken = default)
    {
        var nextId = "-";

        while (!cancellationToken.IsCancellationRequested)
        {
            var pendingResult = (await _redis.XPendingAsync(_streamKey, _groupName, nextId, "+", _streamBatchSize))
                               .Where(t => t.idle > _streamIdleTime)
                               .ToArray();

            if (!pendingResult.Any()) break;

            var pendingIds = pendingResult.Select(t => t.id).ToArray();

            nextId = "(" + pendingResult.Last().id;

            var entries = (await _redis.XClaimAsync(_streamKey, _groupName, _consumerName, _streamIdleTime, pendingIds))
                         .Select(t => t.ToLogData())
                         .ToArray();

            if (entries.Length == 0) break;

            var streamIds = await _hook.InvokeAsync(entries, cancellationToken);

            if (streamIds.Any())
                await _redis.XAckAsync(_streamKey, _groupName, streamIds);
        }

        var consumers = await _redis.XInfoConsumersAsync(_streamKey, _groupName);

        var pendingConsumers = consumers.Where(t => t.name != _consumerName && t.idle > _streamIdleTime)
                                        .ToArray();

        if (pendingConsumers.Any())
            pendingConsumers.ForEach(async t => await _redis.XGroupDelConsumerAsync(_streamKey, _groupName, t.name));
    }

    private async Task DetectPendingStreamAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(_streamIdleTime, cancellationToken);

            if (_isDetecting) continue;

            _isDetecting = true;

            try
            {
                await ClaimPendingStreamAsync(cancellationToken);
            }
            finally
            {
                _isDetecting = false;
            }
        }
    }

    private async Task HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Run(() =>
                           {
                               var pong = _redis.Echo("PONG");

                               _logger.LogDebug("{Message}", pong);
                           }, cancellationToken);

            await Task.Delay(_healthCheckInterval, cancellationToken);
        }
    }

    protected async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await InvokeAsync(this, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Message}", e);
        }
    }

    public virtual Task StartAsync(CancellationToken cancellationToken)
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            _cancellationToken.Dispose();
            _cancellationToken = new CancellationTokenSource();
        }

        _executingTask = ExecuteAsync(_cancellationToken.Token);

        return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
    }

    public virtual async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask == null)
        {
            return;
        }

        try
        {
            _cancellationToken.Cancel();
        }
        finally
        {
            await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }

    public virtual void Dispose()
    {
        _cancellationToken.Cancel();
    }
}