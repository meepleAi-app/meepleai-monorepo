# GetFilteredSharedGamesQuery & GetApprovalQueueQuery Implementation

**Issue**: #3533 - Admin API Endpoints - Approval Queue Management
**Date**: 2026-02-04
**Status**: Completed

## Overview

Implemented two CQRS queries for admin dashboard approval workflow:

1. **GetFilteredSharedGamesQuery** - Browse, search, and filter shared games with pagination and sorting
2. **GetApprovalQueueQuery** - Get games pending approval with urgency indicators and PDF counts

## GetFilteredSharedGamesQuery

### Location
- **Query**: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetFilteredSharedGamesQuery.cs`
- **Handler**: `GetFilteredSharedGamesQueryHandler.cs`
- **Validator**: `GetFilteredSharedGamesQueryValidator.cs`

### Parameters

```csharp
internal record GetFilteredSharedGamesQuery(
    GameStatus? Status = null,           // Filter by publication status (Draft, PendingApproval, Published, Archived)
    string? Search = null,               // Search in Title and Description (case-insensitive)
    int PageNumber = 1,                  // Pagination: starts at 1
    int PageSize = 20,                   // Pagination: 1-100 items per page
    string? SortBy = null,               // Sort field: "title", "createdAt", "modifiedAt", "status", "yearPublished"
                                         // Optional ":asc" or ":desc" suffix (defaults to asc)
    Guid? SubmittedBy = null             // Filter by submitter user ID
) : IQuery<PagedResult<SharedGameDto>>;
```

### Sort Examples
- `"title"` - Ascending by title
- `"title:desc"` - Descending by title
- `"createdAt:desc"` - Newest submissions first (default if no sortBy provided)
- `"modifiedAt"` - Most recently updated
- `"status"` - By publication status

### Response
Returns `PagedResult<SharedGameDto>` with:
- **Items**: Collection of `SharedGameDto` (basic game info: id, title, status, dates, etc.)
- **Total**: Total count matching filters
- **Page**: Current page number
- **PageSize**: Items per page

### Database Query Details
- Filters applied in order: Status → Search → Submitter
- Search is case-insensitive partial match on Title and Description
- Default sort: `ModifiedAt DESC` (newest first) or `CreatedAt DESC` if ModifiedAt is null
- Pagination applied after filtering and sorting
- AsNoTracking() for read-only query optimization

### Validation Rules
- PageNumber > 0
- PageSize: 1-100
- Search: max 200 characters
- SortBy: must be valid field with optional ":asc"/":desc" suffix

## GetApprovalQueueQuery

### Location
- **Query**: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetApprovalQueueQuery.cs`
- **Handler**: `GetApprovalQueueQueryHandler.cs`
- **Validator**: `GetApprovalQueueQueryValidator.cs`
- **DTO**: `ApprovalQueueItemDto` (added to SharedGameDto.cs)

### Parameters

```csharp
internal record GetApprovalQueueQuery(
    bool? Urgency = null,               // When true: filter games >7 days pending
    Guid? Submitter = null,             // Filter by submitter user ID
    bool? HasPdfs = null                // When true: only games with attached PDFs
                                         // When false: only games without PDFs
) : IQuery<IReadOnlyList<ApprovalQueueItemDto>>;
```

### Response DTO
```csharp
public sealed record ApprovalQueueItemDto(
    Guid GameId,                        // Shared game ID
    string Title,                       // Game title
    Guid SubmittedBy,                   // Submitter user ID
    DateTime SubmittedAt,               // Submission timestamp
    int DaysPending,                    // Days since submission (calculated at runtime)
    int PdfCount                        // Number of attached documents
);
```

### Behavior
- Always filters to `GameStatus.PendingApproval` status only
- **Urgency Filter**: When true, includes only games submitted >7 days ago
- **Submitter Filter**: When specified, shows only that user's submissions
- **HasPdfs Filter**: When true shows games with ≥1 document, false shows games with 0 documents
- **Ordering**: Results sorted by `SubmittedAt` ascending (oldest first)
- **DaysPending**: Calculated client-side using current UTC time minus submission date

### All Filters Optional
All three filters are optional and can be combined:
- No filters: returns ALL games in PendingApproval status
- `Urgency=true`: only old submissions (>7 days)
- `HasPdfs=true`: only games with documentation
- `Submitter=userId`: only that user's submissions
- Combinations: `Urgency=true, HasPdfs=true` → old submissions WITH pdfs

## DTOs

### SharedGameDto (existing, unchanged)
```csharp
public sealed record SharedGameDto(
    Guid Id,
    int? BggId,
    string Title,
    int YearPublished,
    string Description,
    int MinPlayers,
    int MaxPlayers,
    int PlayingTimeMinutes,
    int MinAge,
    decimal? ComplexityRating,
    decimal? AverageRating,
    string ImageUrl,
    string ThumbnailUrl,
    GameStatus Status,
    DateTime CreatedAt,
    DateTime? ModifiedAt
);
```

### ApprovalQueueItemDto (new)
```csharp
public sealed record ApprovalQueueItemDto(
    Guid GameId,
    string Title,
    Guid SubmittedBy,
    DateTime SubmittedAt,
    int DaysPending,          // Calculated at runtime
    int PdfCount              // From g.Documents.Count()
);
```

## Implementation Notes

### GetFilteredSharedGamesQueryHandler
- Uses EF Core LINQ with AsNoTracking() for efficiency
- Case-insensitive string comparison with `StringComparison.OrdinalIgnoreCase`
- Dynamic sorting via switch statement matching field names
- Sorting always applied before pagination to ensure correctness
- Logging at INFO level for audit trail

### GetApprovalQueueQueryHandler
- Filters to PendingApproval status at database level
- Document count retrieved with `g.Documents.Count()`
- Optional filters applied client-side after database retrieval
- Days pending calculated using `DateTime.UtcNow`
- Results sorted by submission date (oldest first)
- Returns unmodifiable readonly collection

### Validation
- Fluent Validation validators included
- Case-insensitive field name comparison for SortBy
- SortBy format: "fieldName[:asc|desc]"

## Files Modified

### Created
1. `GetFilteredSharedGamesQuery.cs` - Query record
2. `GetFilteredSharedGamesQueryHandler.cs` - Handler (70 lines)
3. `GetFilteredSharedGamesQueryValidator.cs` - Validator
4. `GetApprovalQueueQuery.cs` - Query record
5. `GetApprovalQueueQueryHandler.cs` - Handler (93 lines)
6. `GetApprovalQueueQueryValidator.cs` - Validator

### Modified
1. `SharedGameDto.cs` - Added `ApprovalQueueItemDto` record

## Build Status
✅ Clean build successful
- No compiler warnings
- No analyzzer warnings
- Code follows project standards (culture-aware string operations, etc.)

## Next Steps

These queries are ready for endpoint integration:
1. `GET /api/v1/admin/shared-games` - Calls GetFilteredSharedGamesQuery
2. `GET /api/v1/approval-queue` - Calls GetApprovalQueueQuery

See Issue #3533 for endpoint implementation requirements.

## Testing Recommendations

### GetFilteredSharedGamesQuery
- Test search functionality (partial match, case-insensitive)
- Verify pagination boundaries
- Test each sort field with asc/desc
- Test filter combinations
- Verify total count accuracy

### GetApprovalQueueQuery
- Test urgency threshold (>7 days)
- Verify PDF count accuracy
- Test submitter filter
- Verify days pending calculation
- Test filter combinations
