using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace FlowForge.LogIndexer;

public sealed class ElasticsearchLogClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly ILogger<ElasticsearchLogClient> _logger;

    public ElasticsearchLogClient(
        IOptions<ElasticsearchOptions> options,
        ILogger<ElasticsearchLogClient> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(options.Value.Uri.TrimEnd('/') + "/")
        };
    }

    public async Task PutIndexTemplateWithRetryAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PutIndexTemplateAsync(ct);
                return;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Elasticsearch is not ready. Retrying index template PUT in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    public async Task BulkIndexAsync(IReadOnlyList<BufferedLogMessage> batch, CancellationToken ct)
    {
        var body = BuildBulkBody(batch);
        using var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-ndjson");

        using var response = await _httpClient.PostAsync("_bulk", content, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Elasticsearch bulk request failed with HTTP {(int)response.StatusCode}: {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        if (document.RootElement.GetProperty("errors").GetBoolean())
        {
            throw new InvalidOperationException($"Elasticsearch bulk request contained item errors: {responseBody}");
        }
    }

    private async Task PutIndexTemplateAsync(CancellationToken ct)
    {
        var template = new
        {
            index_patterns = new[] { "flowforge-logs-*" },
            template = new
            {
                mappings = new
                {
                    properties = new Dictionary<string, object>
                    {
                        ["runId"] = new { type = "keyword" },
                        ["jobName"] = new { type = "keyword" },
                        ["stepNo"] = new { type = "integer" },
                        ["stepType"] = new { type = "keyword" },
                        ["level"] = new { type = "keyword" },
                        ["workerId"] = new { type = "keyword" },
                        ["message"] = new { type = "text" },
                        ["error"] = new { type = "text" },
                        ["attempt"] = new { type = "integer" },
                        ["durationMs"] = new { type = "long" },
                        ["timestamp"] = new { type = "date" }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(template, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PutAsync("_index_template/flowforge-logs-template", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Elasticsearch index template PUT failed with HTTP {(int)response.StatusCode}: {responseBody}");
        }

        _logger.LogInformation("Elasticsearch index template for flowforge logs is ready.");
    }

    private static string BuildBulkBody(IReadOnlyList<BufferedLogMessage> batch)
    {
        var builder = new StringBuilder();
        foreach (var message in batch)
        {
            var metadata = JsonSerializer.Serialize(
                new
                {
                    index = new
                    {
                        _index = message.IndexName,
                        _id = message.DocumentId
                    }
                },
                JsonOptions);

            builder.AppendLine(metadata);
            builder.AppendLine(message.Json);
        }

        return builder.ToString();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
