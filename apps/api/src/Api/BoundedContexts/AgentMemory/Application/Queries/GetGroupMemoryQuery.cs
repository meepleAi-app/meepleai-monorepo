using Api.BoundedContexts.AgentMemory.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Queries;

/// <summary>
/// Query to retrieve a group's memory by group ID.
/// </summary>
/// <param name="GroupId">Group to read.</param>
/// <param name="RequesterId">Authenticated caller — must be the creator or a member (#2655 IDOR guard).</param>
internal record GetGroupMemoryQuery(Guid GroupId, Guid RequesterId) : IQuery<GroupMemoryDto?>;
