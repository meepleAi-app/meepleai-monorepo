using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get detailed information about a board game from BoardGameGeek API.
/// Used by the wizard flow for BGG integration step.
/// Issue #4139: Backend - API Endpoints PDF Wizard
/// </summary>
/// <param name="BggId">BoardGameGeek game ID to fetch details for</param>
internal record GetBggGameDetailsQuery(
    int BggId
) : IQuery<BggGameDetailsDto?>;
