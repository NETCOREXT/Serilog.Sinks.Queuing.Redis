using System.Text;
using System.Text.Json;

namespace Serilog.Sinks.Queuing.Redis.ElasticHook;

public class ElasticRedisStreamHook : IRedisStreamHook
{
    private readonly ElasticHookOptions _options;
    private readonly HttpClient _httpClient;

    public ElasticRedisStreamHook(ElasticHookOptions options)
    {
        _options = options;

        _httpClient = new HttpClient
                      {
                          BaseAddress = new Uri(_options.Connection)
                      };

        CreateTemplateAsync().GetAwaiter()
                             .GetResult();
    }

    public async Task InvokeAsync(IEnumerable<LogData> logs, CancellationToken cancellationToken = default)
    {
        var lsTaskResult = new List<HttpResponseMessage>();
        var lsPostData = new StringBuilder();

        foreach (var log in logs)
        {
            var indexFormatPattern = log.Timestamp.ToString(_options.IndexFormatPattern);
            var fullIndex = (_options.Index + indexFormatPattern).ToLower();

            lsPostData.AppendLine("{\"index\":{\"_index\":\"" + fullIndex + "\",\"_id\":\"" + log.Id + "\"}}");
            lsPostData.AppendLine(log.Data);
        }

        if (lsPostData.Length > 0)
        {
            var postData = lsPostData.ToString();
            var result = await _httpClient.PostAsync("_bulk", new StringContent(postData, Encoding.UTF8, "application/json"), cancellationToken);

            lsTaskResult.Add(result);
        }

        foreach (var response in lsTaskResult)
        {
            try
            {
                response.EnsureSuccessStatusCode();

                var raw = await response.Content.ReadAsByteArrayAsync();

                var rp = JsonSerializer.Deserialize<ResponseResult>(raw);

                if (rp == null) continue;

                foreach (var item in rp.Items)
                {
                    if (item.Index.Status == 200 || item.Index.Status == 201) continue;

                    Console.Error.WriteLine("{0} fatal error: {1}", item.Index.Id, item.Index.Error.Reason);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("{0} fatal error: {1}", $"{typeof(ElasticRedisStreamHook)}.{nameof(InvokeAsync)}", e);
            }
        }
    }

    private async Task CreateTemplateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_options.TemplateName) || string.IsNullOrWhiteSpace(_options.TemplatePattern))
                return;

            var result = await _httpClient.GetAsync("_index_template/" + _options.TemplateName.ToLower(), HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (result.IsSuccessStatusCode) return;

            result = await _httpClient.PutAsync("_index_template/" + _options.TemplateName.ToLower(), new StringContent(_options.TemplatePattern, Encoding.UTF8, "application/json"), cancellationToken);

            result.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("{0} fatal error: {1}", $"{nameof(ElasticRedisStreamHook)}.{nameof(CreateTemplateAsync)}", e);
        }
    }
}