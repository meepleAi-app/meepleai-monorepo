using Api.BoundedContexts.AgentMemory.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Queries;

/// <summary>
/// Query to find claimable guest player memories matching a guest name.
/// Returns only unclaimed (UserId == null) records.
/// </summary>
internal record GetClaimableGuestsQuery(Guid UserId, string GuestName) : IQuery<List<ClaimableGuestDto>>;
