using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries.Labels;

/// <summary>
/// Query to get all labels available to a user (predefined + custom).
/// </summary>
internal record GetLabelsQuery(Guid UserId) : IQuery<IReadOnlyList<LabelDto>>;
