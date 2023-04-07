using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Serilog.Sinks.Queuing.Redis.ElasticHook;

public class ElasticLogStore : ILogStore
{
    private HttpClient _httpClient;
    private readonly ElasticHookOptions _options;
    private readonly ILogger<ElasticLogStore> _logger;

    public ElasticLogStore(ElasticHookOptions options, ILogger<ElasticLogStore> logger)
    {
        var handler = new HttpClientHandler
                      {
                          ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                      };

        _httpClient = new HttpClient(handler, true)
                      {
                          BaseAddress = new Uri(options.ElasticsearchHost)
                      };

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", options.ApiKey);

        _options = options;
        _logger = logger;
    }

    public async Task<string[]> InvokeAsync(IEnumerable<LogData> logs, CancellationToken cancellationToken = default)
    {
        var indexPrefix = _options.Index;
        var indexDateFormat = string.IsNullOrWhiteSpace(_options.IndexFormatPattern) ? string.Empty : DateTimeOffset.UtcNow.ToString(_options.IndexFormatPattern);
        var index = indexPrefix + indexDateFormat;

        var lsTaskResult = new List<HttpResponseMessage>();
        var lsPostData = new StringBuilder();

        foreach (var req in logs)
        {
            lsPostData.AppendLine("{\"create\":{ \"_id\": \"" + index + "-" + req.Id + "\" }}");
            lsPostData.AppendLine(req.Data);
        }

        if (lsPostData.Length > 0)
        {
            var postData = lsPostData.ToString();
            var rep = await _httpClient.PostAsync(index + "/_bulk", new StringContent(postData, Encoding.UTF8, "application/json"), cancellationToken);

            lsTaskResult.Add(rep);
        }

        var result = new List<string>();

        foreach (var response in lsTaskResult)
        {
            try
            {
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var streamIds = GetStreamId(index, content);

                if (streamIds != null && streamIds.Any())
                    result.AddRange(streamIds);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "{Message}", e.Message);
            }
        }

        return result.ToArray();
    }

    private string[]? GetStreamId(string index, string content)
    {
        var regex = new Regex("\"_id\":\"([^\"]+)\"", RegexOptions.IgnoreCase);

        if (!regex.IsMatch(content)) return null;

        var mc = regex.Matches(content);

        return mc.Select(t => t.Groups[1].Value.TrimStart((index + "-").ToCharArray()))
                 .ToArray();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}