using Api.BoundedContexts.AgentMemory.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Queries;

/// <summary>
/// Query to retrieve a group's memory by group ID.
/// </summary>
internal record GetGroupMemoryQuery(Guid GroupId) : IQuery<GroupMemoryDto?>;
