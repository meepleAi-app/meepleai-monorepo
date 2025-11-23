using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for ProvideAgentFeedbackCommand.
/// Records user feedback for agent responses to track effectiveness.
/// </summary>
public sealed class ProvideAgentFeedbackCommandHandler : IRequestHandler<ProvideAgentFeedbackCommand>
{
    private static readonly HashSet<string> ValidOutcomes = new()
    {
        "helpful",
        "not-helpful",
        "incorrect"
    };

    private readonly MeepleAiDbContext _db;
    private readonly ILogger<ProvideAgentFeedbackCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public ProvideAgentFeedbackCommandHandler(
        MeepleAiDbContext db,
        ILogger<ProvideAgentFeedbackCommandHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task Handle(ProvideAgentFeedbackCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MessageId))
        {
            throw new ArgumentException("messageId is required", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Endpoint))
        {
            throw new ArgumentException("endpoint is required", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new ArgumentException("userId is required", nameof(request));
        }

        // Validate outcome value if provided
        if (!string.IsNullOrWhiteSpace(request.Outcome) && !ValidOutcomes.Contains(request.Outcome))
        {
            throw new ArgumentException(
                $"Invalid outcome '{request.Outcome}'. Must be one of: {string.Join(", ", ValidOutcomes)}",
                nameof(request));
        }

        try
        {
            var userGuid = Guid.Parse(request.UserId);
            var messageGuid = Guid.Parse(request.MessageId);
            var existing = await _db.AgentFeedbacks
                .FirstOrDefaultAsync(f => f.MessageId == messageGuid && f.UserId == userGuid, cancellationToken);

            // If outcome is null/empty, remove existing feedback
            if (string.IsNullOrWhiteSpace(request.Outcome))
            {
                if (existing != null)
                {
                    _db.AgentFeedbacks.Remove(existing);
                    await _db.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "Removed agent feedback for message {MessageId} by user {UserId}",
                        request.MessageId,
                        request.UserId);
                }

                return;
            }

            // Create or update feedback
            if (existing == null)
            {
                var entity = new AgentFeedbackEntity
                {
                    MessageId = messageGuid,
                    Endpoint = request.Endpoint,
                    GameId = !string.IsNullOrWhiteSpace(request.GameId) && Guid.TryParse(request.GameId, out var gameGuid)
                        ? gameGuid
                        : null,
                    UserId = userGuid,
                    Outcome = request.Outcome,
                    CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
                    UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime
                };

                _db.AgentFeedbacks.Add(entity);

                _logger.LogInformation(
                    "Created agent feedback for message {MessageId}: {Outcome}",
                    request.MessageId,
                    request.Outcome);
            }
            else
            {
                existing.Endpoint = request.Endpoint;
                existing.GameId = !string.IsNullOrWhiteSpace(request.GameId) && Guid.TryParse(request.GameId, out var gameGuid)
                    ? gameGuid
                    : null;
                existing.Outcome = request.Outcome;
                existing.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

                _logger.LogInformation(
                    "Updated agent feedback for message {MessageId}: {Outcome}",
                    request.MessageId,
                    request.Outcome);
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: CQRS handler boundary - log and rethrow for caller handling
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to record feedback for message {MessageId}",
                request.MessageId);
            throw;
        }
#pragma warning restore CA1031
    }
}
