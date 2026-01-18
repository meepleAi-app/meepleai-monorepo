using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Aggregate root representing a shared game in the catalog.
/// Manages game core information, gameplay details, and publication status.
/// </summary>
public sealed class SharedGame : AggregateRoot<Guid>
{
    // Identity
    private Guid _id;
    private int? _bggId;

    // Core Info
    private string _title = string.Empty;
    private int _yearPublished;
    private string _description = string.Empty;

    // Gameplay
    private int _minPlayers;
    private int _maxPlayers;
    private int _playingTimeMinutes;
    private int _minAge;

    // Ratings
    private decimal? _complexityRating;
    private decimal? _averageRating;

    // Media
    private string _imageUrl = string.Empty;
    private string _thumbnailUrl = string.Empty;

    // Rules
    private GameRules? _rules;

    // Status & Metadata
    private GameStatus _status;
    private bool _isDeleted;
    private Guid _createdBy;
    private Guid? _modifiedBy;
    private readonly DateTime _createdAt;
    private DateTime? _modifiedAt;

    // Collections (navigation properties)
    private readonly List<GameDesigner> _designers = new();
    private readonly List<GamePublisher> _publishers = new();
    private readonly List<GameCategory> _categories = new();
    private readonly List<GameMechanic> _mechanics = new();
    private readonly List<GameFaq> _faqs = new();
    private readonly List<GameErrata> _erratas = new();
    private readonly List<QuickQuestion> _quickQuestions = new();

    /// <summary>
    /// Gets the unique identifier of this game.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the BoardGameGeek ID if imported from BGG.
    /// </summary>
    public int? BggId => _bggId;

    /// <summary>
    /// Gets the game title.
    /// </summary>
    public string Title => _title;

    /// <summary>
    /// Gets the year the game was published.
    /// </summary>
    public int YearPublished => _yearPublished;

    /// <summary>
    /// Gets the game description.
    /// </summary>
    public string Description => _description;

    /// <summary>
    /// Gets the minimum number of players.
    /// </summary>
    public int MinPlayers => _minPlayers;

    /// <summary>
    /// Gets the maximum number of players.
    /// </summary>
    public int MaxPlayers => _maxPlayers;

    /// <summary>
    /// Gets the typical playing time in minutes.
    /// </summary>
    public int PlayingTimeMinutes => _playingTimeMinutes;

    /// <summary>
    /// Gets the minimum recommended age.
    /// </summary>
    public int MinAge => _minAge;

    /// <summary>
    /// Gets the complexity rating (1.0 - 5.0).
    /// </summary>
    public decimal? ComplexityRating => _complexityRating;

    /// <summary>
    /// Gets the average user rating (1.0 - 10.0).
    /// </summary>
    public decimal? AverageRating => _averageRating;

    /// <summary>
    /// Gets the URL to the game's main image.
    /// </summary>
    public string ImageUrl => _imageUrl;

    /// <summary>
    /// Gets the URL to the game's thumbnail image.
    /// </summary>
    public string ThumbnailUrl => _thumbnailUrl;

    /// <summary>
    /// Gets the game rules.
    /// </summary>
    public GameRules? Rules => _rules;

    /// <summary>
    /// Gets the publication status of this game.
    /// </summary>
    public GameStatus Status => _status;

    /// <summary>
    /// Gets whether this game has been soft-deleted.
    /// </summary>
    public bool IsDeleted => _isDeleted;

    /// <summary>
    /// Gets the ID of the user who created this game entry.
    /// </summary>
    public Guid CreatedBy => _createdBy;

    /// <summary>
    /// Gets the ID of the user who last modified this game entry.
    /// </summary>
    public Guid? ModifiedBy => _modifiedBy;

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    /// Gets the last modification timestamp.
    /// </summary>
    public DateTime? ModifiedAt => _modifiedAt;

    /// <summary>
    /// Gets the designers who created this game.
    /// </summary>
    public IReadOnlyCollection<GameDesigner> Designers => _designers.AsReadOnly();

    /// <summary>
    /// Gets the publishers who published this game.
    /// </summary>
    public IReadOnlyCollection<GamePublisher> Publishers => _publishers.AsReadOnly();

    /// <summary>
    /// Gets the categories this game belongs to.
    /// </summary>
    public IReadOnlyCollection<GameCategory> Categories => _categories.AsReadOnly();

    /// <summary>
    /// Gets the mechanics this game uses.
    /// </summary>
    public IReadOnlyCollection<GameMechanic> Mechanics => _mechanics.AsReadOnly();

    /// <summary>
    /// Gets the FAQs for this game.
    /// </summary>
    public IReadOnlyCollection<GameFaq> Faqs => _faqs.AsReadOnly();

    /// <summary>
    /// Gets the errata for this game.
    /// </summary>
    public IReadOnlyCollection<GameErrata> Erratas => _erratas.AsReadOnly();

    /// <summary>
    /// Gets the quick questions for this game.
    /// </summary>
    public IReadOnlyCollection<QuickQuestion> QuickQuestions => _quickQuestions.AsReadOnly();

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private SharedGame() : base()
    {
    }

    /// <summary>
    /// Private constructor for creating a new SharedGame instance (with validation and events).
    /// </summary>
    private SharedGame(
        Guid id,
        string title,
        int yearPublished,
        string description,
        int minPlayers,
        int maxPlayers,
        int playingTimeMinutes,
        int minAge,
        decimal? complexityRating,
        decimal? averageRating,
        string imageUrl,
        string thumbnailUrl,
        GameRules? rules,
        Guid createdBy,
        int? bggId = null) : base(id)
    {
        _id = id;
        _title = title;
        _yearPublished = yearPublished;
        _description = description;
        _minPlayers = minPlayers;
        _maxPlayers = maxPlayers;
        _playingTimeMinutes = playingTimeMinutes;
        _minAge = minAge;
        _complexityRating = complexityRating;
        _averageRating = averageRating;
        _imageUrl = imageUrl;
        _thumbnailUrl = thumbnailUrl;
        _rules = rules;
        _status = GameStatus.Draft;
        _isDeleted = false;
        _createdBy = createdBy;
        _createdAt = DateTime.UtcNow;
        _bggId = bggId;
    }

    /// <summary>
    /// Internal constructor for reconstituting a SharedGame from persistence.
    /// Used only by the repository. Does not raise domain events.
    /// </summary>
    internal SharedGame(
        Guid id,
        string title,
        int yearPublished,
        string description,
        int minPlayers,
        int maxPlayers,
        int playingTimeMinutes,
        int minAge,
        decimal? complexityRating,
        decimal? averageRating,
        string imageUrl,
        string thumbnailUrl,
        GameRules? rules,
        GameStatus status,
        Guid createdBy,
        Guid? modifiedBy,
        DateTime createdAt,
        DateTime? modifiedAt,
        bool isDeleted,
        int? bggId = null) : base(id)
    {
        _id = id;
        _title = title;
        _yearPublished = yearPublished;
        _description = description;
        _minPlayers = minPlayers;
        _maxPlayers = maxPlayers;
        _playingTimeMinutes = playingTimeMinutes;
        _minAge = minAge;
        _complexityRating = complexityRating;
        _averageRating = averageRating;
        _imageUrl = imageUrl;
        _thumbnailUrl = thumbnailUrl;
        _rules = rules;
        _status = status;
        _isDeleted = isDeleted;
        _createdBy = createdBy;
        _modifiedBy = modifiedBy;
        _createdAt = createdAt;
        _modifiedAt = modifiedAt;
        _bggId = bggId;
    }

    /// <summary>
    /// Creates a new SharedGame with validation.
    /// </summary>
    /// <returns>A new SharedGame instance in Draft status</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    public static SharedGame Create(
        string title,
        int yearPublished,
        string description,
        int minPlayers,
        int maxPlayers,
        int playingTimeMinutes,
        int minAge,
        decimal? complexityRating,
        decimal? averageRating,
        string imageUrl,
        string thumbnailUrl,
        GameRules? rules,
        Guid createdBy,
        int? bggId = null)
    {
        // Validation
        ValidateTitle(title);
        ValidateYear(yearPublished);
        ValidateDescription(description);
        ValidatePlayers(minPlayers, maxPlayers);
        ValidatePlayingTime(playingTimeMinutes);
        ValidateMinAge(minAge);
        ValidateComplexityRating(complexityRating);
        ValidateAverageRating(averageRating);
        ValidateImageUrl(imageUrl);
        ValidateThumbnailUrl(thumbnailUrl);

        if (createdBy == Guid.Empty)
            throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));

        var gameId = Guid.NewGuid();
        var game = new SharedGame(
            gameId,
            title,
            yearPublished,
            description,
            minPlayers,
            maxPlayers,
            playingTimeMinutes,
            minAge,
            complexityRating,
            averageRating,
            imageUrl,
            thumbnailUrl,
            rules,
            createdBy,
            bggId);

        game.AddDomainEvent(new SharedGameCreatedEvent(gameId, title, createdBy));

        return game;
    }

    /// <summary>
    /// Updates the game information.
    /// </summary>
    public void UpdateInfo(
        string title,
        int yearPublished,
        string description,
        int minPlayers,
        int maxPlayers,
        int playingTimeMinutes,
        int minAge,
        decimal? complexityRating,
        decimal? averageRating,
        string imageUrl,
        string thumbnailUrl,
        GameRules? rules,
        Guid modifiedBy)
    {
        // Validation
        ValidateTitle(title);
        ValidateYear(yearPublished);
        ValidateDescription(description);
        ValidatePlayers(minPlayers, maxPlayers);
        ValidatePlayingTime(playingTimeMinutes);
        ValidateMinAge(minAge);
        ValidateComplexityRating(complexityRating);
        ValidateAverageRating(averageRating);
        ValidateImageUrl(imageUrl);
        ValidateThumbnailUrl(thumbnailUrl);

        if (modifiedBy == Guid.Empty)
            throw new ArgumentException("ModifiedBy cannot be empty", nameof(modifiedBy));

        _title = title;
        _yearPublished = yearPublished;
        _description = description;
        _minPlayers = minPlayers;
        _maxPlayers = maxPlayers;
        _playingTimeMinutes = playingTimeMinutes;
        _minAge = minAge;
        _complexityRating = complexityRating;
        _averageRating = averageRating;
        _imageUrl = imageUrl;
        _thumbnailUrl = thumbnailUrl;
        _rules = rules;
        _modifiedBy = modifiedBy;
        _modifiedAt = DateTime.UtcNow;

        AddDomainEvent(new SharedGameUpdatedEvent(_id, modifiedBy));
    }

    /// <summary>
    /// Submits the game for approval, transitioning from Draft to PendingApproval.
    /// Issue #2514: Approval workflow implementation
    /// </summary>
    /// <param name="submittedBy">The ID of the user submitting for approval</param>
    /// <exception cref="InvalidOperationException">Thrown when game is not in Draft status</exception>
    public void SubmitForApproval(Guid submittedBy)
    {
        if (_status != GameStatus.Draft)
            throw new InvalidOperationException($"Cannot submit game for approval in {_status} status. Only Draft games can be submitted.");

        if (submittedBy == Guid.Empty)
            throw new ArgumentException("SubmittedBy cannot be empty", nameof(submittedBy));

        _status = GameStatus.PendingApproval;
        _modifiedBy = submittedBy;
        _modifiedAt = DateTime.UtcNow;

        AddDomainEvent(new SharedGameSubmittedForApprovalEvent(_id, submittedBy));
    }

    /// <summary>
    /// Approves the publication, transitioning from PendingApproval to Published.
    /// Issue #2514: Approval workflow implementation
    /// </summary>
    /// <param name="approvedBy">The ID of the admin approving the publication</param>
    /// <exception cref="InvalidOperationException">Thrown when game is not in PendingApproval status</exception>
    public void ApprovePublication(Guid approvedBy)
    {
        if (_status != GameStatus.PendingApproval)
            throw new InvalidOperationException($"Cannot approve publication in {_status} status. Only PendingApproval games can be approved.");

        if (approvedBy == Guid.Empty)
            throw new ArgumentException("ApprovedBy cannot be empty", nameof(approvedBy));

        _status = GameStatus.Published;
        _modifiedBy = approvedBy;
        _modifiedAt = DateTime.UtcNow;

        AddDomainEvent(new SharedGamePublicationApprovedEvent(_id, approvedBy));
    }

    /// <summary>
    /// Rejects the publication, transitioning from PendingApproval back to Draft.
    /// Issue #2514: Approval workflow implementation
    /// </summary>
    /// <param name="rejectedBy">The ID of the admin rejecting the publication</param>
    /// <param name="reason">The reason for rejection</param>
    /// <exception cref="InvalidOperationException">Thrown when game is not in PendingApproval status</exception>
    public void RejectPublication(Guid rejectedBy, string reason)
    {
        if (_status != GameStatus.PendingApproval)
            throw new InvalidOperationException($"Cannot reject publication in {_status} status. Only PendingApproval games can be rejected.");

        if (rejectedBy == Guid.Empty)
            throw new ArgumentException("RejectedBy cannot be empty", nameof(rejectedBy));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required", nameof(reason));

        _status = GameStatus.Draft;
        _modifiedBy = rejectedBy;
        _modifiedAt = DateTime.UtcNow;

        AddDomainEvent(new SharedGamePublicationRejectedEvent(_id, rejectedBy, reason));
    }

    /// <summary>
    /// [DEPRECATED] Legacy method for backward compatibility.
    /// Use SubmitForApproval() followed by ApprovePublication() instead.
    /// Direct publishing bypasses the approval workflow (Issue #2514).
    /// Planned for removal in 2026-Q2 after client migration.
    /// </summary>
    /// <param name="publishedBy">The ID of the user publishing the game</param>
    /// <exception cref="InvalidOperationException">Thrown when game is not in Draft status</exception>
#pragma warning disable S1133 // Deprecated code should be removed - Planned removal in 2026-Q2
    [Obsolete("Use SubmitForApproval() and ApprovePublication() for proper approval workflow (Issue #2514)")]
    public void Publish(Guid publishedBy)
#pragma warning restore S1133
    {
        if (_status != GameStatus.Draft)
            throw new InvalidOperationException($"Cannot publish game in {_status} status");

        if (publishedBy == Guid.Empty)
            throw new ArgumentException("PublishedBy cannot be empty", nameof(publishedBy));

        // Direct transition: Draft → Published (bypassing approval)
        _status = GameStatus.Published;
        _modifiedBy = publishedBy;
        _modifiedAt = DateTime.UtcNow;

        AddDomainEvent(new SharedGamePublishedEvent(_id, publishedBy));
    }

    /// <summary>
    /// Archives the game, removing it from public view.
    /// </summary>
    /// <param name="archivedBy">The ID of the user archiving the game</param>
    /// <exception cref="InvalidOperationException">Thrown when game is already archived</exception>
    public void Archive(Guid archivedBy)
    {
        if (_status == GameStatus.Archived)
            throw new InvalidOperationException("Game is already archived");

        if (archivedBy == Guid.Empty)
            throw new ArgumentException("ArchivedBy cannot be empty", nameof(archivedBy));

        _status = GameStatus.Archived;
        _modifiedBy = archivedBy;
        _modifiedAt = DateTime.UtcNow;

        AddDomainEvent(new SharedGameArchivedEvent(_id, archivedBy));
    }

    /// <summary>
    /// Deletes the game by setting IsDeleted flag (soft delete).
    /// Only games in non-draft status can be deleted.
    /// </summary>
    /// <param name="deletedBy">The ID of the user deleting the game</param>
    /// <exception cref="ArgumentException">Thrown when deletedBy is empty</exception>
    public void Delete(Guid deletedBy)
    {
        if (deletedBy == Guid.Empty)
            throw new ArgumentException("DeletedBy cannot be empty", nameof(deletedBy));

        if (_isDeleted)
            throw new InvalidOperationException("Game is already deleted");

        _isDeleted = true;
        _modifiedBy = deletedBy;
        _modifiedAt = DateTime.UtcNow;

        AddDomainEvent(new SharedGameDeletedEvent(_id, deletedBy));
    }

    /// <summary>
    /// Adds a FAQ to this game.
    /// </summary>
    /// <param name="faq">The FAQ to add</param>
    /// <exception cref="ArgumentNullException">Thrown when faq is null</exception>
    public void AddFaq(GameFaq faq)
    {
        ArgumentNullException.ThrowIfNull(faq);

        if (faq.SharedGameId != _id)
            throw new ArgumentException("FAQ does not belong to this game", nameof(faq));

        _faqs.Add(faq);
        AddDomainEvent(new GameFaqAddedEvent(_id, faq.Id, faq.Question));
    }

    /// <summary>
    /// Updates an existing FAQ in this game.
    /// </summary>
    /// <param name="faqId">The ID of the FAQ to update</param>
    /// <param name="question">The new question text</param>
    /// <param name="answer">The new answer text</param>
    /// <param name="order">The new display order</param>
    /// <exception cref="InvalidOperationException">Thrown when FAQ is not found</exception>
    public void UpdateFaq(Guid faqId, string question, string answer, int order)
    {
        if (faqId == Guid.Empty)
            throw new ArgumentException("FaqId cannot be empty", nameof(faqId));

        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question is required", nameof(question));

        if (question.Length > 500)
            throw new ArgumentException("Question cannot exceed 500 characters", nameof(question));

        if (string.IsNullOrWhiteSpace(answer))
            throw new ArgumentException("Answer is required", nameof(answer));

        if (order < 0)
            throw new ArgumentException("Order cannot be negative", nameof(order));

        var faq = _faqs.FirstOrDefault(f => f.Id == faqId);
        if (faq is null)
            throw new InvalidOperationException($"FAQ with ID {faqId} not found in this game");

        // Create updated FAQ (recreate with new values since entity is immutable)
        var updatedFaq = new GameFaq(faqId, _id, question, answer, order, faq.CreatedAt);

        // Remove old and add updated
        _faqs.Remove(faq);
        _faqs.Add(updatedFaq);
    }

    /// <summary>
    /// Removes an existing FAQ from this game.
    /// </summary>
    /// <param name="faqId">The ID of the FAQ to remove</param>
    /// <exception cref="InvalidOperationException">Thrown when FAQ is not found</exception>
    public void RemoveFaq(Guid faqId)
    {
        if (faqId == Guid.Empty)
            throw new ArgumentException("FaqId cannot be empty", nameof(faqId));

        var faq = _faqs.FirstOrDefault(f => f.Id == faqId);
        if (faq is null)
            throw new InvalidOperationException($"FAQ with ID {faqId} not found in this game");

        _faqs.Remove(faq);
    }

    /// <summary>
    /// Adds an erratum to this game.
    /// </summary>
    /// <param name="errata">The erratum to add</param>
    /// <exception cref="ArgumentNullException">Thrown when errata is null</exception>
    public void AddErrata(GameErrata errata)
    {
        ArgumentNullException.ThrowIfNull(errata);

        if (errata.SharedGameId != _id)
            throw new ArgumentException("Erratum does not belong to this game", nameof(errata));

        _erratas.Add(errata);
        AddDomainEvent(new GameErrataAddedEvent(_id, errata.Id, errata.Description));
    }

    /// <summary>
    /// Updates an existing erratum in this game.
    /// </summary>
    /// <param name="errataId">The ID of the erratum to update</param>
    /// <param name="description">The new description</param>
    /// <param name="pageReference">The new page reference</param>
    /// <param name="publishedDate">The new published date</param>
    /// <exception cref="InvalidOperationException">Thrown when erratum is not found</exception>
    public void UpdateErrata(Guid errataId, string description, string pageReference, DateTime publishedDate)
    {
        if (errataId == Guid.Empty)
            throw new ArgumentException("ErrataId cannot be empty", nameof(errataId));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required", nameof(description));

        if (string.IsNullOrWhiteSpace(pageReference))
            throw new ArgumentException("PageReference is required", nameof(pageReference));

        if (pageReference.Length > 100)
            throw new ArgumentException("PageReference cannot exceed 100 characters", nameof(pageReference));

        if (publishedDate > DateTime.UtcNow)
            throw new ArgumentException("PublishedDate cannot be in the future", nameof(publishedDate));

        var errata = _erratas.FirstOrDefault(e => e.Id == errataId);
        if (errata is null)
            throw new InvalidOperationException($"Erratum with ID {errataId} not found in this game");

        // Create updated erratum (recreate with new values since entity is immutable)
        var updatedErrata = new GameErrata(errataId, _id, description, pageReference, publishedDate, errata.CreatedAt);

        // Remove old and add updated
        _erratas.Remove(errata);
        _erratas.Add(updatedErrata);
    }

    /// <summary>
    /// Removes an existing erratum from this game.
    /// </summary>
    /// <param name="errataId">The ID of the erratum to remove</param>
    /// <exception cref="InvalidOperationException">Thrown when erratum is not found</exception>
    public void RemoveErrata(Guid errataId)
    {
        if (errataId == Guid.Empty)
            throw new ArgumentException("ErrataId cannot be empty", nameof(errataId));

        var errata = _erratas.FirstOrDefault(e => e.Id == errataId);
        if (errata is null)
            throw new InvalidOperationException($"Erratum with ID {errataId} not found in this game");

        _erratas.Remove(errata);
    }

    /// <summary>
    /// Adds a quick question to this game.
    /// </summary>
    /// <param name="question">The quick question to add</param>
    /// <exception cref="ArgumentNullException">Thrown when question is null</exception>
    public void AddQuickQuestion(QuickQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        if (question.SharedGameId != _id)
            throw new ArgumentException("Quick question does not belong to this game", nameof(question));

        _quickQuestions.Add(question);
    }

    /// <summary>
    /// Removes a quick question from this game.
    /// </summary>
    /// <param name="questionId">The ID of the question to remove</param>
    /// <exception cref="InvalidOperationException">Thrown when question is not found</exception>
    public void RemoveQuickQuestion(Guid questionId)
    {
        if (questionId == Guid.Empty)
            throw new ArgumentException("QuestionId cannot be empty", nameof(questionId));

        var question = _quickQuestions.FirstOrDefault(q => q.Id == questionId);
        if (question is null)
            throw new InvalidOperationException($"Quick question with ID {questionId} not found in this game");

        _quickQuestions.Remove(question);
    }

    /// <summary>
    /// Clears all quick questions and replaces them with a new set (used during AI regeneration).
    /// </summary>
    /// <param name="questions">The new questions to set</param>
    public void ReplaceQuickQuestions(IEnumerable<QuickQuestion> questions)
    {
        ArgumentNullException.ThrowIfNull(questions);

        _quickQuestions.Clear();
        _quickQuestions.AddRange(questions);
    }

    // Validation Methods

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

        if (title.Length > 500)
            throw new ArgumentException("Title cannot exceed 500 characters", nameof(title));
    }

    private static void ValidateYear(int year)
    {
        if (year <= 1900)
            throw new ArgumentException("Year must be greater than 1900", nameof(year));

        if (year > DateTime.UtcNow.Year + 1)
            throw new ArgumentException($"Year cannot exceed {DateTime.UtcNow.Year + 1}", nameof(year));
    }

    private static void ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required", nameof(description));
    }

    private static void ValidatePlayers(int minPlayers, int maxPlayers)
    {
        if (minPlayers <= 0)
            throw new ArgumentException("MinPlayers must be greater than 0", nameof(minPlayers));

        if (maxPlayers < minPlayers)
            throw new ArgumentException("MaxPlayers must be greater than or equal to MinPlayers", nameof(maxPlayers));
    }

    private static void ValidatePlayingTime(int playingTimeMinutes)
    {
        if (playingTimeMinutes <= 0)
            throw new ArgumentException("PlayingTime must be greater than 0", nameof(playingTimeMinutes));
    }

    private static void ValidateMinAge(int minAge)
    {
        if (minAge < 0)
            throw new ArgumentException("MinAge cannot be negative", nameof(minAge));
    }

    private static void ValidateComplexityRating(decimal? rating)
    {
        if (rating.HasValue && (rating.Value < 1.0m || rating.Value > 5.0m))
            throw new ArgumentException("ComplexityRating must be between 1.0 and 5.0", nameof(rating));
    }

    private static void ValidateAverageRating(decimal? rating)
    {
        if (rating.HasValue && (rating.Value < 1.0m || rating.Value > 10.0m))
            throw new ArgumentException("AverageRating must be between 1.0 and 10.0", nameof(rating));
    }

    private static void ValidateImageUrl(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new ArgumentException("ImageUrl is required", nameof(imageUrl));

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
            throw new ArgumentException("ImageUrl must be a valid URL", nameof(imageUrl));
    }

    private static void ValidateThumbnailUrl(string thumbnailUrl)
    {
        if (string.IsNullOrWhiteSpace(thumbnailUrl))
            throw new ArgumentException("ThumbnailUrl is required", nameof(thumbnailUrl));

        if (!Uri.TryCreate(thumbnailUrl, UriKind.Absolute, out _))
            throw new ArgumentException("ThumbnailUrl must be a valid URL", nameof(thumbnailUrl));
    }
}
