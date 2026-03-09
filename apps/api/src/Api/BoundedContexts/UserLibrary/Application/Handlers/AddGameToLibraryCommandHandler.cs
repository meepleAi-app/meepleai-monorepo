using Api.BoundedContexts.Administration.Domain.Events;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Services;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for adding a game to user's library.
/// Uses SharedGameCatalog for game validation - users add games from the shared catalog to their personal library.
/// Enforces tier-based game library quotas.
/// </summary>
internal class AddGameToLibraryCommandHandler : ICommandHandler<AddGameToLibraryCommand, UserLibraryEntryDto>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGameLibraryQuotaService _quotaService;
    private readonly MeepleAiDbContext _db;
    private readonly IPublisher _publisher;
    private readonly ILogger<AddGameToLibraryCommandHandler> _logger;

    public AddGameToLibraryCommandHandler(
        IUserLibraryRepository libraryRepository,
        ISharedGameRepository sharedGameRepository,
        IUnitOfWork unitOfWork,
        IGameLibraryQuotaService quotaService,
        MeepleAiDbContext db,
        IPublisher publisher,
        ILogger<AddGameToLibraryCommandHandler> logger)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserLibraryEntryDto> Handle(AddGameToLibraryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validate shared game exists in catalog
        var sharedGame = await _sharedGameRepository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException($"Game with ID {command.GameId} not found in catalog");

        // Check if already in library
        if (await _libraryRepository.IsGameInLibraryAsync(command.UserId, command.GameId, cancellationToken).ConfigureAwait(false))
        {
            throw new DomainException("Game is already in your library");
        }

        // Check user quota before adding game
        await CheckUserQuotaAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        // Create library entry referencing the shared game
        var entry = new UserLibraryEntry(Guid.NewGuid(), command.UserId, command.GameId);

        // Set notes if provided
        if (!string.IsNullOrWhiteSpace(command.Notes))
        {
            entry.UpdateNotes(LibraryNotes.FromNullable(command.Notes));
        }

        // Set favorite status if requested
        if (command.IsFavorite)
        {
            entry.MarkAsFavorite();
        }

        await _libraryRepository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Issue #3974: Publish cache invalidation event
        await _publisher.Publish(
            new UserLibraryGameAddedEvent(command.UserId, command.GameId),
            cancellationToken).ConfigureAwait(false);

        return new UserLibraryEntryDto(
            Id: entry.Id,
            UserId: entry.UserId,
            GameId: entry.GameId,
            GameTitle: sharedGame.Title,
            GamePublisher: sharedGame.Publishers.FirstOrDefault()?.Name,
            GameYearPublished: sharedGame.YearPublished,
            GameIconUrl: sharedGame.ThumbnailUrl,
            GameImageUrl: sharedGame.ImageUrl,
            AddedAt: entry.AddedAt,
            Notes: entry.Notes?.Value,
            IsFavorite: entry.IsFavorite,
            CurrentState: entry.CurrentState.Value.ToString(),
            StateChangedAt: entry.CurrentState.ChangedAt,
            StateNotes: entry.CurrentState.StateNotes
        );
    }

    /// <summary>
    /// Checks user quota before allowing a game to be added to the library.
    /// </summary>
    /// <param name="userId">User ID to check quota for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="DomainException">Thrown if user not found or quota exceeded</exception>
    private async Task CheckUserQuotaAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Get user tier and role from database
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.Tier, u.Role })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (user == null)
        {
            _logger.LogError("User {UserId} not found during library quota check", userId);
            throw new DomainException($"User with ID {userId} not found");
        }

        var userTier = UserTier.Parse(user.Tier);
        var userRole = Role.Parse(user.Role);

        var quotaResult = await _quotaService.CheckQuotaAsync(
            userId,
            userTier,
            userRole,
            cancellationToken).ConfigureAwait(false);

        if (!quotaResult.IsAllowed)
        {
            _logger.LogWarning(
                "Library quota exceeded for user {UserId} ({Tier}): {CurrentCount}/{MaxAllowed} games",
                userId,
                userTier.Value,
                quotaResult.CurrentCount,
                quotaResult.MaxAllowed);

            throw new DomainException(quotaResult.DenialReason ?? "Game library limit reached");
        }

        _logger.LogDebug(
            "Library quota check passed for user {UserId} ({Tier}): {CurrentCount}/{MaxAllowed} games",
            userId,
            userTier.Value,
            quotaResult.CurrentCount,
            quotaResult.MaxAllowed);
    }
}
