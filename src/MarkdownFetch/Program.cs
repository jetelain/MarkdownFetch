using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.Json;
using MimeMapping;

if (args.Length < 1)
{
    Console.WriteLine("Usage   : mdfetch <url> [<output file>]");
    Console.WriteLine();
    Console.WriteLine("Example : mdfetch https://example.com result.md");
    Console.WriteLine("          mdfetch https://example.com");
    return;
}

string url = args[0];
string? outputPath = args.Length > 1 ? args[1] : null;

string apiUrl = (Environment.GetEnvironmentVariable("MARKITDOWN_API_URL") ?? "http://127.0.0.1:8490").TrimEnd('/');

using var client = new HttpClient();
client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MarkdownFetch", "1.0"));
client.Timeout = TimeSpan.FromSeconds(30);
try
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    using var result = await client.GetAsync(apiUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
}
catch
{
    var port = 8490;
    if (Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri))
    {
        port = uri.Port;
    }
    Console.Error.WriteLine($"[ERROR] Markitdown API is not reachable at '{apiUrl}'");
    Console.Error.WriteLine($"Run 'docker run -d --name markitdown-api -p {port}:8490 ghcr.io/dezoito/markitdown-api:latest'");
    return;
}

try
{
    using var webResponse = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
    webResponse.EnsureSuccessStatusCode();

    string? fileName = string.Empty;
    if (webResponse.Content.Headers.ContentDisposition != null)
    {
        fileName = webResponse.Content.Headers.ContentDisposition.FileName?.Trim('"');
    }
    else if (webResponse.Content.Headers.Contains("Content-Disposition"))
    {
        var contentDisposition = webResponse.Content.Headers.GetValues("Content-Disposition")?.ToString();
        if (!string.IsNullOrEmpty(contentDisposition))
        {
            fileName = new ContentDisposition(contentDisposition).FileName;
        }
    }

    using var contentStream = await webResponse.Content.ReadAsStreamAsync();

    using var multipartContent = new MultipartFormDataContent();
    using var streamContent = new StreamContent(contentStream);

    var contentType = webResponse.Content.Headers.ContentType;

    if (string.IsNullOrEmpty(fileName))
    {
        fileName = "file.html";

        if (!string.IsNullOrEmpty(contentType?.MediaType))
        {
            var extensions = MimeUtility.GetExtensions(contentType.MediaType);
            if (extensions?.Length > 0)
            {
                fileName = "file." + extensions[0];
            }
        }
    }

    streamContent.Headers.ContentType = contentType;
    if (!string.IsNullOrEmpty(fileName))
    {
        multipartContent.Add(streamContent, name: "file", fileName: fileName);
    }
    else
    {
        multipartContent.Add(streamContent, name: "file");
    }

    using var apiResponse = await client.PostAsync(apiUrl + "/process_file", multipartContent);

    if (apiResponse.StatusCode == System.Net.HttpStatusCode.UnprocessableContent)
    {
        Console.Error.WriteLine("[ERROR] File type is not supported. Try curl instead.");
    }

    apiResponse.EnsureSuccessStatusCode();

    string jsonResponse = await apiResponse.Content.ReadAsStringAsync();

    using var jsonDoc = JsonDocument.Parse(jsonResponse);
    if (jsonDoc.RootElement.TryGetProperty("markdown", out JsonElement markdownElement))
    {
        string markdown = markdownElement.GetString() ?? string.Empty;
        if (outputPath != null)
        {
            await File.WriteAllTextAsync(outputPath, markdown);
            Console.WriteLine($"[SUCCESS] Saved to: {outputPath}");
        }
        else
        {
            Console.WriteLine(markdown);
        }
    }
    else
    {
        Console.Error.WriteLine("[ERROR] Invalid response from markitdown-api. A 'markdown' field was expected. Try curl instead.");
        Console.Error.WriteLine(jsonResponse);
    }
}
catch (HttpRequestException ex)
{
    if (ex.StatusCode != null)
    {
        Console.Error.WriteLine($"[ERROR] Unable to download '{url}': status code {(int)ex.StatusCode} ({ex.StatusCode})");
    }
    else
    {
        Console.Error.WriteLine($"[ERROR] Unable to download '{url}': {ex.Message}");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[ERROR] {ex.Message}");
}