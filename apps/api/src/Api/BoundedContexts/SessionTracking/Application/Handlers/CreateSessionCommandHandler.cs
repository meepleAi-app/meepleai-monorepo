using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Exceptions;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

public class CreateSessionCommandHandler : ICommandHandler<CreateSessionCommand, CreateSessionResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionQuotaService _quotaService;
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<CreateSessionCommandHandler> _logger;

    public CreateSessionCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ISessionQuotaService quotaService,
        MeepleAiDbContext db,
        ILogger<CreateSessionCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CreateSessionResult> Handle(CreateSessionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Check session quota before allowing creation
        await CheckSessionQuotaAsync(request.UserId, cancellationToken).ConfigureAwait(false);

        var sessionType = Enum.Parse<SessionType>(request.SessionType);

        // Create session with retry for unique code
        Session session;
        const int maxRetries = 3;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            session = Session.Create(
                request.UserId,
                request.GameId,
                sessionType,
                request.Location,
                request.SessionDate);

            try
            {
                await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                // Add additional participants (owner already added by factory)
                foreach (var participantDto in request.Participants.Where(p => !p.IsOwner))
                {
                    var participantInfo = ParticipantInfo.Create(
                        participantDto.DisplayName,
                        participantDto.IsOwner,
                        session.Participants.Count + 1);

                    session.AddParticipant(participantInfo, participantDto.UserId);
                }

                if (request.Participants.Count > 1)
                {
                    await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
                    await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                return new CreateSessionResult(
                    session.Id,
                    session.SessionCode,
                    session.Participants.Select(MapParticipant).ToList());
            }
            catch (InvalidOperationException) when (attempt < maxRetries - 1)
            {
                // Session code collision, retry with new code
            }
        }

        throw new ConflictException("Unable to generate unique session code after retries");
    }

    private static ParticipantDto MapParticipant(Participant p) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        DisplayName = p.DisplayName,
        IsOwner = p.IsOwner,
        JoinOrder = p.JoinOrder,
        FinalRank = p.FinalRank,
        TotalScore = 0
    };

    /// <summary>
    /// Checks session quota before allowing a new session to be created.
    /// </summary>
    /// <param name="userId">User ID to check quota for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="DomainException">Thrown if user not found</exception>
    /// <exception cref="SessionQuotaExceededException">Thrown if quota exceeded</exception>
    private async Task CheckSessionQuotaAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Get user tier and role from database
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.Tier, u.Role })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (user == null)
        {
            _logger.LogError("User {UserId} not found during session quota check", userId);
            throw new DomainException($"User with ID {userId} not found");
        }

        var userTier = UserTier.Parse(user.Tier);
        var userRole = Role.Parse(user.Role);

        var quotaResult = await _quotaService.CheckQuotaAsync(
            userId,
            userTier,
            userRole,
            cancellationToken).ConfigureAwait(false);

        if (!quotaResult.IsAllowed)
        {
            _logger.LogWarning(
                "Session quota exceeded for user {UserId} ({Tier}): {CurrentCount}/{MaxAllowed} sessions",
                userId,
                userTier.Value,
                quotaResult.CurrentCount,
                quotaResult.MaxAllowed);

            throw new SessionQuotaExceededException(
                quotaResult.DenialReason ?? "Concurrent session limit reached",
                quotaResult.CurrentCount,
                quotaResult.MaxAllowed);
        }

        _logger.LogDebug(
            "Session quota check passed for user {UserId} ({Tier}): {CurrentCount}/{MaxAllowed} sessions",
            userId,
            userTier.Value,
            quotaResult.CurrentCount,
            quotaResult.MaxAllowed);
    }
}