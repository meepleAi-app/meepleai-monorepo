using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

/// <summary>
/// Value object representing notification type.
/// Defines event types that trigger user notifications.
/// </summary>
internal sealed class NotificationType : ValueObject
{
    public string Value { get; }

    public static readonly NotificationType PdfUploadCompleted = new("pdf_upload_completed");
    public static readonly NotificationType RuleSpecGenerated = new("rule_spec_generated");
    public static readonly NotificationType ProcessingFailed = new("processing_failed");
    public static readonly NotificationType NewComment = new("new_comment");
    public static readonly NotificationType SharedLinkAccessed = new("shared_link_accessed");

    private NotificationType(string value)
    {
        Value = value;
    }

    public bool IsPdfUploadCompleted => string.Equals(Value, PdfUploadCompleted.Value, StringComparison.Ordinal);
    public bool IsRuleSpecGenerated => string.Equals(Value, RuleSpecGenerated.Value, StringComparison.Ordinal);
    public bool IsProcessingFailed => string.Equals(Value, ProcessingFailed.Value, StringComparison.Ordinal);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    /// <summary>
    /// Creates a NotificationType from a string value.
    /// Validates against known types.
    /// </summary>
    public static NotificationType FromString(string value)
    {
        var normalized = value.ToLowerInvariant();
        return normalized switch
        {
            "pdf_upload_completed" => PdfUploadCompleted,
            "rule_spec_generated" => RuleSpecGenerated,
            "processing_failed" => ProcessingFailed,
            "new_comment" => NewComment,
            "shared_link_accessed" => SharedLinkAccessed,
            _ => throw new ArgumentException($"Unknown notification type: {value}", nameof(value))
        };
    }
}
