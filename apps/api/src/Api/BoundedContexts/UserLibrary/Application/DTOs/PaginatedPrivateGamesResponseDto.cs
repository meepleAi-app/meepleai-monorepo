using System.Text.Json.Serialization;

namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// Paginated response DTO for the private games list endpoint.
/// JSON property names match the frontend PaginatedPrivateGamesResponseSchema.
/// </summary>
internal record PaginatedPrivateGamesResponseDto(
    [property: JsonPropertyName("items")] IReadOnlyList<PrivateGameDto> Items,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("totalPages")] int TotalPages,
    [property: JsonPropertyName("hasNextPage")] bool HasNextPage,
    [property: JsonPropertyName("hasPreviousPage")] bool HasPreviousPage
);
