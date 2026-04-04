using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameToolkit.Application.Commands;

internal class ApplyAiToolkitSuggestionHandler : ICommandHandler<ApplyAiToolkitSuggestionCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ApplyAiToolkitSuggestionHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(ApplyAiToolkitSuggestionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var suggestion = command.Suggestion;
        Domain.Entities.GameToolkit toolkit;

        if (command.ToolkitId.HasValue)
        {
            // Update existing toolkit
            toolkit = await _repository.GetByIdAsync(command.ToolkitId.Value, cancellationToken).ConfigureAwait(false)
                ?? throw new NotFoundException("GameToolkit", command.ToolkitId.Value.ToString());

            toolkit.UpdateDetails(suggestion.ToolkitName);
        }
        else
        {
            // Create new toolkit from AI suggestion
            var overrides = suggestion.Overrides;
            toolkit = new Domain.Entities.GameToolkit(
                id: Guid.NewGuid(),
                gameId: command.GameId,
                name: suggestion.ToolkitName,
                createdByUserId: command.UserId,
                overridesTurnOrder: overrides?.OverridesTurnOrder ?? false,
                overridesScoreboard: overrides?.OverridesScoreboard ?? false,
                overridesDiceSet: overrides?.OverridesDiceSet ?? false
            );
        }

        // Apply override flags (for both new and existing toolkits)
        if (suggestion.Overrides is not null)
        {
            toolkit.UpdateOverrideFlags(
                suggestion.Overrides.OverridesTurnOrder,
                suggestion.Overrides.OverridesScoreboard,
                suggestion.Overrides.OverridesDiceSet);
        }

        // Map AI dice tool suggestions to domain configs
        foreach (var dice in suggestion.DiceTools)
        {
            var config = new DiceToolConfig(
                name: dice.Name,
                diceType: dice.DiceType,
                quantity: dice.Quantity,
                customFaces: dice.CustomFaces,
                isInteractive: dice.IsInteractive,
                color: dice.Color
            );
            toolkit.AddDiceTool(config);
        }

        // Map AI counter tool suggestions to domain configs
        foreach (var counter in suggestion.CounterTools)
        {
            var config = new CounterToolConfig(
                name: counter.Name,
                minValue: counter.MinValue,
                maxValue: counter.MaxValue,
                defaultValue: counter.DefaultValue,
                isPerPlayer: counter.IsPerPlayer,
                icon: counter.Icon,
                color: counter.Color
            );
            toolkit.AddCounterTool(config);
        }

        // Map AI timer tool suggestions to domain configs
        foreach (var timer in suggestion.TimerTools)
        {
            var config = new TimerToolConfig(
                name: timer.Name,
                durationSeconds: timer.DurationSeconds,
                timerType: timer.TimerType,
                autoStart: timer.AutoStart,
                color: timer.Color,
                isPerPlayer: timer.IsPerPlayer,
                warningThresholdSeconds: timer.WarningThresholdSeconds
            );
            toolkit.AddTimerTool(config);
        }

        // Set scoring template if suggested
        if (suggestion.ScoringTemplate is not null)
        {
            var scoring = new ScoringTemplateConfig(
                dimensions: suggestion.ScoringTemplate.Dimensions,
                defaultUnit: suggestion.ScoringTemplate.DefaultUnit,
                scoreType: suggestion.ScoringTemplate.ScoreType
            );
            toolkit.SetScoringTemplate(scoring);
        }

        // Set turn template if suggested
        if (suggestion.TurnTemplate is not null)
        {
            var turn = new TurnTemplateConfig(
                turnOrderType: suggestion.TurnTemplate.TurnOrderType,
                phases: suggestion.TurnTemplate.Phases
            );
            toolkit.SetTurnTemplate(turn);
        }

        // Store AI reasoning as agent config for traceability
        toolkit.SetAgentConfig(suggestion.Reasoning);

        // Persist
        if (command.ToolkitId.HasValue)
        {
            await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _repository.AddAsync(toolkit, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}
