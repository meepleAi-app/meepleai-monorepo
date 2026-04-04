// Handlers for session invite functionality (Issue #3354).

using System.Text;

using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Middleware.Exceptions;

using MediatR;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

// ============================================================================
// Get Session By Invite Token Handler
// ============================================================================

public sealed class GetSessionByInviteQueryHandler : IRequestHandler<GetSessionByInviteQuery, SessionInviteResponse>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<GetSessionByInviteQueryHandler> _logger;

    public GetSessionByInviteQueryHandler(
        ISessionRepository sessionRepository,
        ILogger<GetSessionByInviteQueryHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _logger = logger;
    }

    public async Task<SessionInviteResponse> Handle(GetSessionByInviteQuery request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByInviteTokenAsync(request.InviteToken, cancellationToken).ConfigureAwait(false);
        if (session == null)
        {
            throw new NotFoundException("Invalid or expired invite link.");
        }

        // Check if invite is still valid
        var canJoin = true;
        string? reasonCannotJoin = null;

        if (!session.IsInviteTokenValid(request.InviteToken))
        {
            canJoin = false;
            reasonCannotJoin = "Invite link has expired.";
        }
        else if (session.Status == SessionStatus.Finalized)
        {
            canJoin = false;
            reasonCannotJoin = "This session has already ended.";
        }

        var owner = session.Participants.FirstOrDefault(p => p.IsOwner);

        _logger.LogInformation(
            "Retrieved session {SessionId} by invite token (canJoin: {CanJoin})",
            session.Id,
            canJoin);

        return new SessionInviteResponse(
            SessionId: session.Id,
            SessionCode: session.SessionCode,
            GameName: null, // Would need to join with Game table
            GameImageUrl: null,
            SessionDate: session.SessionDate,
            Location: session.Location,
            Status: session.Status.ToString(),
            ParticipantCount: session.Participants.Count,
            OwnerDisplayName: owner?.DisplayName ?? "Unknown",
            CanJoin: canJoin,
            ReasonCannotJoin: reasonCannotJoin
        );
    }
}

// ============================================================================
// Generate Invite Token Handler
// ============================================================================

public sealed class GenerateInviteTokenCommandHandler : IRequestHandler<GenerateInviteTokenCommand, InviteTokenResponse>
{
    // QR code generation uses a simple text-based approach
    // In production, use a library like QRCoder or ZXing.Net
#pragma warning disable S1075 // URIs should not be hardcoded - this is a fallback default
    private const string DefaultBaseUrl = "https://meepleai.com";
#pragma warning restore S1075

    private readonly ISessionRepository _sessionRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GenerateInviteTokenCommandHandler> _logger;

    public GenerateInviteTokenCommandHandler(
        ISessionRepository sessionRepository,
        IConfiguration configuration,
        ILogger<GenerateInviteTokenCommandHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<InviteTokenResponse> Handle(GenerateInviteTokenCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        if (session == null)
        {
            throw new NotFoundException($"Session {request.SessionId} not found.");
        }

        // Verify user has permission (must be owner or participant)
        if (session.UserId != request.RequestedBy &&
            !session.Participants.Any(p => p.UserId == request.RequestedBy && p.IsOwner))
        {
            throw new ForbiddenException("Only the session owner can generate invite links.");
        }

        // Generate the invite token
        var token = session.GenerateInviteToken(request.ExpiresInHours);

        // Save the session with new invite token
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        var baseUrl = _configuration["App:BaseUrl"] ?? DefaultBaseUrl;
        var inviteUrl = $"{baseUrl}/sessions/join/{token}";

        // Generate QR code as data URL
        var qrCodeDataUrl = GenerateQrCodeDataUrl(inviteUrl);

        _logger.LogInformation(
            "Generated invite token for session {SessionId} by user {UserId}, expires: {ExpiresAt}",
            request.SessionId,
            request.RequestedBy,
            session.InviteExpiresAt);

        return new InviteTokenResponse(
            InviteToken: token,
            InviteUrl: inviteUrl,
            ExpiresAt: session.InviteExpiresAt,
            SessionCode: session.SessionCode,
            QrCodeDataUrl: qrCodeDataUrl
        );
    }

    /// <summary>
    /// Generates a simple QR code as a data URL.
    /// In production, use a proper QR code library like QRCoder.
    /// This generates a placeholder SVG for now.
    /// </summary>
    private static string GenerateQrCodeDataUrl(string content)
    {
        // Simple SVG placeholder QR code
        // In production, use QRCoder or similar library
        var svg = $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 200 200"">
            <rect width=""200"" height=""200"" fill=""white""/>
            <rect x=""10"" y=""10"" width=""50"" height=""50"" fill=""black""/>
            <rect x=""140"" y=""10"" width=""50"" height=""50"" fill=""black""/>
            <rect x=""10"" y=""140"" width=""50"" height=""50"" fill=""black""/>
            <rect x=""70"" y=""70"" width=""60"" height=""60"" fill=""black""/>
            <text x=""100"" y=""190"" text-anchor=""middle"" font-size=""10"" fill=""gray"">Scan to join</text>
        </svg>";

        var bytes = Encoding.UTF8.GetBytes(svg);
        return $"data:image/svg+xml;base64,{Convert.ToBase64String(bytes)}";
    }
}

// ============================================================================
// Join Session By Invite Handler
// ============================================================================

public sealed class JoinSessionByInviteCommandHandler : IRequestHandler<JoinSessionByInviteCommand, JoinSessionResponse>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<JoinSessionByInviteCommandHandler> _logger;

    public JoinSessionByInviteCommandHandler(
        ISessionRepository sessionRepository,
        ILogger<JoinSessionByInviteCommandHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _logger = logger;
    }

    public async Task<JoinSessionResponse> Handle(JoinSessionByInviteCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByInviteTokenAsync(request.InviteToken, cancellationToken).ConfigureAwait(false);
        if (session == null)
        {
            throw new NotFoundException("Invalid or expired invite link.");
        }

        // Validate invite token
        if (!session.IsInviteTokenValid(request.InviteToken))
        {
            throw new ConflictException("Invite link has expired.");
        }

        // Check if session can accept new participants
        if (session.Status == SessionStatus.Finalized)
        {
            throw new ConflictException("Cannot join a finalized session.");
        }

        // Check if user is already a participant
        if (session.Participants.Any(p => p.UserId == request.UserId))
        {
            var existingParticipant = session.Participants.First(p => p.UserId == request.UserId);
            _logger.LogInformation(
                "User {UserId} is already a participant in session {SessionId}",
                request.UserId,
                session.Id);

            return new JoinSessionResponse(
                SessionId: session.Id,
                SessionCode: session.SessionCode,
                ParticipantId: existingParticipant.Id,
                DisplayName: existingParticipant.DisplayName,
                JoinOrder: existingParticipant.JoinOrder
            );
        }

        // Add participant
        var participantInfo = ParticipantInfo.Create(
            displayName: request.DisplayName,
            isOwner: false,
            joinOrder: session.Participants.Count + 1
        );

        session.AddParticipant(participantInfo, request.UserId);

        // Save changes
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        var newParticipant = session.Participants.Last();

        _logger.LogInformation(
            "User {UserId} joined session {SessionId} via invite as participant {ParticipantId}",
            request.UserId,
            session.Id,
            newParticipant.Id);

        return new JoinSessionResponse(
            SessionId: session.Id,
            SessionCode: session.SessionCode,
            ParticipantId: newParticipant.Id,
            DisplayName: newParticipant.DisplayName,
            JoinOrder: newParticipant.JoinOrder
        );
    }
}
