using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Query to retrieve PDF upload tier limits configuration.
/// Issue #3333: PDF Upload Limits Configuration UI
/// </summary>
internal record GetPdfTierUploadLimitsQuery : IQuery<PdfTierUploadLimitsDto>;
