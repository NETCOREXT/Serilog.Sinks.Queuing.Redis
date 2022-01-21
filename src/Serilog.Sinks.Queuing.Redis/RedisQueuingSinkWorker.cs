using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Sinks.Queuing.Redis.Extensions;
using StackExchange.Redis;

namespace Serilog.Sinks.Queuing.Redis;

public class RedisQueuingSinkWorker : IHostedService, IDisposable
{
    private readonly IDatabase _redis;
    private readonly ISubscriber _subscriber;
    private readonly RedisQueuingSinkOptions _options;
    private readonly ILogger<RedisQueuingSinkWorker> _logger;
    private static bool _isProcessing;
    private Task _executingTask;
    private CancellationTokenSource _stoppingCts = new CancellationTokenSource();
    private static DateTimeOffset _lastCleanTime;

    public RedisQueuingSinkWorker(IDatabase redis, ISubscriber subscriber, RedisQueuingSinkOptions options, ILogger<RedisQueuingSinkWorker> logger)
    {
        _redis = redis;
        _subscriber = subscriber;
        _options = options;
        _logger = logger;

        if (_options.OlderLogsKeepDays.HasValue)
            Task.Factory.StartNew(() => TimeIntervalAsync());
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await StreamCreateConsumerGroupAsync(_options.StreamKey, _options.StreamGroup, cancellationToken);

        await _subscriber.SubscribeAsync(_options.NotficationChannel, async (channel, value) => await ProcessRedisStreamAsync(_options.StreamKey, _options.StreamGroup, _options.MachineName, cancellationToken));
    }

    private async Task ProcessRedisStreamAsync(string streamKey, string streamGroup, string machineName, CancellationToken cancellationToken = default)
    {
        if (_isProcessing) return;

        _isProcessing = true;

        while (true)
        {
            try
            {
                var streams = (await StreamReadGroupAsync(streamKey, streamGroup, machineName, cancellationToken)).ToArray();

                if (!streams.Any()) break;

                if (_options.RedisStreamHook != null)
                    await _options.RedisStreamHook.InvokeAsync(streams.Select(t => new LogData
                                                                                   {
                                                                                       Timestamp = DateTimeOffset.Parse(t.Values.First(t2 => t2.Name == nameof(LogData.Timestamp)).Value),
                                                                                       Id = t.Id,
                                                                                       Data = t.Values.First(t2 => t2.Name == nameof(LogData.Data)).Value
                                                                                   }), cancellationToken);

                if (streams.Any())
                {
                    await StreamAcknowledgeAsync(streamKey, streamGroup, streams.Select(t => t.Id).ToArray());
                }
            }
            catch (AggregateException e)
            {
                foreach (var exception in e.InnerExceptions)
                {
                    var ex = exception.GetInnerException();

                    _logger.LogError(ex, ex.Message);
                }
            }
            catch (Exception e)
            {
                var ex = e.GetInnerException();

                _logger.LogError(ex, ex.Message);
            }

            await Task.Delay(_options.WorkerTaskDelay, cancellationToken);
        }

        _isProcessing = false;
    }

    private async Task StreamCreateConsumerGroupAsync(string streamKey, string streamGroup, CancellationToken cancellationToken = default)
    {
        var stopwatch = new Stopwatch();

        try
        {
            await _redis.StreamCreateConsumerGroupAsync(streamKey, streamGroup);
        }
        catch (RedisServerException e)
        {
            if (e.Message.Equals("BUSYGROUP Consumer Group name already exists", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(e, e.Message);
            }
            else
            {
                _logger.LogError(e, e.Message);

                throw;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);

            throw;
        }
        finally
        {
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > _options.SlowCommandTimes)
                _logger.LogWarning($"\"{nameof(IDatabase.StreamCreateConsumerGroupAsync)}\" processing too slow, elapsed: {stopwatch.Elapsed}");
        }
    }

    private async Task<IEnumerable<StreamEntry>> StreamReadGroupAsync(string streamKey, string streamGroup, string machineName, CancellationToken cancellationToken = default)
    {
        var stopwatch = new Stopwatch();

        try
        {
            stopwatch.Start();

            return await _redis.StreamReadGroupAsync(streamKey, streamGroup, machineName, count: _options.StreamBatchSize);
        }
        catch (RedisServerException e)
        {
            if (e.Message.StartsWith("NOGROUP", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(e, e.Message);

                await StreamCreateConsumerGroupAsync(streamKey, streamGroup, cancellationToken);
            }
            else
            {
                _logger.LogError(e, e.Message);
            }

            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);

            throw;
        }
        finally
        {
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > _options.SlowCommandTimes)
                _logger.LogWarning($"\"{nameof(IDatabase.StreamReadGroupAsync)}\" processing too slow, elapsed: {stopwatch.Elapsed}");
        }
    }

    private async Task<long> StreamAcknowledgeAsync(string streamKey, string streamGroup, params RedisValue[] streamIds)
    {
        var stopwatch = new Stopwatch();

        try
        {
            stopwatch.Start();

            return await _redis.StreamAcknowledgeAsync(streamKey, streamGroup, streamIds);
        }
        catch (RedisServerException e)
        {
            if (e.Message.StartsWith("NOGROUP", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(e, e.Message);

                await StreamCreateConsumerGroupAsync(streamKey, streamGroup);
            }
            else
            {
                _logger.LogError(e, e.Message);
            }

            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);

            throw;
        }
        finally
        {
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > _options.SlowCommandTimes)
                _logger.LogWarning($"\"{nameof(IDatabase.StreamAcknowledgeAsync)}\" processing too slow, elapsed: {stopwatch.Elapsed}");
        }
    }

    private async Task RemoveOlderLogsAsync(DateTimeOffset clearDate, CancellationToken cancellationToken = default)
    {
        var stopwatch = new Stopwatch();

        try
        {
            stopwatch.Start();

            var result = await _redis.ExecuteAsync("XTRIM", _options.StreamKey, "MINID", clearDate.ToUnixTimeMilliseconds());

            _logger.LogInformation($"Remove {result} older logs.");
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
        }
        finally
        {
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > _options.SlowCommandTimes)
                _logger.LogWarning($"\"{nameof(IDatabase.ExecuteAsync)}(\"XTRIM\", {_options.StreamKey}, \"MINID\", {clearDate.ToUnixTimeMilliseconds()})\" processing too slow, elapsed: {stopwatch.Elapsed}");
        }
    }

    private async Task TimeIntervalAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_options.OlderLogsKeepDays.HasValue)
            {
                var now = DateTimeOffset.UtcNow.AddDays(-_options.OlderLogsKeepDays.Value);

                now = now.Add(-now.TimeOfDay);

                if (_lastCleanTime < now)
                {
                    _lastCleanTime = now;

                    await RemoveOlderLogsAsync(now, cancellationToken);
                }
            }

            await Task.Delay(1000);
        }
    }

    #region Implementation of base class

    public virtual Task StartAsync(CancellationToken cancellationToken)
    {
        if (_stoppingCts.IsCancellationRequested)
        {
            _stoppingCts.Dispose();
            _stoppingCts = new CancellationTokenSource();
        }

        _executingTask = ExecuteAsync(_stoppingCts.Token);

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
            _stoppingCts.Cancel();
        }
        finally
        {
            await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }

    public virtual void Dispose()
    {
        _stoppingCts.Cancel();
    }

    #endregion
}