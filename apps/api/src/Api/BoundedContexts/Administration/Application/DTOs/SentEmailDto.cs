namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Response DTO for sent email records.
/// Issue #3696: Operations - Service Control Panel.
/// </summary>
public record SentEmailDto(
    Guid Id,
    string To,
    string Subject,
    string? Preview,
    DateTime SentAt,
    string Status,
    string? ErrorMessage
);

/// <summary>
/// Response DTO for sent emails query with pagination.
/// Issue #3696: Operations - Service Control Panel.
/// </summary>
public record SentEmailsResponseDto(
    IReadOnlyList<SentEmailDto> Emails,
    int Total,
    int Limit,
    int Offset
);
