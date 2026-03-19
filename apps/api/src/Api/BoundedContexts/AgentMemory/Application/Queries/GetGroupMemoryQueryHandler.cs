using Api.BoundedContexts.AgentMemory.Application.DTOs;
using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Queries;

/// <summary>
/// Handles retrieving a group's memory by group ID.
/// </summary>
internal sealed class GetGroupMemoryQueryHandler : IQueryHandler<GetGroupMemoryQuery, GroupMemoryDto?>
{
    private readonly IGroupMemoryRepository _groupRepo;

    public GetGroupMemoryQueryHandler(IGroupMemoryRepository groupRepo)
    {
        _groupRepo = groupRepo ?? throw new ArgumentNullException(nameof(groupRepo));
    }

    public async Task<GroupMemoryDto?> Handle(GetGroupMemoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var group = await _groupRepo
            .GetByIdAsync(query.GroupId, cancellationToken)
            .ConfigureAwait(false);

        return group != null ? MapToDto(group) : null;
    }

    private static GroupMemoryDto MapToDto(GroupMemory group) => new(
        Id: group.Id,
        Name: group.Name,
        Members: group.Members.Select(m => new GroupMemberDto(
            UserId: m.UserId,
            GuestName: m.GuestName,
            JoinedAt: m.JoinedAt
        )).ToList(),
        Preferences: group.Preferences != null ? new GroupPreferencesDto(
            MaxDuration: group.Preferences.MaxDuration,
            PreferredComplexity: group.Preferences.PreferredComplexity?.ToString(),
            CustomNotes: group.Preferences.CustomNotes
        ) : null,
        Stats: group.Stats != null ? new GroupStatsDto(
            TotalSessions: group.Stats.TotalSessions,
            GamePlayCounts: group.Stats.GamePlayCounts,
            LastPlayedAt: group.Stats.LastPlayedAt
        ) : null
    );
}
