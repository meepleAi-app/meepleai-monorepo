using Api.BoundedContexts.UserLibrary.Application.Commands.ProposalMigrations;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.ProposalMigrations;

/// <summary>
/// Handler for processing a user's migration choice after proposal approval.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
internal sealed class HandleMigrationChoiceCommandHandler : ICommandHandler<HandleMigrationChoiceCommand>
{
    private readonly IProposalMigrationRepository _migrationRepository;
    private readonly IPrivateGameRepository _privateGameRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<HandleMigrationChoiceCommandHandler> _logger;

    public HandleMigrationChoiceCommandHandler(
        IProposalMigrationRepository migrationRepository,
        IPrivateGameRepository privateGameRepository,
        MeepleAiDbContext dbContext,
        IUnitOfWork unitOfWork,
        ILogger<HandleMigrationChoiceCommandHandler> logger)
    {
        _migrationRepository = migrationRepository ?? throw new ArgumentNullException(nameof(migrationRepository));
        _privateGameRepository = privateGameRepository ?? throw new ArgumentNullException(nameof(privateGameRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(HandleMigrationChoiceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Retrieve the migration
        var migration = await _migrationRepository.GetByIdAsync(command.MigrationId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"ProposalMigration with ID {command.MigrationId} not found");

        // Verify ownership
        if (migration.UserId != command.UserId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to handle migration {MigrationId} owned by {OwnerId}",
                command.UserId,
                command.MigrationId,
                migration.UserId);

            throw new ForbiddenException("You can only handle your own migration choices");
        }

        // Verify migration is still pending
        if (!migration.IsPending)
        {
            throw new ConflictException($"Migration choice has already been made: {migration.Choice}");
        }

        // Execute the choice
        if (command.Choice == PostApprovalMigrationChoice.LinkToCatalog)
        {
            await HandleLinkToCatalogAsync(migration, cancellationToken).ConfigureAwait(false);
        }
        else if (command.Choice == PostApprovalMigrationChoice.KeepPrivate)
        {
            await HandleKeepPrivateAsync(migration, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "User {UserId} chose {Choice} for migration {MigrationId}",
            command.UserId,
            command.Choice,
            command.MigrationId);
    }

    /// <summary>
    /// Handles the LinkToCatalog choice:
    /// - Updates UserLibraryEntry to reference SharedGame instead of PrivateGame
    /// - Soft-deletes the PrivateGame
    /// - Marks the migration as completed
    /// </summary>
    private async Task HandleLinkToCatalogAsync(
        Domain.Entities.ProposalMigration migration,
        CancellationToken cancellationToken)
    {
        // Find the UserLibraryEntry that references this PrivateGame
        var libraryEntry = await _dbContext.UserLibraryEntries
            .FirstOrDefaultAsync(e => e.PrivateGameId == migration.PrivateGameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"UserLibraryEntry for PrivateGame {migration.PrivateGameId} not found");

        // Verify ownership (additional safety check)
        if (libraryEntry.UserId != migration.UserId)
        {
            throw new ForbiddenException("UserLibraryEntry ownership mismatch");
        }

        // Update library entry to reference SharedGame
        libraryEntry.SharedGameId = migration.SharedGameId;
        libraryEntry.PrivateGameId = null;

        // Explicitly mark as modified for consistency with pattern
        _dbContext.UserLibraryEntries.Update(libraryEntry);

        // Soft-delete the PrivateGame
        var privateGame = await _privateGameRepository.GetByIdAsync(migration.PrivateGameId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"PrivateGame {migration.PrivateGameId} not found");

        privateGame.Delete();

        // Update repositories
        await _privateGameRepository.UpdateAsync(privateGame, cancellationToken).ConfigureAwait(false);

        // Mark migration as complete
        migration.ChooseLinkToCatalog();
        await _migrationRepository.UpdateAsync(migration, cancellationToken).ConfigureAwait(false);

        // Save all changes
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException("Migration was modified by another request", ex);
        }

        _logger.LogInformation(
            "Linked UserLibraryEntry {EntryId} to SharedGame {SharedGameId}, soft-deleted PrivateGame {PrivateGameId}",
            libraryEntry.Id,
            migration.SharedGameId,
            migration.PrivateGameId);
    }

    /// <summary>
    /// Handles the KeepPrivate choice:
    /// - Marks the migration as completed without any changes to data
    /// </summary>
    private async Task HandleKeepPrivateAsync(
        Domain.Entities.ProposalMigration migration,
        CancellationToken cancellationToken)
    {
        // Simply mark the choice - no changes to PrivateGame or LibraryEntry
        migration.ChooseKeepPrivate();
        await _migrationRepository.UpdateAsync(migration, cancellationToken).ConfigureAwait(false);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException("Migration was modified by another request", ex);
        }

        _logger.LogInformation(
            "User {UserId} chose to keep PrivateGame {PrivateGameId} separate from SharedGame {SharedGameId}",
            migration.UserId,
            migration.PrivateGameId,
            migration.SharedGameId);
    }
}
