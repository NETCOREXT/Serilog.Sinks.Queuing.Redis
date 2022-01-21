using System.Text.Json.Serialization;

namespace Serilog.Sinks.Queuing.Redis.ElasticHook;

public class ResponseResult
{
    [JsonPropertyName("items")]
    public ResponseIndex[] Items { get; set; }
}

public class ResponseIndex
{
    [JsonPropertyName("index")]
    public ResponseItem Index { get; set; }
}

public class ResponseItem
{
    [JsonPropertyName("_id")]
    public string Id { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("error")]
    public ErrorItem Error { get; set; }
}

public class ErrorItem
{
    [JsonPropertyName("reason")]
    public string Reason { get; set; }
}