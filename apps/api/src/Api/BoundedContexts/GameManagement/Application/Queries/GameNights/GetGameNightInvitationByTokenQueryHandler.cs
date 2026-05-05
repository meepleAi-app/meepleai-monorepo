using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Handles the public, token-addressable invitation lookup. Cached at the
/// L1 process / L2 distributed level via <see cref="HybridCache"/> with the
/// per-token tag so the responded-event handler can invalidate after a
/// guest RSVPs (perf gates: cold p95 &lt; 100ms / warm &lt; 30ms — spec §6).
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
internal sealed class GetGameNightInvitationByTokenQueryHandler
    : IQueryHandler<GetGameNightInvitationByTokenQuery, PublicGameNightInvitationDto?>
{
    /// <summary>
    /// Cache tag template: callers (event handlers) build the same string
    /// to invalidate a single invitation without touching the rest of the
    /// namespace. <c>game-night-invitation:{token}</c>.
    /// </summary>
    public const string CacheTagPrefix = "game-night-invitation:";

    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        LocalCacheExpiration = TimeSpan.FromSeconds(60),
        Expiration = TimeSpan.FromSeconds(60),
    };

    private readonly IGameNightInvitationRepository _invitationRepository;
    private readonly IGameNightEventRepository _gameNightRepository;
    private readonly IUserRepository _userRepository;
    private readonly IGameRepository _gameRepository;
    private readonly HybridCache _cache;

    public GetGameNightInvitationByTokenQueryHandler(
        IGameNightInvitationRepository invitationRepository,
        IGameNightEventRepository gameNightRepository,
        IUserRepository userRepository,
        IGameRepository gameRepository,
        HybridCache cache)
    {
        _invitationRepository = invitationRepository ?? throw new ArgumentNullException(nameof(invitationRepository));
        _gameNightRepository = gameNightRepository ?? throw new ArgumentNullException(nameof(gameNightRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<PublicGameNightInvitationDto?> Handle(
        GetGameNightInvitationByTokenQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var cacheKey = CacheTagPrefix + query.Token;

        return await _cache.GetOrCreateAsync<PublicGameNightInvitationDto?>(
            cacheKey,
            async ct => await BuildDtoAsync(query.Token, ct).ConfigureAwait(false),
            CacheOptions,
            tags: new[] { cacheKey },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<PublicGameNightInvitationDto?> BuildDtoAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var invitation = await _invitationRepository
            .GetByTokenAsync(token, cancellationToken)
            .ConfigureAwait(false);

        if (invitation is null)
        {
            return null;
        }

        var gameNight = await _gameNightRepository
            .GetByIdAsync(invitation.GameNightId, cancellationToken)
            .ConfigureAwait(false);

        if (gameNight is null)
        {
            return null;
        }

        var organizer = await _userRepository
            .GetByIdAsync(gameNight.OrganizerId, cancellationToken)
            .ConfigureAwait(false);

        Guid? primaryGameId = gameNight.GameIds.Count > 0 ? gameNight.GameIds[0] : null;
        string? primaryGameName = null;
        string? primaryGameImageUrl = null;

        if (primaryGameId.HasValue)
        {
            var primaryGame = await _gameRepository
                .GetByIdAsync(primaryGameId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (primaryGame is not null)
            {
                primaryGameName = primaryGame.Title.Value;
                primaryGameImageUrl = primaryGame.ImageUrl;
            }
        }

        var acceptedInvitations = await _invitationRepository
            .CountAcceptedByGameNightIdAsync(invitation.GameNightId, cancellationToken)
            .ConfigureAwait(false);

        // AcceptedSoFar = organizer (always counted as attending) + accepted invitations.
        var acceptedSoFar = 1 + acceptedInvitations;

        return new PublicGameNightInvitationDto(
            Token: invitation.Token,
            Status: invitation.Status.ToString(),
            ExpiresAt: invitation.ExpiresAt,
            RespondedAt: invitation.RespondedAt,
            HostUserId: gameNight.OrganizerId,
            HostDisplayName: organizer?.DisplayName ?? "A friend",
            HostAvatarUrl: organizer?.AvatarUrl,
            HostWelcomeMessage: null,
            GameNightId: gameNight.Id,
            Title: gameNight.Title,
            ScheduledAt: gameNight.ScheduledAt,
            Location: gameNight.Location,
            DurationMinutes: null,
            ExpectedPlayers: gameNight.MaxPlayers ?? 0,
            AcceptedSoFar: acceptedSoFar,
            PrimaryGameId: primaryGameId,
            PrimaryGameName: primaryGameName,
            PrimaryGameImageUrl: primaryGameImageUrl,
            AlreadyRespondedAs: MapAlreadyRespondedAs(invitation.Status));
    }

    private static string? MapAlreadyRespondedAs(GameNightInvitationStatus status) => status switch
    {
        GameNightInvitationStatus.Accepted => "Accepted",
        GameNightInvitationStatus.Declined => "Declined",
        _ => null,
    };
}
