namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

internal interface IOcrService
{
    Task<OcrResult> ExtractAsync(Stream imageStream, CancellationToken cancellationToken);
}

internal sealed record OcrResult(string FullText, IReadOnlyList<OcrParagraph> Paragraphs, double AverageConfidence);

internal sealed record OcrParagraph(int Number, string Text, BoundingBox? Bbox);

internal sealed record BoundingBox(int X, int Y, int Width, int Height);
