using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Query to retrieve PDF upload limits for all user tiers.
/// Returns a list with one entry per tier (free, normal, premium).
/// Issue #3673: PDF Upload Limits Admin UI
/// </summary>
internal sealed record GetAllPdfLimitsQuery : IQuery<IReadOnlyList<PdfLimitConfigDto>>;
