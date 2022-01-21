using System.Reflection;
using Serilog.Formatting;

namespace Serilog.Sinks.Queuing.Redis;

public class RedisQueuingSinkOptions
{
    public ITextFormatter LogFormatter { get; set; }
    public IRedisStreamHook RedisStreamHook { get; set; }
    public string RedisConnectionString { get; set; }
    public string NotficationChannel { get; set; }
    public string StreamKey { get; set; }
    public string StreamGroup { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name;
    public string MachineName { get; set; } = Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName;
    public int? StreamMaxSize { get; set; }
    public int? StreamBatchSize { get; set; } = 50;
    public int SlowCommandTimes { get; set; } = 1000 * 2;
    public int WorkerTaskDelay { get; set; } = 1000;
    public int? OlderLogsKeepDays { get; set; }
}