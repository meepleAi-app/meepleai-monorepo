using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// #4449: the PDF upload limits that apply to the calling user, for the web's upload zones — which
/// used to carry their own constants (50, 150, 500 MB) instead of asking.
/// </summary>
internal record GetUploadLimitsQuery(Guid UserId) : IQuery<UploadLimitsDto>;

/// <summary>
/// <paramref name="MaxFileSizeBytes"/> is the effective limit for this user: the smaller of the
/// technical limit (<c>PdfProcessing:MaxFileSizeBytes</c>) and their plan's PDF size.
/// <paramref name="MaxConcurrentChunkedUploads"/> is how many chunked uploads they may have open.
/// </summary>
internal record UploadLimitsDto(long MaxFileSizeBytes, int MaxConcurrentChunkedUploads);
