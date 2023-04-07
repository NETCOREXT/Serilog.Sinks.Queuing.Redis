using FreeRedis;

namespace Serilog.Sinks.Queuing.Redis.Extensions;

internal static class RedisExtension
{
    public static IEnumerable<LogData> ToStreamData(this StreamsEntryResult entry)
    {
        return entry.entries.Select(t => t.ToStreamData());
    }

    public static LogData ToStreamData(this StreamsEntry entry)
    {
        var streamData = new LogData
                         {
                             Id = entry.id
                         };

        for (var i = 0; i < entry.fieldValues.Length; i+=2)
        {
            var key = entry.fieldValues[i];
            var value = entry.fieldValues[i + 1];

            switch (key)
            {
                case nameof(LogData.Timestamp):
                    if (!long.TryParse(value.ToString(), out var unixTimeMilliseconds)) break;
                    streamData.Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);

                    break;
                case nameof(LogData.Data):
                    streamData.Data = (string)value;
                    break;
            }
            
        }

        return streamData;
    }
    
    public static async Task RegisterConsumerAsync(this RedisClient redis, RedisQueuingSinkOptions options)
    {
        if (!await redis.ExistsAsync(options.StreamKey) || (await redis.XInfoGroupsAsync(options.StreamKey)).All(x => x.name != options.StreamGroup))
            await redis.XGroupCreateAsync(options.StreamKey, options.StreamGroup, options.GroupNewestId ? "$" : "0", true);

        var consumers = await redis.XInfoConsumersAsync(options.StreamKey, options.StreamGroup);

        if (consumers.All(x => x.name != options.MachineName))
            await redis.XGroupCreateConsumerAsync(options.StreamKey, options.StreamGroup, options.MachineName);
    }
}