using System.Text;
using FreeRedis;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace Serilog.Sinks.Queuing.Redis;

public class RedisQueuingSink : ILogEventSink
{
    private readonly RedisClient _redis;
    private readonly RedisQueuingSinkOptions _options;

    public RedisQueuingSink(RedisQueuingSinkOptions options)
    {
        _redis = new RedisClient(options.RedisConnectionString);

        _options = options;
    }

    public void Emit(LogEvent logEvent)
    {
        try
        {
            using var writer = new StringWriter();

            var formatter = _options.LogFormatter ?? new JsonFormatter();
            
            formatter.Format(logEvent, writer);
            
            var values = new Dictionary<string, object>
                         {
                             { nameof(LogData.Timestamp), logEvent.Timestamp.ToUnixTimeMilliseconds() },
                             { nameof(LogData.Data), writer.ToString() }
                         };

            var tasks = new List<Task>
                        {
                            _redis.XAddAsync(_options.StreamKey, _options.StreamMaxSize ?? RedisQueuingSinkOptions.DEFAULT_STREAM_MAX_SIZE, "*", values),
                            _redis.PublishAsync(_options.NotificationChannel, "")
                        };

            Task.WhenAll(tasks.ToArray());
        }
        catch (Exception e)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{typeof(RedisQueuingSink)}.{nameof(Emit)} fatal error: {e.Message}\n{e}\n");
            sb.AppendLine("LogEvent:");
            sb.AppendLine($"Timestamp: {logEvent.Timestamp}");
            sb.AppendLine($"Level: {logEvent.Level}");
            sb.AppendLine($"MessageTemplate: {logEvent.MessageTemplate}");
            sb.AppendLine("Properties:");

            foreach (var prop in logEvent.Properties)
            {
                sb.AppendLine($"  {prop.Key}: {prop.Value}");
            }

            sb.AppendLine($"Exception: {logEvent.Exception}");

            Console.Error.WriteLine(sb.ToString());
        }
    }
}