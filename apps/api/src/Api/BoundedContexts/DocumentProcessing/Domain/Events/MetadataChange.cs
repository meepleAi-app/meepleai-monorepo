namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Single-field diff carried by <see cref="PdfMetadataChangedEvent"/> for audit and
/// cache-invalidation consumers. Issue #1687 D-9.
/// </summary>
/// <param name="Field">Field name (camelCase: title, documentType, language, tags).</param>
/// <param name="OldValue">Pre-edit value rendered as a string (null when previously unset).</param>
/// <param name="NewValue">Post-edit value rendered as a string (null when explicitly cleared).</param>
public sealed record MetadataChange(string Field, string? OldValue, string? NewValue);
