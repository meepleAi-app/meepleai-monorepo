using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Query to retrieve PDF upload limits configuration.
/// Issue #3072: PDF Upload Limits - Admin API
/// </summary>
internal record GetPdfUploadLimitsQuery : IQuery<PdfUploadLimitsDto>;
