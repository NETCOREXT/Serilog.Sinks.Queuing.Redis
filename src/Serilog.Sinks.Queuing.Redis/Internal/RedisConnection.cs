using StackExchange.Redis;

namespace Serilog.Sinks.Queuing.Redis.Internal;

internal class RedisConnection
{
    private static Lazy<ConnectionMultiplexer> _lazyConnection;
    private static readonly object Locker = new object();

    public RedisConnection(string connectionString)
    {
        lock (Locker)
        {
            var config = ConfigurationOptions.Parse(connectionString);

            _lazyConnection ??= new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(config));
        }
    }

    public ConnectionMultiplexer Connection => _lazyConnection.Value;
}