using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get FAQs for a game with pagination.
/// Issue #2681: Public FAQs endpoints
/// </summary>
/// <param name="GameId">The ID of the game.</param>
/// <param name="Limit">Maximum number of FAQs to return. Default is 10.</param>
/// <param name="Offset">Number of FAQs to skip. Default is 0.</param>
internal record GetGameFaqsQuery(Guid GameId, int Limit = 10, int Offset = 0)
    : IQuery<GetGameFaqsResultDto>;
