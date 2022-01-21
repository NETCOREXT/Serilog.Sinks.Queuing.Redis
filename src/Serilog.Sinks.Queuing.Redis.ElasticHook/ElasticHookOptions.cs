namespace Serilog.Sinks.Queuing.Redis.ElasticHook;

public class ElasticHookOptions
{
    public string Connection { get; set; }
    public string Index { get; set; } = "log-";
    public string IndexFormatPattern { get; set; } = "yyyyMMdd";
    public string TemplateName { get; set; } = "serilog";
    public string TemplatePattern { get; set; } = "{\"index_patterns\":[\"serilog-*\"],\"template\":{\"mappings\":{\"properties\":{\"_metadata\":{\"properties\":{\"interval\":{\"type\":\"text\"}}}}}}}";
}