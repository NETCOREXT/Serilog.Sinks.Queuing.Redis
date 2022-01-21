namespace Serilog.Sinks.Queuing.Redis;

public interface IRedisStreamHook
{
    Task InvokeAsync(IEnumerable<LogData> logs, CancellationToken cancellationToken = default);
}