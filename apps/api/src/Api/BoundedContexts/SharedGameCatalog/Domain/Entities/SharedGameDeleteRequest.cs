using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Entity representing a delete request for a shared game.
/// Editors must request approval before deleting games.
/// </summary>
public sealed class SharedGameDeleteRequest : Entity<Guid>
{
    private Guid _id;
    private Guid _sharedGameId;
    private Guid _requestedBy;
    private readonly string _reason = string.Empty;
    private DeleteRequestStatus _status;
    private Guid? _reviewedBy;
    private string? _reviewComment;
    private readonly DateTime _createdAt;
    private DateTime? _reviewedAt;

    public new Guid Id => _id;
    public Guid SharedGameId => _sharedGameId;
    public Guid RequestedBy => _requestedBy;
    public string Reason => _reason;
    public DeleteRequestStatus Status => _status;
    public Guid? ReviewedBy => _reviewedBy;
    public string? ReviewComment => _reviewComment;
    public DateTime CreatedAt => _createdAt;
    public DateTime? ReviewedAt => _reviewedAt;

    private SharedGameDeleteRequest() : base()
    {
    }

    internal SharedGameDeleteRequest(
        Guid id,
        Guid sharedGameId,
        Guid requestedBy,
        string reason,
        DeleteRequestStatus status,
        Guid? reviewedBy,
        string? reviewComment,
        DateTime createdAt,
        DateTime? reviewedAt) : base(id)
    {
        _id = id;
        _sharedGameId = sharedGameId;
        _requestedBy = requestedBy;
        _reason = reason;
        _status = status;
        _reviewedBy = reviewedBy;
        _reviewComment = reviewComment;
        _createdAt = createdAt;
        _reviewedAt = reviewedAt;
    }

    public static SharedGameDeleteRequest Create(
        Guid sharedGameId,
        Guid requestedBy,
        string reason)
    {
        if (sharedGameId == Guid.Empty)
            throw new ArgumentException("SharedGameId cannot be empty", nameof(sharedGameId));

        if (requestedBy == Guid.Empty)
            throw new ArgumentException("RequestedBy cannot be empty", nameof(requestedBy));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required", nameof(reason));

        if (reason.Length > 1000)
            throw new ArgumentException("Reason cannot exceed 1000 characters", nameof(reason));

        return new SharedGameDeleteRequest(
            Guid.NewGuid(),
            sharedGameId,
            requestedBy,
            reason,
            DeleteRequestStatus.Pending,
            null,
            null,
            DateTime.UtcNow,
            null);
    }

    public void Approve(Guid reviewedBy, string? comment = null)
    {
        if (reviewedBy == Guid.Empty)
            throw new ArgumentException("ReviewedBy cannot be empty", nameof(reviewedBy));

        if (_status != DeleteRequestStatus.Pending)
            throw new InvalidOperationException($"Cannot approve request in {_status} status");

        _status = DeleteRequestStatus.Approved;
        _reviewedBy = reviewedBy;
        _reviewComment = comment;
        _reviewedAt = DateTime.UtcNow;
    }

    public void Reject(Guid reviewedBy, string reason)
    {
        if (reviewedBy == Guid.Empty)
            throw new ArgumentException("ReviewedBy cannot be empty", nameof(reviewedBy));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required", nameof(reason));

        if (_status != DeleteRequestStatus.Pending)
            throw new InvalidOperationException($"Cannot reject request in {_status} status");

        _status = DeleteRequestStatus.Rejected;
        _reviewedBy = reviewedBy;
        _reviewComment = reason;
        _reviewedAt = DateTime.UtcNow;
    }
}
