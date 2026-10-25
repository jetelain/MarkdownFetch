# MarkdownFetch (`mdfetch`)

A fast, lightweight command-line tool to download any web resource (HTML, PDF, Word, Excel, CSV, JSON, Text, etc.) and convert it into clean Markdown using **pure managed .NET** (or optionally via [markitdown-api](https://github.com/dezoito/markitdown-api)).

Built with **.NET 10**, **Clean Architecture**, **System.CommandLine 2.0**, and **Native AOT** support for instant startup.

---

## Conversion Engines

`mdfetch` supports two conversion engines:

1. **Native .NET Engine (Default)**:
   * **Zero external dependencies**: No Docker or external service required.
   * Powered in-process by **`Pmad.Markitdown`**, a lightweight, high-performance .NET library dedicated to converting:
     * **HTML**: Robust DOM parsing and sanitization via `AngleSharp` and semantic Markdown conversion with `ReverseMarkdown`.
     * **PDF**: Clean text and structure extraction via `PdfPig`, including support for subsetted fonts, font CMaps, and Unicode mappings.
     * **DOCX**: Semantic Word document conversion via `Mammoth`.
     * **Text / Markdown / JSON**: Automatic normalization of line endings and encoding.
   * Fast, private, and runs directly within the CLI process.

2. **Markitdown API Engine (Optional)**:
   * Enabled via `--use-api`, `--engine api`, or `--api-url <url>`.
   * Delegates conversion to a local or remote `markitdown-api` Docker service:
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
| `--engine <native\|api>` | | Conversion engine to use (`native` or `api`). | `native` |
| `--use-api` | | Use external markitdown-api instead of native .NET (shortcut for `--engine api`). | `false` |
| `--api-url <url>` | `-a` | The markitdown API server URL (automatically selects API engine). | `MARKITDOWN_API_URL` env var or `http://127.0.0.1:8490` |
| `--timeout <sec>` | `-t` | Network and processing timeout in seconds. | `30` |
| `--filter-hidden-text` | | Filter hidden text and elements in HTML (e.g. `display:none`, `visibility:hidden`, `[hidden]`, zero-width spaces). | `true` |
| `--no-filter-hidden-text` | | Disable filtering of hidden HTML elements. | |
| `--detect-prompt-injection` | | Detect adversarial prompt injection patterns in fetched content. | `true` |
| `--no-detect-prompt-injection` | | Disable prompt injection detection. | |
| `--url-whitelist <pattern...>` | | Permitted URL patterns or domains (e.g. `github.com`, `*.github.com`). If not specified, all URLs are allowed unless restricted by safety policy. | none |
| `--safety-config <path>` | | Path to a safety configuration JSON file. | |
| `--help` | `-h`, `-?` | Show help and usage information. | |
| `--version` | | Show version information. | |

---

## Safety Guards & Containered Agent Hardening

`mdfetch` is designed for autonomous AI agents and includes built-in safety guards to protect LLMs against adversarial inputs:

1. **Hidden Text Filtering**: Filters invisible text from HTML (elements with `display:none`, `visibility:hidden`, `opacity:0`, `font-size:0`, `[hidden]`, `[aria-hidden="true"]`, off-screen styling, and zero-width unicode characters). Enabled by default.
2. **Prompt Injection Detection**: Scans downloaded content across all formats (HTML, PDF, DOCX, text) for jailbreaks, instruction overrides, system token injections (`<|im_start|>`, `[INST]`), exfiltration directives, and agent targeting attacks. Enabled by default.
3. **URL Whitelisting**: Restricts downloads (and redirect targets) to specific allowed domains, wildcards (`*.github.com`), or URL prefixes.

### Tamper-Proof Container Configuration

For sandboxed or containered agent environments (e.g. Docker, Kubernetes), administrators can mount a read-only configuration file that the agent cannot tamper with:

* **Linux / Containers**: `/etc/markdownfetch/safety.json` or `/etc/mdfetch/safety.json` (mount as read-only `:ro`)
* **Environment Variable**: `MDFETCH_SAFETY_CONFIG=/path/to/protected/safety.json`
* **Windows**: `%ProgramData%\MarkdownFetch\safety.json`

Example `safety.json`:

```json
{
  "urlWhitelist": [
    "*.github.com",
    "github.com",
    "raw.githubusercontent.com",
    "learn.microsoft.com"
  ],
  "filterHiddenText": true,
  "forceFilterHiddenText": true,
  "detectPromptInjection": true,
  "forceDetectPromptInjection": true,
  "promptInjectionAction": "Block"
}
```

When `forceFilterHiddenText` or `forceDetectPromptInjection` is set to `true`, containered agents **cannot** disable safety guards via CLI flags (`--no-filter-hidden-text`, `--no-detect-prompt-injection`). Likewise, the URL whitelist is strictly enforced against both initial requests and redirect hops.

---

## Configuration & Environment Variables

- `MARKITDOWN_API_URL`: Overrides the default API address without passing `--api-url` every time:
  ```bash
  export MARKITDOWN_API_URL="http://localhost:8490"
  ```
- `MDFETCH_SAFETY_CONFIG`: Specifies the location of the protected safety configuration file:
  ```bash
  export MDFETCH_SAFETY_CONFIG="/etc/mdfetch/safety.json"
  ```

---

## Architecture

The project follows **Clean Architecture** principles to decouple business logic from external dependencies and CLI frameworks:

```
src/
├── Pmad.Markitdown/                        # Core Managed Markdown Conversion Library
│   ├── Converters/                         # Format Converters (Html, Pdf [PdfPig], Docx [Mammoth], PlainText)
│   ├── IMarkitdown.cs                      # Conversion contracts & options
│   └── Markitdown.cs                       # Converter implementation & pipeline
│
└── MarkdownFetch/                          # CLI Application (Clean Architecture)
    ├── Core/                               # Domain & Application Layer
    │   ├── Models/                         # DownloadedResource, ConversionResult, MarkdownFetchOptions, ConversionEngine
    │   ├── Ports/                          # IResourceDownloader, INativeMarkdownConverter, IMarkitdownClient, IOutputWriter, IConsoleView
    │   └── UseCases/                       # FetchMarkdownUseCase, IFetchMarkdownUseCase
    ├── Infrastructure/                     # Adapters & Implementations
    │   ├── Common/                         # FileNameResolver (Content-Disposition, MIME detection, URL fallbacks)
    │   ├── Converters/                     # NativeMarkdownConverter (adapters Pmad.Markitdown)
    │   ├── Http/                           # HttpResourceDownloader, MarkitdownApiClient (TCP health checks & HTTP upload)
    │   └── IO/                             # OutputWriter (File/Stdout), ConsoleView
    ├── Presentation/                       # CLI Layer
    │   └── CommandLine/                    # System.CommandLine 2.0 configuration & binding
    └── Program.cs                          # Composition Root
```

### Running Tests

Run the test suite with `dotnet test`:

```bash
dotnet test
```

---

## License

This project is licensed under the [MIT License](LICENSE).