using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// #5139, decision of 2026-11-23: an approved share request records its author as a contributor of the shared game it
/// contributes to, and the contribution. No production path wrote <c>contributors</c> and <c>contribution_records</c>,
/// so the badge leaderboard and the contributors of a game were always empty. approve-share-request, bulk-approve and
/// approve-game-proposal record through this class, after <see cref="ShareRequest.Approve"/>, in the unit of work of
/// the approval.
/// </summary>
/// <remarks>
/// One instance per approval command. A user has one contributor per game (<c>ix_contributors_user_shared_game_unique</c>):
/// the instance keeps the contributors it touched, so two requests of one user on one game in a bulk approval give one
/// contributor and two records, the second at the next version.
/// </remarks>
internal sealed class ShareRequestApprovalContributions
{
    /// <summary>What an approval brings to its shared game.</summary>
    internal enum Kind
    {
        /// <summary>The game itself: NewGame, ApproveAsNew, ApproveAsVariant.</summary>
        NewGame,

        /// <summary>Documents or content: AdditionalContent, MergeKnowledgeBase.</summary>
        Content,

        /// <summary>A cover image: UpdateCover.</summary>
        Cover,
    }

    /// <summary>
    /// What approve-share-request and bulk-approve bring: a NewGame the game itself (its source game, already shared),
    /// an AdditionalContent its content. The other types are refused there and go through approve-game-proposal.
    /// </summary>
    public static Kind KindOf(ShareRequest shareRequest)
    {
        ArgumentNullException.ThrowIfNull(shareRequest);
        return shareRequest.ContributionType == ContributionType.NewGame ? Kind.NewGame : Kind.Content;
    }

    private readonly IContributorRepository _contributors;
    private readonly Dictionary<(Guid UserId, Guid SharedGameId), Pending> _pending = [];
    private readonly HashSet<Guid> _gamesWithANewPrimary = [];
    private readonly HashSet<Guid> _games = [];

    public ShareRequestApprovalContributions(IContributorRepository contributors)
    {
        _contributors = contributors ?? throw new ArgumentNullException(nameof(contributors));
    }

    /// <summary>
    /// Records the contribution of <paramref name="shareRequest"/> to <paramref name="sharedGameId"/>.
    /// The game itself, on a game with no primary contributor yet, makes the author its primary contributor with the
    /// initial submission. Otherwise the author's contributor (created if needed) gets the next version: a metadata
    /// update for a cover, a document addition when documents were copied, a content enhancement without them.
    /// <c>documentIds</c> are the documents copied into the shared game: the copies, not the request's originals.
    /// </summary>
    public async Task RecordAsync(
        ShareRequest shareRequest,
        Guid sharedGameId,
        Kind kind,
        IReadOnlyList<Guid> documentIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shareRequest);
        ArgumentNullException.ThrowIfNull(documentIds);

        _games.Add(sharedGameId);
        var key = (shareRequest.UserId, sharedGameId);
        if (!_pending.TryGetValue(key, out var pending))
        {
            var existing = await _contributors
                .GetByUserAndSharedGameAsync(shareRequest.UserId, sharedGameId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null && kind == Kind.NewGame
                && await CanTakeAPrimaryAsync(sharedGameId, cancellationToken).ConfigureAwait(false))
            {
                var primary = Contributor.CreatePrimaryWithInitialSubmission(
                    shareRequest.UserId, sharedGameId, shareRequest.Id, documentIds);
                _gamesWithANewPrimary.Add(sharedGameId);
                _pending[key] = new Pending(primary, IsNew: true);
                return;
            }

            pending = new Pending(
                existing ?? Contributor.Create(shareRequest.UserId, sharedGameId, isPrimary: false),
                IsNew: existing is null);
            _pending[key] = pending;
        }

        pending.Added.Add(kind switch
        {
            Kind.Cover => pending.Contributor.RecordMetadataUpdate("Cover image", shareRequest.Id),
            _ when documentIds.Count > 0 => pending.Contributor.RecordDocumentAddition(
                documentIds, "Documents added through a share request", shareRequest.Id),
            _ => pending.Contributor.RecordContentEnhancement(
                "Content added through a share request",
                shareRequest.Id,
                includesGameData: kind == Kind.NewGame),
        });
    }

    /// <summary>
    /// Hands what <see cref="RecordAsync"/> recorded to the repository, to be saved with the approval. Call it before
    /// the unit of work's <c>SaveChangesAsync</c>.
    /// </summary>
    public async Task AddToUnitOfWorkAsync(CancellationToken cancellationToken)
    {
        foreach (var pending in _pending.Values)
        {
            if (pending.IsNew)
            {
                await _contributors.AddAsync(pending.Contributor, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _contributors.AddContributionsAsync(pending.Contributor, pending.Added, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        _pending.Clear();
    }

    /// <summary>
    /// The games whose contributors this approval changed: their cached contributors are evicted after the save
    /// (<see cref="ISharedGameCacheInvalidator.EvictContributorsAsync"/>).
    /// </summary>
    public IReadOnlyCollection<Guid> Games => _games;

    /// <summary>
    /// A concurrent approval on the same game saved first, and this save hit one of the unique indexes that a
    /// read-then-write cannot see. Both approvals looked before either committed:
    /// <list type="bullet">
    /// <item><c>ix_contributors_user_shared_game_unique</c>: the same author, two contributors (#5142);</item>
    /// <item><c>ix_contributors_one_primary_per_shared_game_unique</c>: two authors, two primaries (#5143);</item>
    /// <item><c>ix_contribution_records_contributor_version_unique</c>: one contributor, the same next version (#5143).</item>
    /// </list>
    /// The approval is rolled back (one save) and a retry reads what the other saved: it is a conflict (409), not a
    /// 500. A double-submitted approval of one request used to get its 409 from xmin, and may now hit one of these first.
    /// </summary>
    public static bool IsAConcurrentApproval(DbUpdateException exception) =>
        exception?.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: { } constraint,
        }
        && ConcurrentApprovalConstraints.Contains(constraint);

    private static readonly HashSet<string> ConcurrentApprovalConstraints = new(StringComparer.Ordinal)
    {
        "ix_contributors_user_shared_game_unique",
        "ix_contributors_one_primary_per_shared_game_unique",
        "ix_contribution_records_contributor_version_unique",
    };

    /// <summary>
    /// Saves the approval with what <see cref="RecordAsync"/> recorded, in one <c>SaveChangesAsync</c>, then evicts
    /// the cached contributors of the games it touched. The approval handlers call it in place of their save.
    /// </summary>
    /// <exception cref="ConflictException">A concurrent approval of the same author on the same game (409).</exception>
    public async Task SaveWithTheApprovalAsync(
        IUnitOfWork unitOfWork,
        ISharedGameCacheInvalidator cacheInvalidator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(cacheInvalidator);

        await AddToUnitOfWorkAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsAConcurrentApproval(ex))
        {
            throw new ConflictException(
                "Another approval for the same shared game was saved at the same time: retry");
        }

        await cacheInvalidator.EvictContributorsAsync(_games, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> CanTakeAPrimaryAsync(Guid sharedGameId, CancellationToken cancellationToken) =>
        !_gamesWithANewPrimary.Contains(sharedGameId)
        && !await _contributors.HasPrimaryContributorAsync(sharedGameId, cancellationToken).ConfigureAwait(false);

    private sealed record Pending(Contributor Contributor, bool IsNew)
    {
        /// <summary>The records added here. A new contributor is saved with its whole history, an existing one with these.</summary>
        public List<ContributionRecord> Added { get; } = [];
    }
}
