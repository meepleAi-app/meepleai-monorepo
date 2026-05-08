using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Integration tests for <see cref="Api.BoundedContexts.SessionTracking.Infrastructure.Services.TesseractOcrService"/>.
/// Skipped in CI: requires eng.traineddata bundled at GAMEBOOK_TESSDATA_DIR.
/// Aaron validates locally against synthetic PNG fixtures during Iter 1.B integration runs.
/// </summary>
[Trait("Category", "Integration")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class TesseractOcrServiceTests
{
    [Fact(Skip = "Requires Tesseract eng.traineddata bundled at GAMEBOOK_TESSDATA_DIR — Aaron-validated locally with synthetic PNG fixture in Iter 1.B integration runs.")]
    public void Extract_FromSyntheticPng_ReturnsParagraphsAndConfidence()
    {
        // Manual run pattern:
        //   1. Generate PNG with rendered text "§47 The cave is dark." via SkiaSharp
        //   2. Construct TesseractOcrService with tessdata dir via GAMEBOOK_TESSDATA_DIR env var
        //   3. Assert paragraphs[0].Number == 47, Text contains "cave", AverageConfidence > 60
    }
}
