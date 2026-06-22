using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

internal record ListGameBooksByGameQuery(GameRef GameRef, Guid? OwnerUserId)
    : IQuery<IReadOnlyList<GameBookDto>>;
