using System.Reflection;
using Serilog.Formatting;

namespace Serilog.Sinks.Queuing.Redis;

public class RedisQueuingSinkOptions
{
    public const int DEFAULT_STREAM_IDLE_TIME = 5 * 1000;
    public const int DEFAULT_STREAM_BATCH_SIZE = 50;
    public const long DEFAULT_STREAM_MAX_SIZE = 65535;
    public const int DEFAULT_HEALTH_CHECK_INTERVAL = 30 * 1000;
    
    public ITextFormatter? LogFormatter { get; set; }
    public string RedisConnectionString { get; set; } = null!;
    public string NotificationChannel { get; set; } = null!;
    public string StreamKey { get; set; } = null!;
    public string StreamGroup { get; set; } = Assembly.GetEntryAssembly()!.GetName().Name!;
    public string MachineName { get; set; } = Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName;
    public long? StreamMaxSize { get; set; } = DEFAULT_STREAM_MAX_SIZE;
    public int? StreamBatchSize { get; set; } = DEFAULT_STREAM_BATCH_SIZE;
    public int? StreamIdleTime { get; set; } = DEFAULT_STREAM_IDLE_TIME;
    public int? HealthCheckInterval { get; set; } = DEFAULT_HEALTH_CHECK_INTERVAL;
}