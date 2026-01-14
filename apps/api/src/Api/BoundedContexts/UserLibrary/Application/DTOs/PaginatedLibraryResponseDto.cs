namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for paginated library response.
/// </summary>
internal record PaginatedLibraryResponseDto(
    IReadOnlyList<UserLibraryEntryDto> Entries,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);
