using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Handler for declaring ownership of a game in the user's library.
/// Idempotent: if already declared, returns current state without error.
/// Ownership grants RAG access to the game's knowledge base.
/// </summary>
internal sealed class DeclareOwnershipCommandHandler
    : ICommandHandler<DeclareOwnershipCommand, DeclareOwnershipResult>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRagAccessService _ragAccessService;
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<DeclareOwnershipCommandHandler> _logger;

    public DeclareOwnershipCommandHandler(
        IUserLibraryRepository libraryRepository,
        IUnitOfWork unitOfWork,
        IRagAccessService ragAccessService,
        MeepleAiDbContext db,
        ILogger<DeclareOwnershipCommandHandler> logger)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DeclareOwnershipResult> Handle(
        DeclareOwnershipCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Step 1: Get library entry
        var entry = await _libraryRepository.GetByUserAndGameAsync(command.UserId, command.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Game {command.GameId} not found in your library");

        // Step 2: Declare ownership (domain method handles idempotency and wishlist validation)
        entry.DeclareOwnership();

        // Step 3: Persist
        await _libraryRepository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Step 4: Check RAG access and KB card count for response
        var hasRagAccess = await _ragAccessService.CanAccessRagAsync(
            command.UserId, command.GameId, UserRole.User, cancellationToken).ConfigureAwait(false);

        var kbCards = await _ragAccessService.GetAccessibleKbCardsAsync(
            command.UserId, command.GameId, UserRole.User, cancellationToken).ConfigureAwait(false);

        // Step 5: Check if the SharedGame has public RAG access
        var isRagPublic = await _db.SharedGames
            .AsNoTracking()
            .Where(sg => sg.Id == command.GameId && !sg.IsDeleted)
            .Select(sg => sg.IsRagPublic)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} declared ownership of game {GameId}. HasRagAccess={HasRagAccess}, KbCardCount={KbCardCount}",
            command.UserId, command.GameId, hasRagAccess, kbCards.Count);

        return new DeclareOwnershipResult(
            GameState: entry.CurrentState.Value.ToString(),
            OwnershipDeclaredAt: entry.OwnershipDeclaredAt,
            HasRagAccess: hasRagAccess,
            KbCardCount: kbCards.Count,
            IsRagPublic: isRagPublic
        );
    }
}
