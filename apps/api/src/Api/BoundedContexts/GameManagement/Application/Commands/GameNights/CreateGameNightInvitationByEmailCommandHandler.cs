using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Handles organizer-issued creation of a token-based game-night invitation.
/// Persists the aggregate and synchronously dispatches the invitation email
/// (D1 a — see spec §5).
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
internal sealed class CreateGameNightInvitationByEmailCommandHandler
    : ICommandHandler<CreateGameNightInvitationByEmailCommand, GameNightInvitationDto>
{
    private const string BaseUrlConfigKey = "App:BaseUrl";
    private const string ExpiryDaysConfigKey = "GameNight:InvitationExpiryDays";
    private const int DefaultExpiryDays = 14;

    private readonly IGameNightInvitationRepository _invitationRepository;
    private readonly IGameNightEventRepository _gameNightRepository;
    private readonly IUserRepository _userRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IGameNightEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public CreateGameNightInvitationByEmailCommandHandler(
        IGameNightInvitationRepository invitationRepository,
        IGameNightEventRepository gameNightRepository,
        IUserRepository userRepository,
        IGameRepository gameRepository,
        IGameNightEmailService emailService,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        TimeProvider timeProvider)
    {
        _invitationRepository = invitationRepository ?? throw new ArgumentNullException(nameof(invitationRepository));
        _gameNightRepository = gameNightRepository ?? throw new ArgumentNullException(nameof(gameNightRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<GameNightInvitationDto> Handle(
        CreateGameNightInvitationByEmailCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gameNight = await _gameNightRepository
            .GetByIdAsync(command.GameNightId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", command.GameNightId.ToString());

        if (gameNight.OrganizerId != command.OrganizerUserId)
        {
            throw new UnauthorizedAccessException(
                "Only the organizer can issue invitations for this game night");
        }

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();

        var alreadyInvited = await _invitationRepository
            .ExistsPendingByEmailAsync(command.GameNightId, normalizedEmail, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyInvited)
        {
            throw new ConflictException(
                $"A pending invitation already exists for {normalizedEmail} on this game night.");
        }

        var utcNow = _timeProvider.GetUtcNow();
        var expiryDays = _configuration.GetValue<int?>(ExpiryDaysConfigKey) ?? DefaultExpiryDays;
        var expiresAt = utcNow.AddDays(expiryDays);

        var invitation = GameNightInvitation.Create(
            gameNightId: command.GameNightId,
            email: normalizedEmail,
            expiresAt: expiresAt,
            createdBy: command.OrganizerUserId,
            utcNow: utcNow);

        await _invitationRepository.AddAsync(invitation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // D1 a: emit invitation email synchronously after persistence so guests
        // receive the token-bearing accept/decline links.
        await SendInvitationEmailAsync(gameNight, invitation, cancellationToken).ConfigureAwait(false);

        return new GameNightInvitationDto(
            Id: invitation.Id,
            Token: invitation.Token,
            GameNightId: invitation.GameNightId,
            Email: invitation.Email,
            Status: invitation.Status.ToString(),
            ExpiresAt: invitation.ExpiresAt,
            RespondedAt: invitation.RespondedAt,
            RespondedByUserId: invitation.RespondedByUserId,
            CreatedAt: invitation.CreatedAt,
            CreatedBy: invitation.CreatedBy);
    }

    private async Task SendInvitationEmailAsync(
        GameNightEvent gameNight,
        GameNightInvitation invitation,
        CancellationToken cancellationToken)
    {
        var baseUrl = _configuration[BaseUrlConfigKey]
            ?? throw new InvalidOperationException(
                $"Missing required configuration '{BaseUrlConfigKey}' for invitation URLs.");

        var organizer = await _userRepository
            .GetByIdAsync(gameNight.OrganizerId, cancellationToken)
            .ConfigureAwait(false);

        var organizerName = organizer?.DisplayName ?? "A friend";

        var gameNames = new List<string>(capacity: gameNight.GameIds.Count);
        foreach (var gameId in gameNight.GameIds)
        {
            var game = await _gameRepository.GetByIdAsync(gameId, cancellationToken).ConfigureAwait(false);
            if (game is not null)
            {
                gameNames.Add(game.Title.Value);
            }
        }

        var rsvpAcceptUrl = $"{baseUrl.TrimEnd('/')}/invites/{invitation.Token}?action=accept";
        var rsvpDeclineUrl = $"{baseUrl.TrimEnd('/')}/invites/{invitation.Token}?action=decline";
        var unsubscribeUrl = $"{baseUrl.TrimEnd('/')}/invites/{invitation.Token}/unsubscribe";

        await _emailService.SendGameNightInvitationEmailAsync(
            toEmail: invitation.Email,
            organizerName: organizerName,
            title: gameNight.Title,
            scheduledAt: gameNight.ScheduledAt,
            location: gameNight.Location,
            gameNames: gameNames,
            rsvpAcceptUrl: rsvpAcceptUrl,
            rsvpDeclineUrl: rsvpDeclineUrl,
            unsubscribeUrl: unsubscribeUrl,
            ct: cancellationToken).ConfigureAwait(false);
    }
}
