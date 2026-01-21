using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Entity representing a user who has contributed to a shared game.
/// Part of the SharedGame aggregate, not an aggregate root.
/// Tracks contribution history for attribution and gamification.
/// </summary>
public sealed class Contributor : Entity<Guid>
{
    private Guid _id;
    private Guid _userId;
    private Guid _sharedGameId;
    private readonly bool _isPrimaryContributor;
    private readonly DateTime _createdAt;
    private DateTime? _modifiedAt;

    private readonly List<ContributionRecord> _contributions = new();

    /// <summary>
    /// Gets the unique identifier of this contributor record.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the ID of the user who made the contributions.
    /// </summary>
    public Guid UserId => _userId;

    /// <summary>
    /// Gets the ID of the shared game this contributor is associated with.
    /// </summary>
    public Guid SharedGameId => _sharedGameId;

    /// <summary>
    /// Gets whether this is the primary (original) contributor who first shared the game.
    /// </summary>
    public bool IsPrimaryContributor => _isPrimaryContributor;

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    /// Gets the last modification timestamp.
    /// </summary>
    public DateTime? ModifiedAt => _modifiedAt;

    /// <summary>
    /// Gets the contribution history for this contributor.
    /// </summary>
    public IReadOnlyCollection<ContributionRecord> Contributions => _contributions.AsReadOnly();

    /// <summary>
    /// Gets the total number of contributions made by this contributor.
    /// </summary>
    public int ContributionCount => _contributions.Count;

    /// <summary>
    /// Gets the latest contribution version number.
    /// </summary>
    public int LatestVersion => _contributions.Count > 0
        ? _contributions.Max(c => c.Version)
        : 0;

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable S1144 // Unused private types or members should be removed - Required for EF Core
    private Contributor() : base()
    {
        _createdAt = DateTime.UtcNow;
    }
#pragma warning restore S1144

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal Contributor(
        Guid id,
        Guid userId,
        Guid sharedGameId,
        bool isPrimaryContributor,
        DateTime createdAt,
        DateTime? modifiedAt,
        List<ContributionRecord>? contributions = null) : base(id)
    {
        _id = id;
        _userId = userId;
        _sharedGameId = sharedGameId;
        _isPrimaryContributor = isPrimaryContributor;
        _createdAt = createdAt;
        _modifiedAt = modifiedAt;

        if (contributions != null)
            _contributions.AddRange(contributions);
    }

    /// <summary>
    /// Creates a new contributor for a shared game.
    /// </summary>
    /// <param name="userId">The ID of the contributing user.</param>
    /// <param name="sharedGameId">The ID of the shared game.</param>
    /// <param name="isPrimary">Whether this is the primary (original) contributor.</param>
    /// <returns>A new Contributor instance.</returns>
    /// <exception cref="ArgumentException">Thrown when required parameters are invalid.</exception>
    public static Contributor Create(Guid userId, Guid sharedGameId, bool isPrimary)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        if (sharedGameId == Guid.Empty)
            throw new ArgumentException("SharedGameId cannot be empty", nameof(sharedGameId));

        return new Contributor(
            Guid.NewGuid(),
            userId,
            sharedGameId,
            isPrimary,
            DateTime.UtcNow,
            null);
    }

    /// <summary>
    /// Creates a primary contributor with their initial submission contribution record.
    /// </summary>
    /// <param name="userId">The ID of the contributing user.</param>
    /// <param name="sharedGameId">The ID of the shared game.</param>
    /// <param name="shareRequestId">The ID of the share request that created the game.</param>
    /// <param name="documentIds">Optional document IDs included in the submission.</param>
    /// <returns>A new Contributor instance with initial contribution recorded.</returns>
    public static Contributor CreatePrimaryWithInitialSubmission(
        Guid userId,
        Guid sharedGameId,
        Guid shareRequestId,
        IReadOnlyList<Guid>? documentIds = null)
    {
        var contributor = Create(userId, sharedGameId, isPrimary: true);

        var initialContribution = ContributionRecord.CreateInitialSubmission(
            contributor.Id,
            shareRequestId,
            documentIds);

        contributor._contributions.Add(initialContribution);

        return contributor;
    }

    /// <summary>
    /// Adds a contribution record to this contributor's history.
    /// </summary>
    /// <param name="record">The contribution record to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when record is null.</exception>
    /// <exception cref="ArgumentException">Thrown when record contributor ID doesn't match.</exception>
    public void AddContribution(ContributionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.ContributorId != _id)
            throw new ArgumentException(
                "Contribution record must belong to this contributor",
                nameof(record));

        _contributions.Add(record);
        _modifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a document addition contribution.
    /// </summary>
    /// <param name="documentIds">The IDs of documents added.</param>
    /// <param name="description">Description of the documents added.</param>
    /// <param name="shareRequestId">Optional share request ID.</param>
    /// <returns>The created contribution record.</returns>
    public ContributionRecord RecordDocumentAddition(
        IReadOnlyList<Guid> documentIds,
        string description,
        Guid? shareRequestId = null)
    {
        var nextVersion = LatestVersion + 1;
        var record = ContributionRecord.CreateDocumentAddition(
            _id,
            nextVersion,
            documentIds,
            description,
            shareRequestId);

        _contributions.Add(record);
        _modifiedAt = DateTime.UtcNow;

        return record;
    }

    /// <summary>
    /// Records a metadata update contribution.
    /// </summary>
    /// <param name="description">Description of the metadata changes.</param>
    /// <param name="shareRequestId">Optional share request ID.</param>
    /// <returns>The created contribution record.</returns>
    public ContributionRecord RecordMetadataUpdate(
        string description,
        Guid? shareRequestId = null)
    {
        var nextVersion = LatestVersion + 1;
        var record = ContributionRecord.CreateMetadataUpdate(
            _id,
            nextVersion,
            description,
            shareRequestId);

        _contributions.Add(record);
        _modifiedAt = DateTime.UtcNow;

        return record;
    }

    /// <summary>
    /// Records a content enhancement contribution.
    /// </summary>
    /// <param name="description">Description of the enhancement.</param>
    /// <param name="shareRequestId">Optional share request ID.</param>
    /// <param name="documentIds">Optional document IDs included.</param>
    /// <param name="includesGameData">Whether game data was changed.</param>
    /// <param name="includesMetadata">Whether metadata was changed.</param>
    /// <returns>The created contribution record.</returns>
    public ContributionRecord RecordContentEnhancement(
        string description,
        Guid? shareRequestId = null,
        IReadOnlyList<Guid>? documentIds = null,
        bool includesGameData = false,
        bool includesMetadata = false)
    {
        var nextVersion = LatestVersion + 1;
        var record = ContributionRecord.CreateContentEnhancement(
            _id,
            nextVersion,
            description,
            shareRequestId,
            documentIds,
            includesGameData,
            includesMetadata);

        _contributions.Add(record);
        _modifiedAt = DateTime.UtcNow;

        return record;
    }

    /// <summary>
    /// Gets contributions of a specific type.
    /// </summary>
    public IEnumerable<ContributionRecord> GetContributionsByType(ContributionRecordType type)
    {
        return _contributions.Where(c => c.Type == type);
    }

    /// <summary>
    /// Gets the most recent contribution.
    /// </summary>
    public ContributionRecord? GetLatestContribution()
    {
        return _contributions
            .OrderByDescending(c => c.ContributedAt)
            .FirstOrDefault();
    }
}
