namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a single contribution to a shared game.
/// Part of the append-only contribution history for a contributor.
/// </summary>
public sealed class ContributionRecord
{
    /// <summary>
    /// Gets the unique identifier of this contribution record.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the ID of the contributor who made this contribution.
    /// </summary>
    public Guid ContributorId { get; }

    /// <summary>
    /// Gets the type of contribution.
    /// </summary>
    public ContributionRecordType Type { get; }

    /// <summary>
    /// Gets a description of what was contributed.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the version number of this contribution (sequential per game).
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// Gets the date and time when this contribution was made.
    /// </summary>
    public DateTime ContributedAt { get; }

    /// <summary>
    /// Gets the ID of the share request that resulted in this contribution, if any.
    /// </summary>
    public Guid? ShareRequestId { get; }

    /// <summary>
    /// Gets the IDs of documents included in this contribution.
    /// </summary>
    public IReadOnlyList<Guid> DocumentIds { get; }

    /// <summary>
    /// Gets whether this contribution includes game data changes.
    /// </summary>
    public bool IncludesGameData { get; }

    /// <summary>
    /// Gets whether this contribution includes metadata changes.
    /// </summary>
    public bool IncludesMetadata { get; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable S1144 // Unused private types or members should be removed - Required for EF Core
    private ContributionRecord()
    {
        Description = string.Empty;
        DocumentIds = Array.Empty<Guid>();
    }
#pragma warning restore S1144

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal ContributionRecord(
        Guid id,
        Guid contributorId,
        ContributionRecordType type,
        string description,
        int version,
        DateTime contributedAt,
        Guid? shareRequestId,
        IReadOnlyList<Guid>? documentIds,
        bool includesGameData,
        bool includesMetadata)
    {
        Id = id;
        ContributorId = contributorId;
        Type = type;
        Description = description;
        Version = version;
        ContributedAt = contributedAt;
        ShareRequestId = shareRequestId;
        DocumentIds = documentIds ?? Array.Empty<Guid>();
        IncludesGameData = includesGameData;
        IncludesMetadata = includesMetadata;
    }

    /// <summary>
    /// Creates a new contribution record with validation.
    /// </summary>
    /// <param name="contributorId">The ID of the contributor.</param>
    /// <param name="type">The type of contribution.</param>
    /// <param name="description">Description of the contribution.</param>
    /// <param name="version">The version number for this contribution.</param>
    /// <param name="shareRequestId">Optional share request ID that resulted in this contribution.</param>
    /// <param name="documentIds">Optional list of document IDs included.</param>
    /// <param name="includesGameData">Whether game data was changed.</param>
    /// <param name="includesMetadata">Whether metadata was changed.</param>
    /// <returns>A new ContributionRecord instance.</returns>
    /// <exception cref="ArgumentException">Thrown when required parameters are invalid.</exception>
    public static ContributionRecord Create(
        Guid contributorId,
        ContributionRecordType type,
        string description,
        int version,
        Guid? shareRequestId = null,
        IReadOnlyList<Guid>? documentIds = null,
        bool includesGameData = false,
        bool includesMetadata = false)
    {
        if (contributorId == Guid.Empty)
            throw new ArgumentException("ContributorId cannot be empty", nameof(contributorId));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required", nameof(description));

        if (description.Length > 1000)
            throw new ArgumentException("Description cannot exceed 1000 characters", nameof(description));

        if (version < 1)
            throw new ArgumentException("Version must be at least 1", nameof(version));

        return new ContributionRecord(
            Guid.NewGuid(),
            contributorId,
            type,
            description.Trim(),
            version,
            DateTime.UtcNow,
            shareRequestId,
            documentIds,
            includesGameData,
            includesMetadata);
    }

    /// <summary>
    /// Creates an initial submission contribution record.
    /// </summary>
    public static ContributionRecord CreateInitialSubmission(
        Guid contributorId,
        Guid shareRequestId,
        IReadOnlyList<Guid>? documentIds = null)
    {
        return Create(
            contributorId,
            ContributionRecordType.InitialSubmission,
            "Initial game submission",
            version: 1,
            shareRequestId,
            documentIds,
            includesGameData: true,
            includesMetadata: true);
    }

    /// <summary>
    /// Creates a document addition contribution record.
    /// </summary>
    public static ContributionRecord CreateDocumentAddition(
        Guid contributorId,
        int version,
        IReadOnlyList<Guid> documentIds,
        string description,
        Guid? shareRequestId = null)
    {
        if (documentIds == null || documentIds.Count == 0)
            throw new ArgumentException("DocumentIds must contain at least one document", nameof(documentIds));

        return Create(
            contributorId,
            ContributionRecordType.DocumentAddition,
            description,
            version,
            shareRequestId,
            documentIds,
            includesGameData: false,
            includesMetadata: false);
    }

    /// <summary>
    /// Creates a metadata update contribution record.
    /// </summary>
    public static ContributionRecord CreateMetadataUpdate(
        Guid contributorId,
        int version,
        string description,
        Guid? shareRequestId = null)
    {
        return Create(
            contributorId,
            ContributionRecordType.MetadataUpdate,
            description,
            version,
            shareRequestId,
            documentIds: null,
            includesGameData: false,
            includesMetadata: true);
    }

    /// <summary>
    /// Creates a content enhancement contribution record.
    /// </summary>
    public static ContributionRecord CreateContentEnhancement(
        Guid contributorId,
        int version,
        string description,
        Guid? shareRequestId = null,
        IReadOnlyList<Guid>? documentIds = null,
        bool includesGameData = false,
        bool includesMetadata = false)
    {
        return Create(
            contributorId,
            ContributionRecordType.ContentEnhancement,
            description,
            version,
            shareRequestId,
            documentIds,
            includesGameData,
            includesMetadata);
    }
}
