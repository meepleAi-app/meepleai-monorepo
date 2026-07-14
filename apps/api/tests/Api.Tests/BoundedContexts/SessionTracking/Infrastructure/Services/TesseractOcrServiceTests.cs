using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Integration test for <see cref="TesseractOcrService"/> (#2936).
///
/// <para>Gated at runtime on <c>GAMEBOOK_TESSDATA_DIR</c>: the test runs only when a
/// directory containing <c>eng.traineddata</c> is available — locally, or in a CI shard
/// that provisions the Tesseract language data. When the data is absent the test performs
/// an OBSERVABLE <see cref="Assert.Skip(string)"/> (never a silent no-op pass) so a green
/// run never masks an unexecuted OCR path.</para>
///
/// <para>Setup (local + CI provisioning steps):
/// <c>docs/for-developers/testing/backend/tesseract-ocr-setup.md</c>.</para>
/// </summary>
[Trait("Category", "Integration")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class TesseractOcrServiceTests
{
    [Fact]
    public async Task Extract_FromSyntheticPng_ReturnsParagraphsAndConfidence()
    {
        var tessdataDir = Environment.GetEnvironmentVariable("GAMEBOOK_TESSDATA_DIR");
        if (string.IsNullOrWhiteSpace(tessdataDir) ||
            !File.Exists(Path.Combine(tessdataDir, "eng.traineddata")))
        {
            Assert.Skip(
                "GAMEBOOK_TESSDATA_DIR is unset or eng.traineddata is missing — see " +
                "docs/for-developers/testing/backend/tesseract-ocr-setup.md to enable this test.");
            return;
        }

        // Arrange: render a synthetic storybook paragraph to a high-contrast PNG.
        var png = RenderTextPng("§47 The cave is dark.");
        using var service = new TesseractOcrService(NullLogger<TesseractOcrService>.Instance);
        using var stream = new MemoryStream(png);

        // Act
        var result = await service.ExtractAsync(stream, TestContext.Current.CancellationToken);

        // Assert: the §47 header is parsed into a numbered paragraph, the body text survives
        // OCR, and the mean confidence clears the noise floor for clean synthetic text.
        result.Paragraphs.Should().NotBeEmpty();
        result.Paragraphs[0].Number.Should().Be(47,
            "the '§47' header must be segmented into paragraph number 47");
        result.FullText.Should().ContainEquivalentOf("cave",
            "the rendered body text must survive the OCR round-trip");
        result.AverageConfidence.Should().BeGreaterThan(30d,
            "clean high-contrast synthetic text should OCR well above the noise floor (0-100 scale)");
    }

    private static byte[] RenderTextPng(string text)
    {
        const int width = 640;
        const int height = 160;

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, size: 48f);
        canvas.DrawText(text, x: 20f, y: 100f, SKTextAlign.Left, font, paint);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
