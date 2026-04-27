using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to search shared games with filters and pagination.
/// Uses PostgreSQL full-text search for SearchTerm parameter.
/// Issue #593 (Wave A.3a): Extended with HasToolkit/HasAgent/IsTopRated/IsNew
/// filters and Contrib/New sort options for the v2 /shared-games mockup chip
/// filters (mockup `sp3-shared-games.jsx`).
/// </summary>
internal record SearchSharedGamesQuery(
    string? SearchTerm,
    List<Guid>? CategoryIds,
    List<Guid>? MechanicIds,
    int? MinPlayers,
    int? MaxPlayers,
    int? MaxPlayingTime,
    decimal? MinComplexity,
    decimal? MaxComplexity,
    GameStatus? Status,
    int PageNumber = 1,
    int PageSize = 20,
    string SortBy = "Title",
    bool SortDescending = false,
    bool? HasKnowledgeBase = null, // S2 (library-to-game epic) — filter for AI-ready games
    // Issue #593 (Wave A.3a) — chip filters from `sp3-shared-games.jsx`:
    bool? HasToolkit = null,       // chip "with-toolkit" — at least one non-default Toolkit
    bool? HasAgent = null,         // chip "with-agent" — at least one AgentDefinition
    bool? IsTopRated = null,       // chip "top-rated" — AverageRating >= configured threshold
    bool? IsNew = null             // chip "new" — NewThisWeekCount >= IsNewMinThreshold (default 2)
) : IQuery<PagedResult<SharedGameDto>>;
