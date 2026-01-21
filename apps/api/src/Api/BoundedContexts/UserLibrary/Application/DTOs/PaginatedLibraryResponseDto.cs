using System.Text.Json.Serialization;

namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for paginated library response.
/// Issue #2755: Property names aligned with frontend PaginatedLibraryResponseSchema.
/// </summary>
internal record PaginatedLibraryResponseDto(
    [property: JsonPropertyName("items")] IReadOnlyList<UserLibraryEntryDto> Items,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("totalPages")] int TotalPages,
    [property: JsonPropertyName("hasNextPage")] bool HasNextPage,
    [property: JsonPropertyName("hasPreviousPage")] bool HasPreviousPage
);
