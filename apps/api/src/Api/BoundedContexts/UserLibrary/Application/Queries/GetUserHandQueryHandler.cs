using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Handler for <see cref="GetUserHandQuery"/>.
/// Returns all 4 slot types, with null entity fields for unassigned slots.
/// </summary>
internal class GetUserHandQueryHandler : IQueryHandler<GetUserHandQuery, IReadOnlyList<UserHandSlotDto>>
{
    private readonly IUserHandRepository _repo;

    public GetUserHandQueryHandler(IUserHandRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public async Task<IReadOnlyList<UserHandSlotDto>> Handle(GetUserHandQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var slots = await _repo.GetAllSlotsAsync(query.UserId, cancellationToken).ConfigureAwait(false);
        var slotMap = slots.ToDictionary(s => s.SlotType, StringComparer.OrdinalIgnoreCase);

        // Always return all 4 slot types — empty if not assigned
        return HandSlotConstants.AllSlotTypes.Select(slotType =>
        {
            if (slotMap.TryGetValue(slotType, out var slot))
            {
                return new UserHandSlotDto(
                    slotType,
                    slot.EntityId,
                    slot.EntityType,
                    slot.EntityLabel,
                    slot.EntityImageUrl,
                    slot.PinnedAt?.ToString("o")
                );
            }
            return new UserHandSlotDto(slotType, null, null, null, null, null);
        }).ToList();
    }
}
