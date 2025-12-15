namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// Paginated response for game listing endpoint.
/// Issue: Fix empty games page - frontend expects paginated response.
/// </summary>
internal record PaginatedGamesResponse(
    IReadOnlyList<GameDto> Games,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);
