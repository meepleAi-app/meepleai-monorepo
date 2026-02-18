namespace Api.BoundedContexts.DocumentProcessing.Domain.Enums;

/// <summary>
/// Processing priority for PDF documents.
/// Admin-uploaded PDFs are processed before user PDFs.
/// </summary>
public enum ProcessingPriority
{
    /// <summary>
    /// Normal user-uploaded PDFs.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Admin-uploaded PDFs with elevated priority.
    /// </summary>
    Admin = 10
}
