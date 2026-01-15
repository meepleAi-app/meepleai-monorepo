#pragma warning disable MA0002 // Dictionary without StringComparer
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handles parsing of ledger messages for state change extraction.
/// Issue #2405 - Ledger Mode state tracking
/// </summary>
internal sealed class ParseLedgerMessageCommandHandler
    : IRequestHandler<ParseLedgerMessageCommand, LedgerParseResultDto>
{
    private readonly IGameSessionStateRepository _sessionStateRepository;
    private readonly IStateParser _stateParser;
    private readonly ILogger<ParseLedgerMessageCommandHandler> _logger;

    public ParseLedgerMessageCommandHandler(
        IGameSessionStateRepository sessionStateRepository,
        IStateParser stateParser,
        ILogger<ParseLedgerMessageCommandHandler> logger)
    {
        _sessionStateRepository = sessionStateRepository;
        _stateParser = stateParser;
        _logger = logger;
    }

    public async Task<LedgerParseResultDto> Handle(
        ParseLedgerMessageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Parsing ledger message for session {SessionId}: {Message}",
            command.SessionId,
            command.Message);

        // Get current game state
        var sessionState = await _sessionStateRepository
            .GetBySessionIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (sessionState == null)
        {
            throw new InvalidOperationException(
                $"Game session {command.SessionId} not found");
        }

        // Parse message
        var extraction = await _stateParser.ParseAsync(
            command.Message,
            sessionState.CurrentState,
            cancellationToken)
            .ConfigureAwait(false);

        // Detect conflicts
        var conflicts = await _stateParser.DetectConflictsAsync(
            extraction,
            sessionState.CurrentState,
            cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug(
            "Parsing completed: ChangeType={ChangeType}, Confidence={Confidence}, Conflicts={ConflictCount}",
            extraction.ChangeType,
            extraction.Confidence,
            conflicts.Count);

        // Map to DTO
        return new LedgerParseResultDto
        {
            ChangeType = extraction.ChangeType.ToString(),
            PlayerName = extraction.PlayerName,
            ExtractedState = extraction.ExtractedState.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Confidence = extraction.Confidence,
            OriginalMessage = extraction.OriginalMessage,
            RequiresConfirmation = extraction.RequiresConfirmation,
            Warnings = extraction.Warnings.ToList(),
            Conflicts = conflicts.Select(c => new StateConflictDto
            {
                PropertyName = c.PropertyName,
                PlayerName = c.PlayerName,
                ExistingValue = c.ExistingValue,
                NewValue = c.NewValue,
                Severity = c.Severity.ToString(),
                SuggestedResolution = c.SuggestedResolution.ToString(),
                FormattedMessage = c.FormatForDisplay()
            }).ToList()
        };
    }
}
