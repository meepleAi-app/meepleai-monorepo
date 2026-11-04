using System.Diagnostics.CodeAnalysis;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Exceptions;

/// <summary>
/// The processing queue has <see cref="ProcessingJob.MaxQueueSize"/> active jobs and refuses another.
/// </summary>
/// <remarks>
/// #4443 R8: this used to be a plain <see cref="ConflictException"/>, the same type as «this PDF already
/// has an active job». Callers that treat the second as success (the admin wizard's
/// <c>launch-processing</c>) swallowed a full queue too, and the document was never processed. A
/// distinct type, mapped to <c>429 Too Many Requests</c>, keeps the two apart.
/// </remarks>
public sealed class ProcessingQueueFullException : TooManyRequestsException
{
    public const string Code = "processing_queue_full";

    [SetsRequiredMembers]
    public ProcessingQueueFullException()
        : base(Code, $"Processing queue is full. Maximum {ProcessingJob.MaxQueueSize} active jobs allowed.")
    {
    }
}
