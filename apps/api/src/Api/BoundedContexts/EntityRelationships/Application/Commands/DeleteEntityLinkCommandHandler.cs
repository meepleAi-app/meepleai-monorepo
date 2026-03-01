using Api.BoundedContexts.EntityRelationships.Domain.Exceptions;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.EntityRelationships.Application.Commands;

/// <summary>
/// Handler for DeleteEntityLinkCommand (Issue #5134).
///
/// Enforces:
/// - Not-found check (EntityLinkNotFoundException)
/// - Ownership / admin authorization (UnauthorizedEntityLinkAccessException)
/// - BGG-import protection: only admins may delete BGG-imported links
/// - Soft-delete via EntityLink.Delete(deletedByUserId)
/// </summary>
internal sealed class DeleteEntityLinkCommandHandler : ICommandHandler<DeleteEntityLinkCommand>
{
    private readonly IEntityLinkRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteEntityLinkCommandHandler> _logger;

    public DeleteEntityLinkCommandHandler(
        IEntityLinkRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<DeleteEntityLinkCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        DeleteEntityLinkCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var link = await _repository.GetByIdAsync(command.EntityLinkId, cancellationToken)
            .ConfigureAwait(false);

        if (link is null)
            throw new EntityLinkNotFoundException(command.EntityLinkId);

        if (!command.IsAdmin)
        {
            // BGG-imported links: only admins may delete
            if (link.IsBggImported)
            {
                throw new UnauthorizedEntityLinkAccessException(
                    command.EntityLinkId,
                    command.RequestingUserId,
                    "BGG-imported links can only be deleted by admins.");
            }

            // Ownership check: user can only delete their own links
            if (link.OwnerUserId != command.RequestingUserId)
            {
                throw new UnauthorizedEntityLinkAccessException(
                    command.EntityLinkId,
                    command.RequestingUserId,
                    "Only the owner can delete this link.");
            }
        }

        link.Delete(command.RequestingUserId);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "EntityLink {LinkId} soft-deleted by {UserId} (isAdmin={IsAdmin})",
            link.Id,
            command.RequestingUserId,
            command.IsAdmin);
    }
}
