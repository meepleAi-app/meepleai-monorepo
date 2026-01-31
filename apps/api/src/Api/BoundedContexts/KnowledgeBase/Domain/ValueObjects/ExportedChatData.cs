using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value Object representing exported chat thread data.
/// Encapsulates the format and serialized content of an exported chat.
/// </summary>
internal sealed class ExportedChatData : ValueObject
{
    public ExportFormat Format { get; }
    public string Content { get; }
    public string ContentType { get; }
    public string FileExtension { get; }
    public DateTime ExportedAt { get; }

    public ExportedChatData(ExportFormat format, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Export content cannot be empty", nameof(content));

        Format = format;
        Content = content;
        ExportedAt = DateTime.UtcNow;

        // Set content type and file extension based on format
        (ContentType, FileExtension) = format switch
        {
            ExportFormat.Json => ("application/json", "json"),
            ExportFormat.Markdown => ("text/markdown", "md"),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Invalid export format")
        };
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Format;
        yield return Content;
        yield return ExportedAt;
    }

    public override string ToString() => $"[{Format}] {Content.Substring(0, Math.Min(100, Content.Length))}...";
}
