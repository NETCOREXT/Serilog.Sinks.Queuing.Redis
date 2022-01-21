namespace Serilog.Sinks.Queuing.Redis;

public class LogData
{
    public DateTimeOffset Timestamp { get; set; }
    public string Id { get; set; }
    public string Data { get; set; }
}