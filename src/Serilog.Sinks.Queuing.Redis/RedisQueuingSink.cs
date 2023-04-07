using FreeRedis;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Json;

namespace Serilog.Sinks.Queuing.Redis;

public class RedisQueuingSink : ILogEventSink
{
    private readonly RedisClient _redis;
    private readonly RedisQueuingSinkOptions _options;
    private readonly ITextFormatter _formatter;

    public RedisQueuingSink(RedisQueuingSinkOptions options)
    {
        _redis = new RedisClient(options.RedisConnectionString);

        _options = options;

        _formatter = _options.LogFormatter ?? new JsonFormatter();
    }

    public void Emit(LogEvent logEvent)
    {
        using var writer = new StringWriter();

        _formatter.Format(logEvent, writer);

        var values = new Dictionary<string, object>
                     {
                         { nameof(LogData.Timestamp), logEvent.Timestamp.ToUnixTimeMilliseconds() },
                         { nameof(LogData.Data), writer.ToString() }
                     };

        var tasks = new List<Task>
                    {
                        _options.StreamMaxSize.HasValue
                            ? _redis.XAddAsync(_options.StreamKey, _options.StreamMaxSize.Value, "*", values)
                            : _redis.XAddAsync(_options.StreamKey, values),
                        _redis.PublishAsync(_options.NotificationChannel, "")
                    };

        Task.WhenAll(tasks.ToArray());
    }
}