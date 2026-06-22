using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateStatusBanner;

/// <summary>
/// Issue #1089: Admin command to update the global status banner.
/// Severity is a case-insensitive string ("Info" / "Warning" / "Critical").
/// </summary>
internal sealed record UpdateStatusBannerCommand(
    string Message,
    string Severity,
    bool IsActive,
    DateTime? StartsAt,
    DateTime? EndsAt,
    string? UpdatedBy) : ICommand<AdminStatusBannerResponse>;
