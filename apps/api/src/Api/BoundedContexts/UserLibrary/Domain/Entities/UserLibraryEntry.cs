using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// UserLibraryEntry aggregate root representing a game in a user's personal library.
/// This is the junction between Users (Authentication) and Games (GameManagement) contexts
/// with library-specific metadata like notes and favorites.
/// </summary>
internal sealed class UserLibraryEntry : AggregateRoot<Guid>
{
    /// <summary>
    /// The ID of the user who owns this library entry.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// The ID of the game in the library.
    /// </summary>
    public Guid GameId { get; private set; }

    /// <summary>
    /// When the game was added to the library.
    /// </summary>
    public DateTime AddedAt { get; private set; }

    /// <summary>
    /// Optional personal notes about the game.
    /// </summary>
    public LibraryNotes? Notes { get; private set; }

    /// <summary>
    /// Whether this game is marked as a favorite.
    /// </summary>
    public bool IsFavorite { get; private set; }

    /// <summary>
    /// Custom AI agent configuration for this game.
    /// Null means use system default configuration.
    /// </summary>
    public AgentConfiguration? CustomAgentConfig { get; private set; }

    /// <summary>
    /// Custom PDF rulebook metadata uploaded by user.
    /// Null means use the SharedGame's default PDF.
    /// </summary>
    public CustomPdfMetadata? CustomPdfMetadata { get; private set; }

    /// <summary>
    /// The ID of the associated private PDF document.
    /// Null means no private PDF is associated (uses SharedGame's default or custom metadata).
    /// </summary>
    public Guid? PrivatePdfId { get; private set; }

    /// <summary>
    /// Returns whether this entry has an associated private PDF.
    /// </summary>
    public bool HasPrivatePdf => PrivatePdfId.HasValue;

    /// <summary>
    /// Current state of the game in the library.
    /// </summary>
    public GameState CurrentState { get; private set; }

    /// <summary>
    /// Gameplay statistics for this game.
    /// </summary>
    public GameStats Stats { get; private set; }

    private readonly List<GameSession> _sessions = new();

    /// <summary>
    /// Collection of recorded game sessions.
    /// </summary>
    public IReadOnlyCollection<GameSession> Sessions => _sessions.AsReadOnly();

    private readonly List<GameChecklist> _checklist = new();

    /// <summary>
    /// Setup checklist items for this game.
    /// </summary>
    public IReadOnlyCollection<GameChecklist> Checklist => _checklist.AsReadOnly();

    private readonly List<UserGameLabel> _labels = new();

    /// <summary>
    /// Labels assigned to this game in the library.
    /// </summary>
    public IReadOnlyCollection<UserGameLabel> Labels => _labels.AsReadOnly();

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private UserLibraryEntry() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new library entry for a user's game.
    /// </summary>
    /// <param name="id">Unique identifier for the entry</param>
    /// <param name="userId">The user who owns this entry</param>
    /// <param name="gameId">The game being added to the library</param>
    public UserLibraryEntry(Guid id, Guid userId, Guid gameId) : base(id)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty", nameof(gameId));

        UserId = userId;
        GameId = gameId;
        AddedAt = DateTime.UtcNow;
        IsFavorite = false;
        CurrentState = GameState.Nuovo();
        Stats = GameStats.Empty();

        AddDomainEvent(new GameAddedToLibraryEvent(id, userId, gameId));
    }

    /// <summary>
    /// Updates the personal notes for this game.
    /// </summary>
    /// <param name="notes">New notes (or null to clear)</param>
    public void UpdateNotes(LibraryNotes? notes)
    {
        Notes = notes;
    }

    /// <summary>
    /// Updates the notes from a string value.
    /// </summary>
    /// <param name="notes">Notes string (or null/empty to clear)</param>
    public void UpdateNotesFromString(string? notes)
    {
        Notes = LibraryNotes.FromNullable(notes);
    }

    /// <summary>
    /// Toggles the favorite status.
    /// </summary>
    public void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
    }

    /// <summary>
    /// Marks this game as a favorite.
    /// </summary>
    public void MarkAsFavorite()
    {
        IsFavorite = true;
    }

    /// <summary>
    /// Removes the favorite mark from this game.
    /// </summary>
    public void RemoveFavorite()
    {
        IsFavorite = false;
    }

    /// <summary>
    /// Sets the favorite status explicitly.
    /// </summary>
    /// <param name="isFavorite">True to mark as favorite, false otherwise</param>
    public void SetFavorite(bool isFavorite)
    {
        IsFavorite = isFavorite;
    }

    /// <summary>
    /// Prepares this entry for removal, raising the appropriate domain event.
    /// Call this before deleting the entry from the repository.
    /// </summary>
    public void PrepareForRemoval()
    {
        AddDomainEvent(new GameRemovedFromLibraryEvent(Id, UserId, GameId));
    }

    /// <summary>
    /// Configures a custom AI agent for this game.
    /// Replaces any existing custom configuration.
    /// </summary>
    /// <param name="agentConfig">The agent configuration to apply</param>
    public void ConfigureAgent(AgentConfiguration agentConfig)
    {
        ArgumentNullException.ThrowIfNull(agentConfig);
        CustomAgentConfig = agentConfig;
    }

    /// <summary>
    /// Resets the AI agent to use system default configuration.
    /// Removes any custom agent configuration.
    /// </summary>
    public void ResetAgentToDefault()
    {
        CustomAgentConfig = null;
    }

    /// <summary>
    /// Uploads a custom PDF rulebook for this game.
    /// Replaces any existing custom PDF.
    /// </summary>
    /// <param name="pdfMetadata">Metadata for the uploaded PDF</param>
    public void UploadCustomPdf(CustomPdfMetadata pdfMetadata)
    {
        ArgumentNullException.ThrowIfNull(pdfMetadata);
        CustomPdfMetadata = pdfMetadata;
    }

    /// <summary>
    /// Resets to use the SharedGame's default PDF rulebook.
    /// Removes any custom PDF metadata.
    /// </summary>
    public void ResetPdfToShared()
    {
        CustomPdfMetadata = null;
        PrivatePdfId = null;
    }

    /// <summary>
    /// Associates a private PDF document with this library entry.
    /// Raises a PrivatePdfAssociatedEvent domain event.
    /// </summary>
    /// <param name="pdfDocumentId">The ID of the PDF document to associate</param>
    public void AssociatePrivatePdf(Guid pdfDocumentId)
    {
        if (pdfDocumentId == Guid.Empty)
            throw new ArgumentException("PDF document ID cannot be empty", nameof(pdfDocumentId));

        PrivatePdfId = pdfDocumentId;

        AddDomainEvent(new PrivatePdfAssociatedEvent(
            libraryEntryId: Id,
            userId: UserId,
            gameId: GameId,
            pdfDocumentId: pdfDocumentId));
    }

    /// <summary>
    /// Removes the private PDF document from this library entry.
    /// Raises a PrivatePdfRemovedEvent domain event to trigger vector cleanup.
    /// Issue #3651: Enables cleanup of vectors from private_rules collection.
    /// </summary>
    /// <exception cref="ConflictException">Thrown when no private PDF is associated</exception>
    public void RemovePrivatePdf()
    {
        if (!HasPrivatePdf)
            throw new ConflictException("No private PDF is associated with this library entry");

        var pdfId = PrivatePdfId!.Value;
        PrivatePdfId = null;

        AddDomainEvent(new PrivatePdfRemovedEvent(
            libraryEntryId: Id,
            userId: UserId,
            gameId: GameId,
            pdfDocumentId: pdfId));
    }

    /// <summary>
    /// Returns whether this entry uses a custom agent configuration.
    /// </summary>
    public bool HasCustomAgent() => CustomAgentConfig is not null;

    // ========== GAME STATE MANAGEMENT ==========

    /// <summary>
    /// Changes the state of the game.
    /// </summary>
    /// <param name="newState">The new game state</param>
    /// <exception cref="InvalidOperationException">Thrown when state transition is not allowed</exception>
    public void ChangeState(GameState newState)
    {
        ArgumentNullException.ThrowIfNull(newState);

        // Validate state transition
        if (!CurrentState.CanTransitionTo(newState.Value))
        {
            throw new ConflictException(
                $"Cannot transition from {CurrentState.Value} to {newState.Value}");
        }

        var previousState = CurrentState.Value;
        CurrentState = newState;

        AddDomainEvent(new GameStateChangedEvent(
            libraryEntryId: Id,
            userId: UserId,
            gameId: GameId,
            previousState: previousState,
            newState: newState.Value,
            occurredAt: DateTime.UtcNow));
    }

    /// <summary>
    /// Marks the game as owned (available for play).
    /// </summary>
    public void MarkAsOwned(string? notes = null)
    {
        ChangeState(GameState.Owned(notes));
    }

    /// <summary>
    /// Marks the game as on loan.
    /// </summary>
    /// <param name="borrowerInfo">Information about who borrowed it</param>
    public void MarkAsOnLoan(string? borrowerInfo)
    {
        ChangeState(GameState.InPrestito(borrowerInfo));
    }

    /// <summary>
    /// Adds the game to wishlist.
    /// </summary>
    public void AddToWishlist(string? notes = null)
    {
        ChangeState(GameState.Wishlist(notes));
    }

    /// <summary>
    /// Returns whether the game is currently available for play.
    /// </summary>
    public bool IsAvailableForPlay() => CurrentState.IsAvailable();

    // ========== GAME SESSION MANAGEMENT ==========

    /// <summary>
    /// Records a new game session and updates statistics.
    /// </summary>
    /// <param name="playedAt">When the session was played</param>
    /// <param name="durationMinutes">Duration in minutes</param>
    /// <param name="didWin">Whether the user won (null for non-competitive)</param>
    /// <param name="players">Comma-separated list of players</param>
    /// <param name="notes">Optional session notes</param>
    /// <returns>The created GameSession</returns>
    public GameSession RecordGameSession(
        DateTime playedAt,
        int durationMinutes,
        bool? didWin = null,
        string? players = null,
        string? notes = null)
    {
        var session = GameSession.Create(
            userLibraryEntryId: Id,
            playedAt: playedAt,
            durationMinutes: durationMinutes,
            didWin: didWin,
            players: players,
            notes: notes);

        _sessions.Add(session);

        // Update statistics
        Stats = Stats.RecordSession(durationMinutes, didWin, playedAt);

        AddDomainEvent(new GameSessionRecordedEvent(
            libraryEntryId: Id,
            userId: UserId,
            gameId: GameId,
            sessionId: session.Id,
            playedAt: playedAt,
            durationMinutes: durationMinutes,
            didWin: didWin,
            occurredAt: DateTime.UtcNow));

        return session;
    }

    /// <summary>
    /// Removes a game session and recalculates statistics.
    /// Note: Statistics recalculation requires reprocessing all remaining sessions.
    /// </summary>
    /// <param name="sessionId">ID of the session to remove</param>
    /// <exception cref="InvalidOperationException">Thrown when session not found</exception>
    public void RemoveGameSession(Guid sessionId)
    {
        var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
        if (session is null)
            throw new NotFoundException($"Session {sessionId} not found");

        _sessions.Remove(session);

        // Recalculate stats from remaining sessions
        Stats = GameStats.Empty();
        foreach (var s in _sessions.OrderBy(x => x.PlayedAt))
        {
            Stats = Stats.RecordSession(s.DurationMinutes, s.DidWin, s.PlayedAt);
        }
    }

    /// <summary>
    /// Gets the most recent game session.
    /// </summary>
    public GameSession? GetLatestSession()
        => _sessions.OrderByDescending(s => s.PlayedAt).FirstOrDefault();

    // ========== CHECKLIST MANAGEMENT ==========

    /// <summary>
    /// Adds a new checklist item.
    /// </summary>
    /// <param name="description">Step description</param>
    /// <param name="additionalInfo">Optional additional info</param>
    /// <returns>The created GameChecklist item</returns>
    public GameChecklist AddChecklistItem(string description, string? additionalInfo = null)
    {
        var nextOrder = _checklist.Count > 0 ? _checklist.Max(c => c.DisplayOrder) + 1 : 0;

        var item = GameChecklist.Create(
            userLibraryEntryId: Id,
            description: description,
            displayOrder: nextOrder,
            additionalInfo: additionalInfo);

        _checklist.Add(item);

        return item;
    }

    /// <summary>
    /// Removes a checklist item.
    /// </summary>
    /// <param name="itemId">ID of the item to remove</param>
    /// <exception cref="InvalidOperationException">Thrown when item not found</exception>
    public void RemoveChecklistItem(Guid itemId)
    {
        var item = _checklist.FirstOrDefault(c => c.Id == itemId);
        if (item is null)
            throw new NotFoundException($"Checklist item {itemId} not found");

        _checklist.Remove(item);

        // Reorder remaining items
        var orderedItems = _checklist.OrderBy(c => c.DisplayOrder).ToList();
        for (int i = 0; i < orderedItems.Count; i++)
        {
            orderedItems[i].UpdateOrder(i);
        }
    }

    /// <summary>
    /// Resets all checklist items to incomplete (typically before a new game session).
    /// </summary>
    public void ResetChecklist()
    {
        foreach (var item in _checklist)
        {
            item.ResetCompletion();
        }
    }

    /// <summary>
    /// Gets checklist completion progress as a percentage (0-100).
    /// </summary>
    public decimal GetChecklistProgress()
    {
        if (_checklist.Count == 0)
            return 100m; // No checklist means "nothing to do" = 100% complete

        var completedCount = _checklist.Count(c => c.IsCompleted);
        return (decimal)completedCount / _checklist.Count * 100m;
    }

    /// <summary>
    /// Returns whether all checklist items are completed.
    /// </summary>
    public bool IsChecklistComplete()
        => _checklist.Count > 0 && _checklist.All(c => c.IsCompleted);

    /// <summary>
    /// Gets the checklist items ordered by their DisplayOrder property.
    /// </summary>
    public IReadOnlyList<GameChecklist> GetOrderedChecklist()
        => _checklist.OrderBy(c => c.DisplayOrder).ToList().AsReadOnly();

    // ========== LABEL MANAGEMENT ==========

    /// <summary>
    /// Adds a label to this game in the library.
    /// </summary>
    /// <param name="labelId">The ID of the label to add</param>
    /// <returns>The created UserGameLabel assignment</returns>
    /// <exception cref="ConflictException">Thrown when the label is already assigned</exception>
    public UserGameLabel AddLabel(Guid labelId)
    {
        if (labelId == Guid.Empty)
            throw new ArgumentException("LabelId cannot be empty", nameof(labelId));

        // Check if label is already assigned
        if (_labels.Any(l => l.LabelId == labelId))
            throw new ConflictException($"Label {labelId} is already assigned to this game");

        var assignment = UserGameLabel.Create(Id, labelId);
        _labels.Add(assignment);

        return assignment;
    }

    /// <summary>
    /// Removes a label from this game in the library.
    /// </summary>
    /// <param name="labelId">The ID of the label to remove</param>
    /// <exception cref="NotFoundException">Thrown when the label is not assigned</exception>
    public void RemoveLabel(Guid labelId)
    {
        var assignment = _labels.FirstOrDefault(l => l.LabelId == labelId);
        if (assignment is null)
            throw new NotFoundException($"Label {labelId} is not assigned to this game");

        _labels.Remove(assignment);
    }

    /// <summary>
    /// Removes all labels from this game.
    /// </summary>
    public void ClearLabels()
    {
        _labels.Clear();
    }

    /// <summary>
    /// Checks if a specific label is assigned to this game.
    /// </summary>
    public bool HasLabel(Guid labelId) => _labels.Any(l => l.LabelId == labelId);

    /// <summary>
    /// Gets the count of labels assigned to this game.
    /// </summary>
    public int GetLabelCount() => _labels.Count;
}
