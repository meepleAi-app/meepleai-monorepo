using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Tesseract;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

internal sealed partial class TesseractOcrService : IOcrService, IDisposable
{
    // §47 / "paragraph 47" prefix patterns for storybook paragraph numbering.
    // GeneratedRegex satisfies MA0009 (source-generated, timeout, no backtracking path).
    // ExplicitCapture + named group satisfies MA0023.
    [GeneratedRegex(
        @"^\s*(?:§|paragraph )?(?<n>\d{1,4})[\.\):\- ]",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex ParagraphHeaderRegex();

    private readonly ILogger<TesseractOcrService> _logger;
    private readonly TesseractEngine _engine;

    public TesseractOcrService(ILogger<TesseractOcrService> logger)
    {
        _logger = logger;
        var tessdata = Environment.GetEnvironmentVariable("GAMEBOOK_TESSDATA_DIR") ?? "tessdata";
        _engine = new TesseractEngine(tessdata, "eng", EngineMode.Default);
    }

    public async Task<OcrResult> ExtractAsync(Stream imageStream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        var bytes = await ReadAllAsync(imageStream, cancellationToken).ConfigureAwait(false);
        return await Task.Run(() => Run(bytes), cancellationToken).ConfigureAwait(false);
    }

    private OcrResult Run(byte[] imageBytes)
    {
        using var img = Pix.LoadFromMemory(imageBytes);
        using var page = _engine.Process(img);
        var fullText = page.GetText().Trim();
        var confidence = page.GetMeanConfidence();

        _logger.LogDebug("OCR completed: {CharCount} chars, mean confidence {Confidence:F1}", fullText.Length, confidence);

        var paragraphs = SegmentByParagraphHeader(fullText);
        if (paragraphs.Count == 0)
        {
            paragraphs = new List<OcrParagraph> { new(0, fullText, null) };
        }
        return new OcrResult(fullText, paragraphs, confidence);
    }

    private static List<OcrParagraph> SegmentByParagraphHeader(string text)
    {
        var lines = text.Split('\n');
        var paragraphs = new List<OcrParagraph>();
        int? currentNumber = null;
        var buffer = new System.Text.StringBuilder();
        var regex = ParagraphHeaderRegex();

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            var match = regex.Match(line);
            if (match.Success && int.TryParse(match.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            {
                if (currentNumber.HasValue)
                    paragraphs.Add(new OcrParagraph(currentNumber.Value, buffer.ToString().Trim(), null));
                buffer.Clear();
                currentNumber = n;
                buffer.AppendLine(line[match.Length..].Trim());
            }
            else
            {
                buffer.AppendLine(line);
            }
        }
        if (currentNumber.HasValue)
            paragraphs.Add(new OcrParagraph(currentNumber.Value, buffer.ToString().Trim(), null));
        return paragraphs;
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream is MemoryStream ms) return ms.ToArray();
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        return copy.ToArray();
    }

    public void Dispose() => _engine.Dispose();
}
