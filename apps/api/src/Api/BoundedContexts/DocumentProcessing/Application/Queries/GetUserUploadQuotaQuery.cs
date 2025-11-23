using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get user's PDF upload quota information.
/// Returns current usage, limits, and reset times for daily/weekly quotas.
/// </summary>
public sealed record GetUserUploadQuotaQuery(
    Guid UserId
) : IQuery<PdfUploadQuotaInfo>;
