using System.Net.Http.Headers;
using MarkdownFetch.Core.Ports;
using MarkdownFetch.Core.UseCases;
using MarkdownFetch.Infrastructure.Http;
using MarkdownFetch.Infrastructure.IO;
using MarkdownFetch.Presentation.CommandLine;

var rootCommand = CommandLineConfiguration.CreateRootCommand(() =>
{
    var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MarkdownFetch", "1.0"));

    IResourceDownloader downloader = new HttpResourceDownloader(httpClient);
    IMarkitdownClient markitdownClient = new MarkitdownApiClient(httpClient);
    IOutputWriter outputWriter = new OutputWriter();
    IConsoleView consoleView = new ConsoleView();

    return new FetchMarkdownUseCase(downloader, markitdownClient, outputWriter, consoleView);
});

return await rootCommand.Parse(args).InvokeAsync();