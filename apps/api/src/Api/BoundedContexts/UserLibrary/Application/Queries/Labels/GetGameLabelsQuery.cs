using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries.Labels;

/// <summary>
/// Query to get labels assigned to a specific game in the user's library.
/// </summary>
internal record GetGameLabelsQuery(Guid UserId, Guid GameId) : IQuery<IReadOnlyList<LabelDto>>;
