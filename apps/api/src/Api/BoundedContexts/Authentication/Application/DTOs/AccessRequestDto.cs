namespace Api.BoundedContexts.Authentication.Application.DTOs;

public record AccessRequestDto(
    Guid Id,
    string Email,
    string Status,
    DateTime RequestedAt,
    DateTime? ReviewedAt,
    Guid? ReviewedBy,
    string? RejectionReason,
    Guid? InvitationId);
