---
name: markdown-fetch
description: >-
  Use this skill when fetching, downloading, or converting any web page, remote document (PDF, Word, Excel, PowerPoint, HTML), or online resource into clean Markdown using the mdfetch CLI tool.
---

# MarkdownFetch (`mdfetch`) Skill

This skill guides AI agents on how to fetch remote web resources and convert them into clean, structured Markdown using `mdfetch` backed by `markitdown-api`.

---

## When to Use This Skill

- Fetching and reading web documentation or articles in clean markdown format rather than raw HTML.
- Converting remote PDF manuals, reports, or specifications into text-readable markdown.
- Extracting text and tables from remote Office documents (`.docx`, `.xlsx`, `.pptx`).
- Downloading reference resources into workspace markdown files for project context or knowledge bases.

---

## Prerequisites & Verification

`mdfetch` requires a running `markitdown-api` instance (defaulting to `http://127.0.0.1:8490`).

### 1. Verify Markitdown API Status

Before calling `mdfetch`, verify if the API is reachable or check docker containers:

```bash
docker ps --filter "name=markitdown-api"
```

If it is not running, start it using:

```bash
docker run -d --name markitdown-api -p 8490:8490 ghcr.io/dezoito/markitdown-api:latest
```

### 2. Verify `mdfetch` Availability

Check if `mdfetch` is installed as a global .NET tool:

```bash
mdfetch --version
```

If `mdfetch` is not installed globally, you can invoke it directly from the repository source:

```bash
dotnet run --project src/MarkdownFetch/MarkdownFetch.csproj -- <arguments>
```

Or install it globally from source:

```powershell
.\install-cli.ps1
```

---

## Command Syntax & Common Workflows

### 1. Fetch to Standard Output (Inspect content directly)

To read the converted markdown directly in the console / subagent output:

```bash
mdfetch "https://example.com/article"
```

### 2. Save Converted Content to a File

Save markdown directly to a workspace file for indexing, reviewing, or persistent reference:

```bash
mdfetch "https://example.com/spec.html" -o "docs/spec.md"
# Or using positional argument:
mdfetch "https://example.com/spec.html" "docs/spec.md"
```

### 3. Convert Remote PDFs or Documents

`mdfetch` automatically detects MIME types and Content-Disposition headers to properly handle binary document formats:

```bash
mdfetch "https://example.com/files/whitepaper.pdf" -o "docs/whitepaper.md"
mdfetch "https://example.com/files/sheet.xlsx" -o "docs/sheet.md"
```

### 4. Custom Server or Custom Timeout

For large documents or custom remote API server deployments:

```bash
mdfetch "https://example.com/large-manual.pdf" -o "docs/manual.md" --timeout 120 --api-url "http://127.0.0.1:8490"
```

Environment variable alternative:
```bash
export MARKITDOWN_API_URL="http://127.0.0.1:8490"
```

---

## CLI Options Reference

| Argument / Option | Short | Description | Default |
|---|---|---|---|
| `<url>` | | The URL of the web resource to fetch (required). | |
| `[<output file>]` | | Optional destination file path. If omitted, outputs to stdout. | stdout |
| `--output <path>` | `-o` | Destination file path (overrides positional argument). | stdout |
| `--api-url <url>` | `-a` | URL of the markitdown API server. | `http://127.0.0.1:8490` |
| `--timeout <sec>` | `-t` | Network and conversion timeout in seconds. | `30` |
| `--help` | `-h`, `-?` | Display usage and help message. | |

---

## Error Codes and Handling

| Exit Code | Cause | Recommended Agent Action |
|---|---|---|
| `0` | Success | Proceed with the extracted markdown content. |
| `1` | API not reachable | Start the Docker container: `docker run -d --name markitdown-api -p 8490:8490 ghcr.io/dezoito/markitdown-api:latest`. |
| `1` | Download error (404/403/timeout) | Verify the remote URL is valid and accessible over the network. |
| `1` | File type not supported (422) | The file format cannot be parsed by markitdown. Fallback to raw download or curl. |
| `1` | Invalid CLI arguments | Ensure the URL includes the scheme (e.g. `https://...`). |
