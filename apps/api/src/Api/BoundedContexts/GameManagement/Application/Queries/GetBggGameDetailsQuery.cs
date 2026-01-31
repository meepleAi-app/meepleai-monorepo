using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to get detailed information about a board game from BoardGameGeek API.
/// Includes full metadata: description, player count, playtime, complexity, ratings.
/// AI-13: External API integration for comprehensive game information.
/// </summary>
/// <param name="BggId">BoardGameGeek game ID (positive integer)</param>
internal record GetBggGameDetailsQuery(
    int BggId
) : IQuery<BggGameDetailsDto?>;
