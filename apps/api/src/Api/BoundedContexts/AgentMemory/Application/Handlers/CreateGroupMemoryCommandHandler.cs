using Api.BoundedContexts.AgentMemory.Application.Commands;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.AgentMemory.Application.Handlers;

/// <summary>
/// Handles creation of a group memory with optional initial members and guests.
/// </summary>
internal sealed class CreateGroupMemoryCommandHandler : ICommandHandler<CreateGroupMemoryCommand, Guid>
{
    private readonly IGroupMemoryRepository _groupRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<CreateGroupMemoryCommandHandler> _logger;

    public CreateGroupMemoryCommandHandler(
        IGroupMemoryRepository groupRepo,
        IUnitOfWork unitOfWork,
        IFeatureFlagService featureFlags,
        ILogger<CreateGroupMemoryCommandHandler> logger)
    {
        _groupRepo = groupRepo ?? throw new ArgumentNullException(nameof(groupRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(CreateGroupMemoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var isEnabled = await _featureFlags
            .IsEnabledAsync("Features:AgentMemory.Enabled")
            .ConfigureAwait(false);

        if (!isEnabled)
            throw new InvalidOperationException("Feature AgentMemory.Enabled is disabled");

        var group = GroupMemory.Create(command.CreatorId, command.Name);

        // Add creator as first member
        group.AddMember(command.CreatorId);

        // Add initial registered members
        if (command.InitialMemberUserIds is { Count: > 0 })
        {
            foreach (var userId in command.InitialMemberUserIds.Where(id => id != command.CreatorId))
            {
                group.AddMember(userId);
            }
        }

        // Add initial guest members
        if (command.InitialGuestNames is { Count: > 0 })
        {
            foreach (var guestName in command.InitialGuestNames)
            {
                group.AddGuestMember(guestName);
            }
        }

        await _groupRepo.AddAsync(group, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created group memory {GroupId} '{GroupName}' with {MemberCount} members",
            group.Id, group.Name, group.Members.Count);

        return group.Id;
    }
}
