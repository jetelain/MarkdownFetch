# MarkdownFetch (`mdfetch`)

A fast, lightweight command-line tool to download any web resource (HTML, PDF, Word, Excel, etc.) and convert it into clean Markdown using [markitdown-api](https://github.com/dezoito/markitdown-api).

Built with **.NET 10**, **Clean Architecture**, **System.CommandLine 2.0**, and **Native AOT** support for instant startup.

---

## Prerequisites

`mdfetch` communicates with a `markitdown-api` instance to perform markdown conversions.

You can run the API locally using Docker:

```bash
docker run -d --name markitdown-api -p 8490:8490 ghcr.io/dezoito/markitdown-api:latest
```

---

## Installation

### As a Global .NET Tool

Once published to NuGet:

```bash
dotnet tool install -g Pmad.MarkdownFetch
```

To install directly from local source:

```powershell
.\install-cli.ps1
```

Or pack and install manually:

```bash
dotnet pack src/MarkdownFetch/MarkdownFetch.csproj -c Release -o ./nupkg
dotnet tool install -g Pmad.MarkdownFetch --add-source ./nupkg
```

### Native AOT Binary

You can also publish a standalone native executable with no .NET runtime dependency:

```bash
dotnet publish src/MarkdownFetch/MarkdownFetch.csproj -c Release -r win-x64
# Or for Linux: -r linux-x64
# Or for macOS: -r osx-arm64
```

---

## Usage

```text
mdfetch <url> [<output file>] [options]
```

### Examples

**Print markdown directly to standard output (stdout):**

```bash
mdfetch https://example.com
```

**Save markdown to a file (positional argument):**

```bash
mdfetch https://example.com result.md
```

**Save markdown to a file using the `-o` / `--output` option:**

```bash
mdfetch https://example.com -o result.md
```

**Convert a PDF document from a URL:**

```bash
mdfetch https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf -o doc.md
```

**Specify a custom Markitdown API server:**

```bash
mdfetch https://example.com --api-url http://192.168.1.100:8490
```

**Set a custom network timeout:**

```bash
mdfetch https://example.com -t 60
```

---

## CLI Reference

### Arguments

| Argument | Description |
|---|---|
| `<url>` | The URL of the web resource to fetch (required). |
| `[<output file>]` | The destination path of the output markdown file. If omitted, markdown is printed to `stdout`. |

### Options

| Option | Alias | Description | Default |
|---|---|---|---|
| `--output <path>` | `-o` | Output file path (overrides positional argument if both provided). | `null` (stdout) |
| `--api-url <url>` | `-a` | The markitdown API server URL. | `MARKITDOWN_API_URL` env var or `http://127.0.0.1:8490` |
| `--timeout <sec>` | `-t` | Network and processing timeout in seconds. | `30` |
| `--help` | `-h`, `-?` | Show help and usage information. | |
| `--version` | | Show version information. | |

---

## Configuration & Environment Variables

- `MARKITDOWN_API_URL`: Overrides the default API address without passing `--api-url` every time:
  ```bash
  export MARKITDOWN_API_URL="http://localhost:8490"
  ```

---

## Architecture

The project follows **Clean Architecture** principles to decouple business logic from external dependencies and CLI frameworks:

```
src/MarkdownFetch/
├── Core/                                   # Domain & Application Layer
│   ├── Models/                             # DownloadedResource, ConversionResult, MarkdownFetchOptions
│   ├── Ports/                              # IResourceDownloader, IMarkitdownClient, IOutputWriter, IConsoleView
│   └── UseCases/                           # FetchMarkdownUseCase, IFetchMarkdownUseCase
├── Infrastructure/                         # Adapters & Implementations
│   ├── Common/                             # FileNameResolver (Content-Disposition, MIME detection, URL fallbacks)
│   ├── Http/                               # HttpResourceDownloader, MarkitdownApiClient (TCP health checks & HTTP upload)
│   └── IO/                                 # OutputWriter (File/Stdout), ConsoleView
├── Presentation/                           # CLI Layer
│   └── CommandLine/                        # System.CommandLine 2.0 configuration & binding
└── Program.cs                              # Composition Root
```

### Running Tests

Run the test suite with `dotnet test`:

```bash
dotnet test
```

---

## License

This project is licensed under the [MIT License](LICENSE).