using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using MarkdownFetch.Core.Models;
using MarkdownFetch.Core.Ports;

namespace MarkdownFetch.Infrastructure.Http;

/// <summary>
/// Client for interacting with markitdown-api server over HTTP and TCP.
/// </summary>
public sealed class MarkitdownApiClient : IMarkitdownClient
{
    public const int DefaultPort = 8490;
    public const string DefaultHost = "127.0.0.1";
    public static string DefaultApiUrl => $"http://{DefaultHost}:{DefaultPort}";

    private readonly HttpClient _httpClient;

    public MarkitdownApiClient(HttpClient? httpClient = null)
    {
        if (httpClient != null)
        {
            _httpClient = httpClient;
        }
        else
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MarkdownFetch", "1.0"));
        }
    }

    public async Task<bool> IsReadyAsync(string apiUrl, CancellationToken cancellationToken = default)
    {
        var host = DefaultHost;
        var port = DefaultPort;
        if (Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri))
        {
            port = uri.Port;
            host = uri.DnsSafeHost;
        }

        using var tcpClient = new TcpClient();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(300));
            await tcpClient.ConnectAsync(host, port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ConversionResult> ConvertAsync(
        string apiUrl,
        DownloadedResource resource,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(apiUrl);
        ArgumentNullException.ThrowIfNull(resource);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        using var multipartContent = new MultipartFormDataContent();
        using var streamContent = new StreamContent(resource.Content);

        if (!string.IsNullOrWhiteSpace(resource.ContentType) &&
            MediaTypeHeaderValue.TryParse(resource.ContentType, out var parsedContentType))
        {
            streamContent.Headers.ContentType = parsedContentType;
        }

        multipartContent.Add(streamContent, name: "file", fileName: resource.FileName);

        var endpoint = $"{apiUrl.TrimEnd('/')}/process_file";
        using var response = await _httpClient.PostAsync(endpoint, multipartContent, cts.Token);

        var statusCode = (int)response.StatusCode;

        if (response.StatusCode == HttpStatusCode.UnprocessableContent)
        {
            return ConversionResult.Failure("File type is not supported. Try curl instead.", statusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            return ConversionResult.Failure($"Markitdown API returned HTTP {statusCode} ({response.ReasonPhrase})", statusCode);
        }

        var jsonResponse = await response.Content.ReadAsStringAsync(cts.Token);
        using var jsonDoc = JsonDocument.Parse(jsonResponse);

        if (jsonDoc.RootElement.TryGetProperty("markdown", out var markdownElement))
        {
            var markdown = markdownElement.GetString() ?? string.Empty;
            return ConversionResult.Success(markdown);
        }

        return ConversionResult.Failure(
            $"Invalid response from markitdown-api. A 'markdown' field was expected. Try curl instead.\n{jsonResponse}",
            statusCode);
    }
}
