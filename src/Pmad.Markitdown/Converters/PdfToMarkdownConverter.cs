using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.Advanced;
using PdfSharpCore.Pdf.Content;
using PdfSharpCore.Pdf.Content.Objects;
using PdfSharpCore.Pdf.IO;

namespace Pmad.Markitdown.Converters;

/// <summary>
/// Converts PDF documents to clean Markdown using PdfSharpCore with ToUnicode CMap support.
/// </summary>
public sealed class PdfToMarkdownConverter : IFormatConverter
{
    public bool CanHandle(string? extension, string? mediaType)
    {
        if (!string.IsNullOrEmpty(extension))
        {
            var ext = extension.TrimStart('.').ToLowerInvariant();
            if (ext is "pdf") return true;
        }

        if (!string.IsNullOrEmpty(mediaType))
        {
            var mime = mediaType.Split(';')[0].Trim().ToLowerInvariant();
            if (mime is "application/pdf" or "application/x-pdf") return true;
        }

        return false;
    }

    public async Task<string> ConvertAsync(Stream stream, MarkitdownOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        MemoryStream? memoryStream = null;
        Stream effectiveStream = stream;

        try
        {
            if (!effectiveStream.CanSeek)
            {
                memoryStream = new MemoryStream();
                await effectiveStream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;
                effectiveStream = memoryStream;
            }
            else if (effectiveStream.Position != 0)
            {
                effectiveStream.Position = 0;
            }

            using var document = PdfReader.Open(effectiveStream, PdfDocumentOpenMode.ReadOnly);
            var sb = new StringBuilder();

            for (var i = 0; i < document.PageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = document.Pages[i];
                var pageText = ExtractTextFromPage(page);

                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    if (sb.Length > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine();
                    }
                    sb.Append(pageText.Trim());
                }
            }

            return sb.ToString().Trim();
        }
        finally
        {
            memoryStream?.Dispose();
        }
    }

    private static string ExtractTextFromPage(PdfPage page)
    {
        var fontMaps = BuildFontToUnicodeMaps(page);
        var content = ContentReader.ReadContent(page);
        var sb = new StringBuilder();
        var state = new TextExtractionState();

        ExtractTextFromSequence(content, sb, fontMaps, state);
        return NormalizeExtractedText(sb.ToString());
    }

    private sealed class TextExtractionState
    {
        public string? CurrentFontName { get; set; }
    }

    private static void ExtractTextFromSequence(
        CSequence sequence,
        StringBuilder sb,
        Dictionary<string, Dictionary<int, string>> fontMaps,
        TextExtractionState state)
    {
        foreach (var element in sequence)
        {
            if (element is COperator op)
            {
                switch (op.OpCode.Name)
                {
                    case "Tf":
                        if (op.Operands.Count > 0)
                        {
                            state.CurrentFontName = op.Operands[0].ToString();
                        }
                        break;

                    case "Tj":
                        if (op.Operands.Count > 0 && op.Operands[0] is CString cStr)
                        {
                            sb.Append(DecodeString(cStr.Value, state.CurrentFontName, fontMaps));
                        }
                        break;

                    case "'":
                        sb.AppendLine();
                        if (op.Operands.Count > 0 && op.Operands[0] is CString str)
                        {
                            sb.Append(DecodeString(str.Value, state.CurrentFontName, fontMaps));
                        }
                        break;

                    case "\"":
                        sb.AppendLine();
                        if (op.Operands.Count > 2 && op.Operands[2] is CString quoteStr)
                        {
                            sb.Append(DecodeString(quoteStr.Value, state.CurrentFontName, fontMaps));
                        }
                        break;

                    case "TJ":
                        if (op.Operands.Count > 0 && op.Operands[0] is CSequence tjSeq)
                        {
                            for (var idx = 0; idx < tjSeq.Count; idx++)
                            {
                                var item = tjSeq[idx];
                                if (item is CString tjStr)
                                {
                                    sb.Append(DecodeString(tjStr.Value, state.CurrentFontName, fontMaps));
                                }
                                else if (item is CInteger ci && ci.Value < -250)
                                {
                                    if (sb.Length > 0 && !char.IsWhiteSpace(sb[sb.Length - 1]))
                                    {
                                        sb.Append(' ');
                                    }
                                }
                                else if (item is CReal cr && cr.Value < -250.0)
                                {
                                    if (sb.Length > 0 && !char.IsWhiteSpace(sb[sb.Length - 1]))
                                    {
                                        sb.Append(' ');
                                    }
                                }
                            }
                        }
                        break;

                    case "T*":
                        sb.AppendLine();
                        break;

                    case "Td":
                    case "TD":
                        if (op.Operands.Count >= 2)
                        {
                            double dy = 0;
                            if (op.Operands[1] is CReal cr) dy = cr.Value;
                            else if (op.Operands[1] is CInteger ci) dy = ci.Value;

                            if (Math.Abs(dy) > 1.0)
                            {
                                sb.AppendLine();
                            }
                        }
                        break;

                    case "ET":
                        break;
                }
            }
            else if (element is CSequence nested)
            {
                ExtractTextFromSequence(nested, sb, fontMaps, state);
            }
        }
    }

    private static string DecodeString(string raw, string? fontName, Dictionary<string, Dictionary<int, string>> fontMaps)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        if (!string.IsNullOrEmpty(fontName))
        {
            var key = fontName.TrimStart('/');
            if (fontMaps.TryGetValue(key, out var map) || fontMaps.TryGetValue(fontName, out map))
            {
                var sb = new StringBuilder();
                var bytes = Encoding.Latin1.GetBytes(raw);

                // Check if map uses multi-byte keys
                bool multiByte = map.Keys.Any(k => k > 255);

                if (multiByte && bytes.Length >= 2)
                {
                    for (int i = 0; i + 1 < bytes.Length; i += 2)
                    {
                        int code = (bytes[i] << 8) | bytes[i + 1];
                        if (map.TryGetValue(code, out var mapped))
                        {
                            sb.Append(mapped);
                        }
                    }
                    if (sb.Length > 0) return sb.ToString();
                }
                else
                {
                    foreach (var b in bytes)
                    {
                        if (map.TryGetValue(b, out var mapped))
                        {
                            sb.Append(mapped);
                        }
                        else
                        {
                            sb.Append((char)b);
                        }
                    }
                    if (sb.Length > 0) return sb.ToString();
                }
            }
        }

        return DecodePdfString(raw);
    }

    private static string DecodePdfString(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        // Check for UTF-16BE BOM (\xFE\xFF)
        if (raw.Length >= 2 && raw[0] == '\xFE' && raw[1] == '\xFF')
        {
            var bytes = Encoding.Latin1.GetBytes(raw.Substring(2));
            return Encoding.BigEndianUnicode.GetString(bytes);
        }

        // Check for two-byte encoding with alternating nulls
        if (raw.Length >= 2 && raw.Contains('\0'))
        {
            var bytes = Encoding.Latin1.GetBytes(raw);
            if (bytes.Length % 2 == 0)
            {
                try
                {
                    var decoded = Encoding.BigEndianUnicode.GetString(bytes);
                    if (decoded.Any(c => !char.IsControl(c)))
                    {
                        return decoded;
                    }
                }
                catch
                {
                    // Fall back to raw
                }
            }
        }

        return raw;
    }

    private static Dictionary<string, Dictionary<int, string>> BuildFontToUnicodeMaps(PdfPage page)
    {
        var fontMaps = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

        var resources = ResolveDictionary(page.Elements["/Resources"]);
        if (resources == null) return fontMaps;

        var fontDict = ResolveDictionary(resources.Elements["/Font"]);
        if (fontDict == null) return fontMaps;

        foreach (var key in fontDict.Elements.Keys)
        {
            var font = ResolveDictionary(fontDict.Elements[key]);
            if (font == null) continue;

            var toUnicodeObj = font.Elements["/ToUnicode"];
            var toUnicodeDict = ResolveDictionary(toUnicodeObj);
            if (toUnicodeDict != null && toUnicodeDict.Stream != null)
            {
                var bytes = toUnicodeDict.Stream.UnfilteredValue;
                if (bytes != null && bytes.Length > 0)
                {
                    var cmapText = Encoding.Latin1.GetString(bytes);
                    var map = ParseCMap(cmapText);
                    if (map.Count > 0)
                    {
                        var cleanKey = key.TrimStart('/');
                        fontMaps[cleanKey] = map;
                        fontMaps[key] = map;
                    }
                }
            }
        }

        return fontMaps;
    }

    private static PdfDictionary? ResolveDictionary(PdfItem? item)
    {
        if (item is PdfReference reference)
        {
            return reference.Value as PdfDictionary;
        }
        return item as PdfDictionary;
    }

    private static Dictionary<int, string> ParseCMap(string cmapText)
    {
        var map = new Dictionary<int, string>();
        var lines = cmapText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        bool inBfChar = false;
        bool inBfRange = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Contains("beginbfchar"))
            {
                inBfChar = true;
                continue;
            }
            if (line.Contains("endbfchar"))
            {
                inBfChar = false;
                continue;
            }
            if (line.Contains("beginbfrange"))
            {
                inBfRange = true;
                continue;
            }
            if (line.Contains("endbfrange"))
            {
                inBfRange = false;
                continue;
            }

            if (inBfChar)
            {
                var matches = Regex.Matches(line, @"<([0-9a-fA-F]+)>");
                if (matches.Count >= 2)
                {
                    var srcHex = matches[0].Groups[1].Value;
                    var dstHex = matches[1].Groups[1].Value;

                    if (int.TryParse(srcHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var srcCode))
                    {
                        map[srcCode] = ParseHexUnicode(dstHex);
                    }
                }
            }
            else if (inBfRange)
            {
                var matches = Regex.Matches(line, @"<([0-9a-fA-F]+)>");
                if (matches.Count >= 3)
                {
                    var startHex = matches[0].Groups[1].Value;
                    var endHex = matches[1].Groups[1].Value;
                    var dstHex = matches[2].Groups[1].Value;

                    if (int.TryParse(startHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var startCode) &&
                        int.TryParse(endHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var endCode) &&
                        int.TryParse(dstHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var dstCode))
                    {
                        for (int code = startCode, target = dstCode; code <= endCode; code++, target++)
                        {
                            map[code] = char.ConvertFromUtf32(target);
                        }
                    }
                }
            }
        }

        return map;
    }

    private static string ParseHexUnicode(string hex)
    {
        var sb = new StringBuilder();
        for (int i = 0; i + 4 <= hex.Length; i += 4)
        {
            var part = hex.Substring(i, 4);
            if (int.TryParse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codePoint))
            {
                sb.Append((char)codePoint);
            }
        }
        if (sb.Length == 0 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var val))
        {
            sb.Append((char)val);
        }
        return sb.ToString();
    }

    private static string NormalizeExtractedText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sb = new StringBuilder();

        foreach (var rawLine in lines)
        {
            var line = Regex.Replace(rawLine.Trim(), @"\s+", " ");
            if (!string.IsNullOrEmpty(line))
            {
                sb.AppendLine(line);
            }
        }

        return sb.ToString().Trim();
    }
}
