namespace Api.BoundedContexts.DocumentProcessing.Domain.Enums;

/// <summary>
/// Processing priority for PDF documents.
/// Higher value = higher priority = dequeued first.
/// Issue #5455: 4-level priority system.
/// </summary>
public enum ProcessingPriority
{
    /// <summary>
    /// Low priority — reindex operations, background maintenance.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Normal priority — standard user-uploaded PDFs.
    /// </summary>
    Normal = 10,

    /// <summary>
    /// High priority — admin-uploaded or SharedGame PDFs.
    /// </summary>
    High = 20,

    /// <summary>
    /// Urgent priority — manually bumped by admin.
    /// </summary>
    Urgent = 30
}
