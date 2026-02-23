using Api.BoundedContexts.EntityRelationships.Application.DTOs;
using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Exceptions;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.EntityRelationships.Application.Commands;

/// <summary>
/// Handler for CreateEntityLinkCommand (Issue #5133).
///
/// Enforces BR-08 (duplicate check) and delegates BR-04 to EntityLink.Create.
/// </summary>
internal sealed class CreateEntityLinkCommandHandler
    : ICommandHandler<CreateEntityLinkCommand, EntityLinkDto>
{
    private readonly IEntityLinkRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateEntityLinkCommandHandler> _logger;

    public CreateEntityLinkCommandHandler(
        IEntityLinkRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<CreateEntityLinkCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EntityLinkDto> Handle(
        CreateEntityLinkCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // BR-08: reject duplicate links
        var duplicate = await _repository.ExistsAsync(
            command.SourceEntityType,
            command.SourceEntityId,
            command.TargetEntityType,
            command.TargetEntityId,
            command.LinkType,
            cancellationToken).ConfigureAwait(false);

        if (duplicate)
        {
            throw new DuplicateEntityLinkException(
                command.SourceEntityType,
                command.SourceEntityId,
                command.TargetEntityType,
                command.TargetEntityId,
                command.LinkType);
        }

        // Create aggregate — BR-04 (auto-approve user scope) and self-link guard are inside
        var link = EntityLink.Create(
            command.SourceEntityType,
            command.SourceEntityId,
            command.TargetEntityType,
            command.TargetEntityId,
            command.LinkType,
            command.Scope,
            command.OwnerUserId,
            command.Metadata,
            command.IsBggImported);

        await _repository.AddAsync(link, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "EntityLink created: {LinkId} ({SourceType}/{SourceId} → {TargetType}/{TargetId}, {LinkType}, Scope={Scope})",
            link.Id,
            link.SourceEntityType,
            link.SourceEntityId,
            link.TargetEntityType,
            link.TargetEntityId,
            link.LinkType,
            link.Scope);

        return EntityLinkDto.FromEntity(link);
    }
}
