namespace Serilog.Sinks.Queuing.Redis;

public interface IRedisStreamHook
{
    Task<string[]> InvokeAsync(IEnumerable<LogData> logs, CancellationToken cancellationToken = default);
}