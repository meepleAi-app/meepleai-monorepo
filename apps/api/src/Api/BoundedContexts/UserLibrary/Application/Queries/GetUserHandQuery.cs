using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to retrieve all hand slots for a user.
/// Always returns all 4 slot types; empty slots have null entity fields.
/// </summary>
internal record GetUserHandQuery(Guid UserId) : IQuery<IReadOnlyList<UserHandSlotDto>>;
