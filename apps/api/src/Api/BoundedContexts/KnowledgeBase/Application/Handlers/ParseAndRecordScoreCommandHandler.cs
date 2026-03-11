using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handles parsing natural language messages for score data and optionally recording scores.
/// Flow: Parse → Resolve player → Auto-record if confidence is high enough.
/// </summary>
internal sealed class ParseAndRecordScoreCommandHandler
    : ICommandHandler<ParseAndRecordScoreCommand, ScoreParseResultDto>
{
    private readonly IStateParser _stateParser;
    private readonly IMediator _mediator;
    private readonly IPlayerNameResolutionService _playerNameResolution;

    public ParseAndRecordScoreCommandHandler(
        IStateParser stateParser,
        IMediator mediator,
        IPlayerNameResolutionService playerNameResolution)
    {
        _stateParser = stateParser ?? throw new ArgumentNullException(nameof(stateParser));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _playerNameResolution = playerNameResolution ?? throw new ArgumentNullException(nameof(playerNameResolution));
    }

    public async Task<ScoreParseResultDto> Handle(
        ParseAndRecordScoreCommand command,
        CancellationToken cancellationToken)
    {
        // Step 1: Parse the message with the state parser
        var extraction = await _stateParser.ParseAsync(command.Message, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Step 2: If no state change detected, return unrecognized
        if (extraction.ChangeType == StateChangeType.NoChange)
        {
            return new ScoreParseResultDto
            {
                Status = "unrecognized",
                Confidence = extraction.Confidence,
                Message = "No score information detected in the message."
            };
        }

        // Step 3: Extract score value and dimension from extracted state
        var scoreValue = extraction.ExtractedState.TryGetValue("score", out var scoreObj)
            ? Convert.ToInt32(scoreObj, System.Globalization.CultureInfo.InvariantCulture)
            : (int?)null;

        var dimension = extraction.ExtractedState.TryGetValue("dimension", out var dimObj)
            ? dimObj?.ToString() ?? "points"
            : "points";

        // Step 4: Get session players and resolve the player name
        var playerList = await _mediator.Send(new GetSessionPlayersQuery(command.SessionId), cancellationToken)
            .ConfigureAwait(false);
        var players = playerList
            .Where(p => p.IsActive)
            .ToDictionary(p => p.Id, p => p.DisplayName);

        // Step 5: Determine current round
        var currentRound = await GetCurrentRoundAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false);

        // Step 6: Resolve player
        if (extraction.PlayerName is null or "")
        {
            return new ScoreParseResultDto
            {
                Status = "parsed",
                Dimension = dimension,
                Value = scoreValue,
                Round = currentRound,
                Confidence = extraction.Confidence,
                RequiresConfirmation = true,
                Message = "Score detected but no player name found in the message."
            };
        }

        var resolution = _playerNameResolution.ResolvePlayer(extraction.PlayerName, players);

        // Step 7: Handle ambiguous player
        if (resolution.IsAmbiguous)
        {
            return new ScoreParseResultDto
            {
                Status = "ambiguous",
                PlayerName = extraction.PlayerName,
                Dimension = dimension,
                Value = scoreValue,
                Round = currentRound,
                Confidence = extraction.Confidence,
                RequiresConfirmation = true,
                Message = $"Multiple players match '{extraction.PlayerName}'. Please specify.",
                AmbiguousCandidates = resolution.Candidates.Select(c => c.Name).ToList()
            };
        }

        // Step 8: Handle player not found
        if (!resolution.IsResolved)
        {
            return new ScoreParseResultDto
            {
                Status = "parsed",
                PlayerName = extraction.PlayerName,
                Dimension = dimension,
                Value = scoreValue,
                Round = currentRound,
                Confidence = extraction.Confidence,
                RequiresConfirmation = true,
                Message = $"Player '{extraction.PlayerName}' not found in this session."
            };
        }

        // Step 9: Auto-record if conditions are met
        if (command.AutoRecord && extraction.Confidence >= 0.8f && scoreValue.HasValue)
        {
            await _mediator.Send(new RecordLiveSessionScoreCommand(
                command.SessionId,
                resolution.PlayerId!.Value,
                currentRound,
                dimension,
                scoreValue.Value), cancellationToken).ConfigureAwait(false);

            return new ScoreParseResultDto
            {
                Status = "recorded",
                PlayerName = resolution.ResolvedName,
                PlayerId = resolution.PlayerId,
                Dimension = dimension,
                Value = scoreValue,
                Round = currentRound,
                Confidence = extraction.Confidence,
                RequiresConfirmation = false,
                Message = $"Score recorded: {resolution.ResolvedName} scored {scoreValue} {dimension} in round {currentRound}."
            };
        }

        // Step 10: Return parsed result requiring confirmation
        return new ScoreParseResultDto
        {
            Status = "parsed",
            PlayerName = resolution.ResolvedName,
            PlayerId = resolution.PlayerId,
            Dimension = dimension,
            Value = scoreValue,
            Round = currentRound,
            Confidence = extraction.Confidence,
            RequiresConfirmation = true,
            Message = $"Detected: {resolution.ResolvedName} scored {scoreValue} {dimension}. Please confirm."
        };
    }

    private async Task<int> GetCurrentRoundAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _mediator.Send(new GetLiveSessionQuery(sessionId), cancellationToken)
                .ConfigureAwait(false);
            return session.CurrentTurnIndex > 0 ? session.CurrentTurnIndex : 1;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return 1;
        }
    }
}
