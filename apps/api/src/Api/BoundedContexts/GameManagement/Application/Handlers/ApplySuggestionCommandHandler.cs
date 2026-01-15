#pragma warning disable MA0002 // Dictionary without StringComparer
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handler for ApplySuggestionCommand - applies a move suggestion to game state.
/// Issue #2404 - Player Mode apply suggestion
/// </summary>
internal sealed class ApplySuggestionCommandHandler
    : IRequestHandler<ApplySuggestionCommand, GameSessionStateDto>
{
    private readonly IGameSessionStateRepository _sessionStateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApplySuggestionCommandHandler> _logger;

    public ApplySuggestionCommandHandler(
        IGameSessionStateRepository sessionStateRepository,
        IUnitOfWork unitOfWork,
        ILogger<ApplySuggestionCommandHandler> logger)
    {
        _sessionStateRepository = sessionStateRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<GameSessionStateDto> Handle(
        ApplySuggestionCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Applying suggestion {SuggestionId} to session {SessionId}",
            request.SuggestionId,
            request.SessionId);

        // 1. Get current game state
        var sessionState = await _sessionStateRepository
            .GetBySessionIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (sessionState == null)
        {
            throw new NotFoundException($"Game session {request.SessionId} not found");
        }

        // 2. Parse current state as dictionary for merging
        var currentStateDict = ParseStateToDict(sessionState.CurrentState);

        // 3. Apply state changes (merge new changes into current state)
        foreach (var (key, value) in request.StateChanges)
        {
            currentStateDict[key] = value;
            _logger.LogDebug("Applied state change: {Key} = {Value}", key, value);
        }

        // 4. Convert back to JsonDocument
        var updatedStateJson = JsonSerializer.Serialize(currentStateDict);
        var updatedState = JsonDocument.Parse(updatedStateJson);

        // 5. Update session state
        sessionState.UpdateState(
            newState: updatedState,
            updatedBy: request.UserId.ToString());

        // 6. Create snapshot after applying changes (for undo capability)
        // Calculate turn number from existing snapshots
        var turnNumber = sessionState.Snapshots.Count + 1;
        sessionState.CreateSnapshot(
            turnNumber: turnNumber,
            description: $"Applied suggestion {request.SuggestionId}",
            createdBy: request.UserId.ToString());

        // 7. Persist changes
        await _sessionStateRepository.UpdateAsync(sessionState, cancellationToken)
            .ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Successfully applied suggestion {SuggestionId} to session {SessionId}, new version {Version}",
            request.SuggestionId,
            request.SessionId,
            sessionState.Version);

        // 8. Return updated state
        return sessionState.ToDto();
    }

    /// <summary>
    /// Parses JsonDocument to Dictionary for state manipulation
    /// </summary>
    private static Dictionary<string, object> ParseStateToDict(JsonDocument jsonDoc)
    {
        var dict = new Dictionary<string, object>();

        if (jsonDoc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return dict;
        }

        foreach (var property in jsonDoc.RootElement.EnumerateObject())
        {
            dict[property.Name] = ParseJsonElement(property.Value);
        }

        return dict;
    }

    /// <summary>
    /// Recursively parses JsonElement to C# object
    /// </summary>
    private static object ParseJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ParseObjectElement(element),
            JsonValueKind.Array => ParseArrayElement(element),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            _ => element.GetRawText()
        };
    }

    private static Dictionary<string, object> ParseObjectElement(JsonElement element)
    {
        var dict = new Dictionary<string, object>();
        foreach (var property in element.EnumerateObject())
        {
            dict[property.Name] = ParseJsonElement(property.Value);
        }
        return dict;
    }

    private static List<object> ParseArrayElement(JsonElement element)
    {
        var list = new List<object>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(ParseJsonElement(item));
        }
        return list;
    }
}
