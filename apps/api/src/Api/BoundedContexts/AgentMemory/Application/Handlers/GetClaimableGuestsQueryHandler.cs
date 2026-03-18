using Api.BoundedContexts.AgentMemory.Application.DTOs;
using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Handlers;

/// <summary>
/// Handles searching for claimable guest player memories by name.
/// Returns only unclaimed records (UserId == null).
/// </summary>
internal sealed class GetClaimableGuestsQueryHandler : IQueryHandler<GetClaimableGuestsQuery, List<ClaimableGuestDto>>
{
    private readonly IPlayerMemoryRepository _playerRepo;
    private readonly IGroupMemoryRepository _groupRepo;

    public GetClaimableGuestsQueryHandler(
        IPlayerMemoryRepository playerRepo,
        IGroupMemoryRepository groupRepo)
    {
        _playerRepo = playerRepo ?? throw new ArgumentNullException(nameof(playerRepo));
        _groupRepo = groupRepo ?? throw new ArgumentNullException(nameof(groupRepo));
    }

    public async Task<List<ClaimableGuestDto>> Handle(GetClaimableGuestsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var guests = await _playerRepo
            .GetAllByGuestNameAsync(query.GuestName, cancellationToken)
            .ConfigureAwait(false);

        var result = new List<ClaimableGuestDto>();

        foreach (var guest in guests)
        {
            string? groupName = null;
            if (guest.GroupId.HasValue)
            {
                var group = await _groupRepo
                    .GetByIdAsync(guest.GroupId.Value, cancellationToken)
                    .ConfigureAwait(false);
                groupName = group?.Name;
            }

            result.Add(new ClaimableGuestDto(
                PlayerMemoryId: guest.Id,
                GuestName: guest.GuestName ?? "Unknown",
                GroupId: guest.GroupId,
                GroupName: groupName
            ));
        }

        return result;
    }
}
