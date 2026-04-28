using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IGameNightInvitationRepository"/>.
/// Maps between the domain <see cref="GameNightInvitation"/> aggregate and the
/// <see cref="GameNightInvitationEntity"/> persistence model.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
internal class GameNightInvitationRepository : RepositoryBase, IGameNightInvitationRepository
{
    private readonly ILogger<GameNightInvitationRepository> _logger;

    public GameNightInvitationRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector,
        ILogger<GameNightInvitationRepository> logger)
        : base(dbContext, eventCollector)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Defensive enum parser: returns the parsed value when valid, otherwise logs an error
    /// and returns the corruption fallback. Matches the pattern in
    /// <see cref="GameNightEventRepository"/> to prevent 500 errors from legacy/typo'd
    /// status values in the persistence layer.
    /// </summary>
    private TEnum ParseEnumSafe<TEnum>(
        string rawValue,
        TEnum corruptedFallback,
        string entityId,
        string fieldName) where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(rawValue, ignoreCase: false, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        _logger.LogError(
            "Corrupted {EnumType} value '{RawValue}' for entity {EntityId}.{FieldName}. Mapped to {Fallback}.",
            typeof(TEnum).Name, rawValue, entityId, fieldName, corruptedFallback);

        return corruptedFallback;
    }

    public async Task<GameNightInvitation?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.GameNightInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken).ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<GameNightInvitation?> GetByTokenAsync(
        string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var entity = await DbContext.GameNightInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Token == token, cancellationToken).ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<GameNightInvitation>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.GameNightInvitations
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<GameNightInvitation>> GetByGameNightIdAsync(
        Guid gameNightId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.GameNightInvitations
            .AsNoTracking()
            .Where(i => i.GameNightId == gameNightId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<int> CountAcceptedByGameNightIdAsync(
        Guid gameNightId, CancellationToken cancellationToken = default)
    {
        var acceptedLiteral = nameof(GameNightInvitationStatus.Accepted);

        return await DbContext.GameNightInvitations
            .AsNoTracking()
            .Where(i => i.GameNightId == gameNightId && i.Status == acceptedLiteral)
            .CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsPendingByEmailAsync(
        Guid gameNightId, string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var pendingLiteral = nameof(GameNightInvitationStatus.Pending);
        var normalizedEmail = email.Trim().ToLowerInvariant();

        return await DbContext.GameNightInvitations
            .AsNoTracking()
            .AnyAsync(
                i => i.GameNightId == gameNightId
                    && i.Email == normalizedEmail
                    && i.Status == pendingLiteral,
                cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(
        GameNightInvitation invitation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        CollectDomainEvents(invitation);

        var entity = MapToPersistence(invitation);
        await DbContext.GameNightInvitations
            .AddAsync(entity, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task UpdateAsync(
        GameNightInvitation invitation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        CollectDomainEvents(invitation);

        var entity = MapToPersistence(invitation);
        DbContext.GameNightInvitations.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        GameNightInvitation invitation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        var entity = MapToPersistence(invitation);
        DbContext.GameNightInvitations.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.GameNightInvitations
            .AsNoTracking()
            .AnyAsync(i => i.Id == id, cancellationToken).ConfigureAwait(false);
    }

    private static GameNightInvitationEntity MapToPersistence(GameNightInvitation domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        return new GameNightInvitationEntity
        {
            Id = domain.Id,
            Token = domain.Token,
            GameNightId = domain.GameNightId,
            Email = domain.Email,
            Status = domain.Status.ToString(),
            ExpiresAt = domain.ExpiresAt,
            RespondedAt = domain.RespondedAt,
            RespondedByUserId = domain.RespondedByUserId,
            CreatedAt = domain.CreatedAt,
            CreatedBy = domain.CreatedBy
        };
    }

    private GameNightInvitation MapToDomain(GameNightInvitationEntity entity)
    {
        var status = ParseEnumSafe(
            entity.Status,
            GameNightInvitationStatus.Cancelled, // safest terminal fallback for corrupted rows
            entity.Id.ToString(),
            nameof(entity.Status));

        var invitation = GameNightInvitation.Reconstitute(
            id: entity.Id,
            token: entity.Token,
            gameNightId: entity.GameNightId,
            email: entity.Email,
            status: status,
            expiresAt: entity.ExpiresAt,
            respondedAt: entity.RespondedAt,
            respondedByUserId: entity.RespondedByUserId,
            createdAt: entity.CreatedAt,
            createdBy: entity.CreatedBy);

        // Reconstitute is a pure factory and does not raise events — defensive clear in case
        // base AggregateRoot ctor introduces any (matches GameNightEventRepository pattern).
        invitation.ClearDomainEvents();

        return invitation;
    }
}
