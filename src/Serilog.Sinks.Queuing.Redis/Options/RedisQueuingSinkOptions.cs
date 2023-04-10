using System.Reflection;
using Serilog.Formatting;

namespace Serilog.Sinks.Queuing.Redis;

public class RedisQueuingSinkOptions
{
    public const int DEFAULT_STREAM_IDLE_TIME = 5 * 1000;
    public const int DEFAULT_STREAM_BATCH_SIZE = 100;
    public const long DEFAULT_STREAM_MAX_SIZE = 65535;
    public const int DEFAULT_WORKER_TASK_LIMIT = 5;
    public const int DEFAULT_RETRY_LIMIT = 3;

    public bool EnableWorker { get; set; }
    
    public ITextFormatter? LogFormatter { get; set; }
    public string MachineName { get; set; } = Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName;
    public string NotificationChannel { get; set; } = "serilog";
    public bool GroupNewestId { get; set; }
    public string RedisConnectionString { get; set; } = "0.0.0.0:6379";
    public int? StreamBatchSize { get; set; } = DEFAULT_STREAM_BATCH_SIZE;
    public string StreamGroup { get; set; } = Assembly.GetEntryAssembly()!.GetName().Name!;
    public int? StreamIdleTime { get; set; } = DEFAULT_STREAM_IDLE_TIME;
    public string StreamKey { get; set; } = "serilog";
    public long? StreamMaxSize { get; set; } = DEFAULT_STREAM_MAX_SIZE;
    public int? WorkerTaskLimit { get; set; } = DEFAULT_WORKER_TASK_LIMIT;
    public int? RetryLimit { get; set; } = DEFAULT_RETRY_LIMIT;
}