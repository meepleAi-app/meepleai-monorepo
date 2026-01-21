namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// DTO for paginated game sessions response.
/// Matches frontend PaginatedSessionsResponseSchema.
/// Issue #2755: Schema validation error fix.
/// </summary>
internal record PaginatedSessionsResponseDto(
    IReadOnlyList<GameSessionDto> Sessions,
    int Total,
    int Page,
    int PageSize
);
