#pragma warning disable MA0002 // Dictionary without StringComparer
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handles confirmation and application of state changes from ledger parsing.
/// Issue #2405 - Ledger Mode state tracking
/// </summary>
internal sealed class ConfirmStateChangeCommandHandler
    : IRequestHandler<ConfirmStateChangeCommand, MediatR.Unit>
{
    private readonly IGameSessionStateRepository _sessionStateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConfirmStateChangeCommandHandler> _logger;

    public ConfirmStateChangeCommandHandler(
        IGameSessionStateRepository sessionStateRepository,
        IUnitOfWork unitOfWork,
        ILogger<ConfirmStateChangeCommandHandler> logger)
    {
        _sessionStateRepository = sessionStateRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<MediatR.Unit> Handle(
        ConfirmStateChangeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Confirming state change for session {SessionId} with {ChangeCount} changes",
            command.SessionId,
            command.StateChanges.Count);

        // Get current session state
        var sessionState = await _sessionStateRepository
            .GetBySessionIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (sessionState == null)
        {
            throw new InvalidOperationException(
                $"Game session {command.SessionId} not found");
        }

        // Get current state as dictionary
        var currentStateDict = ParseJsonToDictionary(sessionState.CurrentState);

        // Apply state changes
        foreach (var (key, value) in command.StateChanges)
        {
            currentStateDict[key] = value;
            _logger.LogDebug("Applied change: {Key} = {Value}", key, value);
        }

        // Convert back to JsonDocument
        var json = JsonSerializer.Serialize(currentStateDict);
        var newState = JsonDocument.Parse(json);

        // Update session state
        sessionState.UpdateState(newState, command.UserId.ToString());

        // Create snapshot if description provided
        if (!string.IsNullOrWhiteSpace(command.Description))
        {
            var currentTurn = GetCurrentTurn(currentStateDict);
            sessionState.CreateSnapshot(
                turnNumber: currentTurn,
                description: command.Description,
                createdBy: command.UserId.ToString());

            _logger.LogDebug(
                "Created snapshot for turn {Turn}: {Description}",
                currentTurn,
                command.Description);
        }

        // Persist changes with concurrency check
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Concurrency conflict for session {SessionId}. State was modified by another user.",
                command.SessionId);

            throw new InvalidOperationException(
                "The game state was modified by another user. Please refresh and try again.",
                ex);
        }

        _logger.LogInformation(
            "State change confirmed for session {SessionId}, new version: {Version}",
            command.SessionId,
            sessionState.Version);

        return MediatR.Unit.Value;
    }

    /// <summary>
    /// Parses JsonDocument to Dictionary
    /// </summary>
    private static Dictionary<string, object?> ParseJsonToDictionary(JsonDocument jsonDoc)
    {
        var dict = new Dictionary<string, object?>();

        if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in jsonDoc.RootElement.EnumerateObject())
            {
                dict[property.Name] = ParseJsonValue(property.Value);
            }
        }

        return dict;
    }

    /// <summary>
    /// Parses JsonElement to object
    /// </summary>
    private static object? ParseJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// Gets current turn number from state
    /// </summary>
    private static int GetCurrentTurn(Dictionary<string, object?> state)
    {
        if (state.TryGetValue("turn", out var turnValue) && turnValue is int turn)
        {
            return turn;
        }
        return 1; // Default to turn 1 if not found
    }
}
