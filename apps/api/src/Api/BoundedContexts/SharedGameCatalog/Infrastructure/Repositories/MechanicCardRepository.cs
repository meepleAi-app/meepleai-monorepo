using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for the <see cref="MechanicCard"/> aggregate (ADR-051 / #527).
/// </summary>
internal sealed class MechanicCardRepository : RepositoryBase, IMechanicCardRepository
{
    public MechanicCardRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(MechanicCard card, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);

        var entity = MapToEntity(card);
        await DbContext.MechanicCards.AddAsync(entity, cancellationToken).ConfigureAwait(false);

        CollectDomainEvents(card);
    }

    public void AddAuditLog(MechanicCardAuditLog entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        DbContext.MechanicCardAuditLog.Add(MapAuditToEntity(entry));
    }

    public async Task<int> GetMaxVersionForGameAsync(Guid sharedGameId, CancellationToken cancellationToken = default)
    {
        // Version is monotonic across ALL cards for the game (suppressed or not), so ignore the filter.
        var max = await DbContext.MechanicCards
            .IgnoreQueryFilters()
            .Where(c => c.SharedGameId == sharedGameId)
            .Select(c => (int?)c.Version)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false);

        return max ?? 0;
    }

    public async Task<MechanicCard?> GetActiveByGameAsync(Guid sharedGameId, CancellationToken cancellationToken = default)
    {
        // Suppression query filter applies → only the active (non-suppressed) card is returned.
        var entity = await DbContext.MechanicCards
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SharedGameId == sharedGameId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<MechanicCard?> GetByIdIgnoringFiltersAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.MechanicCards
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == cardId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<MechanicCardApaContext> GetApaContextAsync(
        Guid sharedGameId, Guid pdfDocumentId, CancellationToken cancellationToken = default)
    {
        var year = await DbContext.SharedGames
            .AsNoTracking()
            .Where(g => g.Id == sharedGameId)
            .Select(g => (int?)g.YearPublished)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        string? documentName = null;
        if (pdfDocumentId != Guid.Empty)
        {
            documentName = await DbContext.Set<PdfDocumentEntity>()
                .AsNoTracking()
                .Where(p => p.Id == pdfDocumentId)
                .Select(p => p.Title ?? p.FileName)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return new MechanicCardApaContext(year, documentName);
    }

    // === Mapping ===

    private static MechanicCardEntity MapToEntity(MechanicCard card) =>
        new()
        {
            Id = card.Id,
            SharedGameId = card.SharedGameId,
            OriginAnalysisId = card.OriginAnalysisId,
            Origin = OriginToDb(card.Origin),
            Title = card.Title,
            Content = card.Content,
            Version = card.Version,
            IsSuppressed = card.IsSuppressed,
            SuppressedReason = card.SuppressedReason,
            SuppressedAt = card.SuppressedAt,
            SuppressedBy = card.SuppressedBy,
            ErrorReportsCount = card.ErrorReportsCount,
            FeedbackScore = card.FeedbackScore,
            PublishedAt = card.PublishedAt,
            PublishedBy = card.PublishedBy,
            CreatedAt = card.CreatedAt,
            UpdatedAt = card.UpdatedAt,
            Xmin = card.XminVersion
        };

    private static MechanicCard MapToDomain(MechanicCardEntity entity) =>
        MechanicCard.Reconstitute(
            id: entity.Id,
            sharedGameId: entity.SharedGameId,
            originAnalysisId: entity.OriginAnalysisId,
            origin: OriginFromDb(entity.Origin),
            title: entity.Title,
            content: entity.Content,
            version: entity.Version,
            isSuppressed: entity.IsSuppressed,
            suppressedReason: entity.SuppressedReason,
            suppressedAt: entity.SuppressedAt,
            suppressedBy: entity.SuppressedBy,
            errorReportsCount: entity.ErrorReportsCount,
            feedbackScore: entity.FeedbackScore,
            publishedAt: entity.PublishedAt,
            publishedBy: entity.PublishedBy,
            createdAt: entity.CreatedAt,
            updatedAt: entity.UpdatedAt,
            xminVersion: entity.Xmin);

    private static MechanicCardAuditLogEntity MapAuditToEntity(MechanicCardAuditLog entry) =>
        new()
        {
            Id = entry.Id,
            CardId = entry.CardId,
            Action = ActionToDb(entry.Action),
            ActorId = entry.ActorId,
            OccurredAt = entry.OccurredAt,
            Metadata = entry.Metadata
        };

    private static string OriginToDb(MechanicCardOrigin origin) => origin switch
    {
        MechanicCardOrigin.AiReviewed => "ai_reviewed",
        MechanicCardOrigin.Manual => "manual",
        MechanicCardOrigin.ImportedExternal => "imported_external",
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, "Unknown MechanicCardOrigin.")
    };

    private static MechanicCardOrigin OriginFromDb(string origin) => origin switch
    {
        "ai_reviewed" => MechanicCardOrigin.AiReviewed,
        "manual" => MechanicCardOrigin.Manual,
        "imported_external" => MechanicCardOrigin.ImportedExternal,
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, "Unknown mechanic_cards.origin value.")
    };

    private static string ActionToDb(MechanicCardAuditAction action) => action switch
    {
        MechanicCardAuditAction.Published => "published",
        MechanicCardAuditAction.Suppressed => "suppressed",
        MechanicCardAuditAction.Unsuppressed => "unsuppressed",
        MechanicCardAuditAction.Revised => "revised",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown MechanicCardAuditAction.")
    };
}
