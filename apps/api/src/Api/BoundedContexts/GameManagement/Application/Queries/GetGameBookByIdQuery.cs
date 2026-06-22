using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve a single GameBook by its identifier.
/// Returns the mapped DTO; throws <see cref="Api.Middleware.Exceptions.NotFoundException"/>
/// when no book exists for the given id.
/// </summary>
internal record GetGameBookByIdQuery(Guid BookId) : IQuery<GameBookDto>;
