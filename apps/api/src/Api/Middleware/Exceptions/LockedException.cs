namespace Api.Middleware.Exceptions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Exception thrown when a resource exists but is in a transient state that
/// prevents normal access (e.g. a KB document still being indexed).
/// Maps to HTTP 423 Locked.
/// </summary>
/// <remarks>
/// Wave 3 Phase 3 (Issue #805 / PR #732 §6.3.1): introduced for KB document
/// retrieval where <c>processingStatus != 'ready'</c>. Distinct from 404 (which
/// would imply "not found / never existed") so the FE can render a
/// "Documento in elaborazione..." state with retry semantics, per Nygard's
/// operational note in the spec.
/// </remarks>
public class LockedException : HttpException
{
    [SetsRequiredMembers]
    public LockedException(string message)
        : base(StatusCodes.Status423Locked, "locked", message)
    {
    }

    [SetsRequiredMembers]
    public LockedException(string message, Exception innerException)
        : base(StatusCodes.Status423Locked, "locked", message, innerException)
    {
    }

    public LockedException()
    {
    }
}
