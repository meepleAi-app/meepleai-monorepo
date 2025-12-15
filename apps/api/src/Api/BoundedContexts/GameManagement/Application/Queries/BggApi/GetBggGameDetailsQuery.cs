using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.BggApi;

/// <summary>
/// Query to get detailed information about a board game from BoardGameGeek.
/// Includes full metadata: description, player count, playtime, complexity, ratings, etc.
/// External API integration with caching.
/// </summary>
internal sealed record GetBggGameDetailsQuery : IQuery<BggGameDetailsDto?>
{
    public int BggId { get; init; }
}
