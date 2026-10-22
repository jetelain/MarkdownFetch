# Pmad.Markitdown

A lightweight, pure managed .NET library to convert documents (**HTML**, **PDF**, **DOCX**, and plain text) into clean Markdown with **zero external or Docker dependencies**.

Targeting **.NET 10** and **.NET 8**.

---

## Features

- **Pure .NET (Managed)**: Runs fully in-process. No Python, no Docker, no Node.js required.
- **HTML to Markdown**:
  - Full DOM parsing and sanitization powered by [AngleSharp](https://github.com/AngleSharp/AngleSharp).
  - High-fidelity Markdown generation powered by [ReverseMarkdown](https://github.com/mysticmind/reversemarkdown-net).
  - Automatically extracts `<title>` as a top-level `# ` heading if not already present.
  - Strips noisy boilerplate elements (`<script>`, `<style>`, `<nav>`, `<svg>`, `<footer>`).
- **PDF to Markdown**:
  - Extracted via [PdfSharpCore](https://github.com/ststeiger/PdfSharpCore).
  - Low-level content stream parser handling font CMaps, subsetted fonts (`/ToUnicode`), and accurate character/line positioning.
- **Word (DOCX) to Markdown**:
  - Semantic document extraction via [Mammoth](https://github.com/mwilliamson/mammoth-dotnet) piped into clean Markdown.
- **Plain Text / Code / JSON**:
  - Line ending and encoding normalization.
- **Extensible Architecture**:
  - Implements a pluggable converter strategy (`IFormatConverter`) allowing custom formats to be easily registered.

---

## Installation

Install via NuGet:

```bash
dotnet add package Pmad.Markitdown
```

Or via Package Manager Console:

```powershell
Install-Package Pmad.Markitdown
```

---

## Quick Start

### 1. Basic Conversion

```csharp
using Pmad.Markitdown;

IMarkitdown markitdown = new Markitdown();

// Convert a local file (.html, .pdf, .docx, .txt, etc.)
string markdown = await markitdown.ConvertFileAsync("document.pdf");
Console.WriteLine(markdown);
```

### 2. Convert from a Stream

Pass a `Stream` alongside a file extension or MIME media type:

```csharp
using var stream = File.OpenRead("report.docx");

// By extension
string mdFromDocx = await markitdown.ConvertAsync(stream, ".docx");

// Or by MIME type
string mdFromPdf = await markitdown.ConvertAsync(pdfStream, "application/pdf");
```

### 3. Convert Raw HTML or Text

```csharp
string html = "<html><head><title>My Article</title></head><body><p>Hello <strong>World</strong>!</p></body></html>";

string markdown = await markitdown.ConvertTextAsync(html, "text/html");
// Output:
// # My Article
//
// Hello **World**!
```

---

## Configuration Options

Customize conversion behavior using `MarkitdownOptions`:

```csharp
var options = new MarkitdownOptions
{
    // Extract document title as an H1 heading if missing from the body (default: true)
    IncludeTitle = true,

    // Strip scripts, styles, navigation, and footer tags from HTML (default: true)
    CleanHtml = true
};

string markdown = await markitdown.ConvertFileAsync("article.html", options);
```

You can also pass default options directly to the `Markitdown` constructor:

```csharp
var markitdown = new Markitdown(new MarkitdownOptions
{
    IncludeTitle = false
});
```

---

## Dependency Injection

Register `Pmad.Markitdown` in your `IServiceCollection`:

```csharp
// Program.cs
builder.Services.AddSingleton<IMarkitdown, Markitdown>();
```

Inject and use in your services:

```csharp
public class DocumentProcessor
{
    private readonly IMarkitdown _markitdown;

    public DocumentProcessor(IMarkitdown markitdown)
    {
        _markitdown = markitdown;
    }

    public async Task ProcessAsync(Stream stream, string contentType)
    {
        string markdown = await _markitdown.ConvertAsync(stream, contentType);
        // ...
    }
}
```

---

## Adding Custom Format Converters

You can extend `Pmad.Markitdown` with your own custom converters by implementing `IFormatConverter`:

```csharp
using Pmad.Markitdown.Converters;

public class CustomCsvConverter : IFormatConverter
{
    public bool CanHandle(string? extension, string? mediaType) =>
        string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mediaType, "text/csv", StringComparison.OrdinalIgnoreCase);

    public async Task<string> ConvertAsync(Stream stream, MarkitdownOptions options, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        var csv = await reader.ReadToEndAsync(cancellationToken);
        // Convert CSV to Markdown table...
        return "| Header 1 | Header 2 |\n| --- | --- |\n";
    }
}

// Register custom converters:
var converters = new IFormatConverter[]
{
    new CustomCsvConverter(),
    new HtmlToMarkdownConverter(),
    new PdfToMarkdownConverter()
};

var markitdown = new Markitdown(converters);
```

---

## Supported Formats

| Format | Extensions | MIME Types | Engine |
|---|---|---|---|
| **HTML** | `.html`, `.htm`, `.xhtml` | `text/html`, `application/xhtml+xml` | AngleSharp + ReverseMarkdown |
| **PDF** | `.pdf` | `application/pdf` | PdfSharpCore |
| **Word** | `.docx` | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` | Mammoth |
| **Plain Text** | `.txt`, `.text` | `text/plain` | Managed |
| **Markdown** | `.md`, `.markdown` | `text/markdown` | Managed |
| **JSON** | `.json` | `application/json` | Managed |

---

## License

This project is licensed under the [MIT License](https://opensource.org/licenses/MIT).
