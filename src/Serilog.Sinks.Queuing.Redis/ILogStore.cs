namespace Serilog.Sinks.Queuing.Redis;

public interface ILogStore : IDisposable
{
    Task<string[]> InvokeAsync(IEnumerable<LogData> logs, CancellationToken cancellationToken = default);
}