using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Response DTO for starting a review on a share request.
/// </summary>
public sealed record StartReviewResponse(
    Guid ShareRequestId,
    DateTime LockExpiresAt,
    ShareRequestDetailsDto RequestDetails);
