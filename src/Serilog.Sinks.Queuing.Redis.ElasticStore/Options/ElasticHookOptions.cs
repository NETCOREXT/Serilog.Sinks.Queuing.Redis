namespace Serilog.Sinks.Queuing.Redis.ElasticHook;

public class ElasticHookOptions
{
    public string ElasticsearchHost { get; set; } = null!;
    public string? ApiKey { get; set; }
    public string Index { get; set; } = "serilog-";
    public string IndexFormatPattern { get; set; } = "yyyyMMdd";
}