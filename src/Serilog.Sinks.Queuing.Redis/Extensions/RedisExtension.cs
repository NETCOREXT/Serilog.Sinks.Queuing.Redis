using FreeRedis;

namespace Serilog.Sinks.Queuing.Redis.Extensions;

public static class RedisExtension
{
    public static IEnumerable<LogData> ToLogData(this StreamsEntryResult entry)
    {
        return entry.entries.Select(t => t.ToLogData());
    }

    public static LogData ToLogData(this StreamsEntry entry)
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
}