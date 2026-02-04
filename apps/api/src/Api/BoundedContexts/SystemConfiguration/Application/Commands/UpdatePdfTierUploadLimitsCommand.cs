using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to update PDF upload tier limits configuration.
/// Issue #3333: PDF Upload Limits Configuration UI
/// </summary>
internal record UpdatePdfTierUploadLimitsCommand(
    int FreeDailyLimit,
    int FreeWeeklyLimit,
    int NormalDailyLimit,
    int NormalWeeklyLimit,
    int PremiumDailyLimit,
    int PremiumWeeklyLimit,
    Guid UpdatedByUserId
) : ICommand<PdfTierUploadLimitsDto>;
