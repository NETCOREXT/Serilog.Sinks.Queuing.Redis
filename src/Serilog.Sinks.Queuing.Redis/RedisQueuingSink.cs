using System.Text;
using Serilog.Core;
using Serilog.Events;
using StackExchange.Redis;

namespace Serilog.Sinks.Queuing.Redis;

public class RedisQueuingSink : ILogEventSink
{
    private readonly IDatabase _redis;
    private readonly RedisQueuingSinkOptions _options;

    public RedisQueuingSink(IDatabase redis, RedisQueuingSinkOptions options)
    {
        _redis = redis;
        _options = options;
    }

    public void Emit(LogEvent logEvent)
    {
        try
        {
            using var writer = new StringWriter();

            _options.LogFormatter.Format(logEvent, writer);

            var entries = new NameValueEntry[]
                          {
                              new(nameof(LogData.Timestamp), logEvent.Timestamp.ToString("O")),
                              new(nameof(LogData.Data), writer.ToString())
                          };

            var tasks = new List<Task>();

            tasks.Add(_redis.StreamAddAsync(_options.StreamKey, entries, maxLength: _options.StreamMaxSize, useApproximateMaxLength: true));
            tasks.Add(_redis.PublishAsync(_options.NotficationChannel, ""));

            Task.WaitAll(tasks.ToArray());
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