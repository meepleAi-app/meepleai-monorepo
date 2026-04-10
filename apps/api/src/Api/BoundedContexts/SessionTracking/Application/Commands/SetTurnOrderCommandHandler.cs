using System.Security.Cryptography;
using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Session Flow v2.1 — T6.
/// Loads the session via <see cref="ISessionRepository"/>, applies the domain
/// <see cref="Session.SetTurnOrder"/> transition (manual or random), persists,
/// and writes a <c>turn_order_set</c> diary entry with the seed so that Random
/// rolls are fully reproducible from the audit log.
/// </summary>
internal sealed class SetTurnOrderCommandHandler : ICommandHandler<SetTurnOrderCommand, SetTurnOrderResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeepleAiDbContext _db;

    public SetTurnOrderCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        MeepleAiDbContext db)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<SetTurnOrderResult> Handle(SetTurnOrderCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found.");

        if (session.UserId != request.UserId)
        {
            throw new ForbiddenException("Only the session owner can set turn order.");
        }

        IReadOnlyList<Guid> order;
        int? seed;

        if (request.Method == TurnOrderMethod.Manual)
        {
            if (request.ManualOrder is null || request.ManualOrder.Count == 0)
            {
                throw new ArgumentException("ManualOrder is required for Manual method.", nameof(request));
            }

            order = request.ManualOrder;
            seed = null;
        }
        else
        {
            // Random: use cryptographically-stable seed generation + deterministic Fisher-Yates
            // shuffle so the same seed replays the same order during audit. Sort by JoinOrder
            // first so the shuffle input is deterministic regardless of EF load order.
            seed = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
            var shuffled = session.Participants
                .OrderBy(p => p.JoinOrder)
                .Select(p => p.Id)
                .ToList();
            var rng = new Random(seed.Value);
            for (var i = shuffled.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            order = shuffled;
        }

        // Domain validates: non-finalized, non-empty order, IDs are participants,
        // Random requires seed. Throws ConflictException / ArgumentException.
        session.SetTurnOrder(request.Method, order, seed);

        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        // Resolve GameNightId via the link row (if any) so the diary entry is
        // correlated with the cross-game timeline.
        var gameNightId = await _db.GameNightSessions
            .Where(gns => gns.SessionId == session.Id)
            .Select(gns => (Guid?)gns.GameNightEventId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var payload = JsonSerializer.Serialize(new
        {
            method = request.Method.ToString(),
            seed,
            participantIds = order
        });

        _db.SessionEvents.Add(new SessionEventEntity
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            GameNightId = gameNightId,
            EventType = "turn_order_set",
            Timestamp = DateTime.UtcNow,
            Payload = payload,
            CreatedBy = request.UserId,
            Source = "user",
            IsDeleted = false
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SetTurnOrderResult(request.Method.ToString(), seed, order);
    }
}
