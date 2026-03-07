using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameToolkit.Application.Handlers;

internal class CreateToolkitCommandHandler : ICommandHandler<CreateToolkitCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateToolkitCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(CreateToolkitCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = new Domain.Entities.GameToolkit(
            id: Guid.NewGuid(),
            gameId: command.GameId,
            name: command.Name,
            createdByUserId: command.CreatedByUserId,
            privateGameId: command.PrivateGameId,
            overridesTurnOrder: command.OverridesTurnOrder,
            overridesScoreboard: command.OverridesScoreboard,
            overridesDiceSet: command.OverridesDiceSet
        );

        await _repository.AddAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class UpdateToolkitCommandHandler : ICommandHandler<UpdateToolkitCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateToolkitCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(UpdateToolkitCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        toolkit.UpdateDetails(command.Name);

        if (command.OverridesTurnOrder.HasValue || command.OverridesScoreboard.HasValue || command.OverridesDiceSet.HasValue)
        {
            toolkit.UpdateOverrideFlags(
                command.OverridesTurnOrder ?? toolkit.OverridesTurnOrder,
                command.OverridesScoreboard ?? toolkit.OverridesScoreboard,
                command.OverridesDiceSet ?? toolkit.OverridesDiceSet);
        }

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class PublishToolkitCommandHandler : ICommandHandler<PublishToolkitCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public PublishToolkitCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(PublishToolkitCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        toolkit.Publish();

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class AddDiceToolCommandHandler : ICommandHandler<AddDiceToolCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AddDiceToolCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(AddDiceToolCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        var config = new DiceToolConfig(
            name: command.Name,
            diceType: command.DiceType,
            quantity: command.Quantity,
            customFaces: command.CustomFaces,
            isInteractive: command.IsInteractive,
            color: command.Color
        );

        toolkit.AddDiceTool(config);

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class AddCardToolCommandHandler : ICommandHandler<AddCardToolCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AddCardToolCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(AddCardToolCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        var cardEntries = command.CardEntries?
            .Select(e => new CardEntry(e.Name, e.Suit, e.Rank, e.CustomData))
            .ToList();

        var config = new CardToolConfig(
            name: command.Name,
            deckType: command.DeckType,
            cardCount: command.CardCount,
            shuffleable: command.Shuffleable,
            defaultZone: command.DefaultZone,
            defaultOrientation: command.DefaultOrientation,
            cardEntries: cardEntries,
            allowDraw: command.AllowDraw,
            allowDiscard: command.AllowDiscard,
            allowPeek: command.AllowPeek,
            allowReturnToDeck: command.AllowReturnToDeck
        );

        toolkit.AddCardTool(config);

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class AddTimerToolCommandHandler : ICommandHandler<AddTimerToolCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AddTimerToolCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(AddTimerToolCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        var config = new TimerToolConfig(
            name: command.Name,
            durationSeconds: command.DurationSeconds,
            timerType: command.TimerType,
            autoStart: command.AutoStart,
            color: command.Color,
            isPerPlayer: command.IsPerPlayer,
            warningThresholdSeconds: command.WarningThresholdSeconds
        );

        toolkit.AddTimerTool(config);

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class AddCounterToolCommandHandler : ICommandHandler<AddCounterToolCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AddCounterToolCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(AddCounterToolCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        var config = new CounterToolConfig(
            name: command.Name,
            minValue: command.MinValue,
            maxValue: command.MaxValue,
            defaultValue: command.DefaultValue,
            isPerPlayer: command.IsPerPlayer,
            icon: command.Icon,
            color: command.Color
        );

        toolkit.AddCounterTool(config);

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class RemoveDiceToolCommandHandler : ICommandHandler<RemoveDiceToolCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveDiceToolCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(RemoveDiceToolCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        if (!toolkit.RemoveDiceTool(command.ToolName))
            throw new NotFoundException("DiceTool", command.ToolName);

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class RemoveCardToolCommandHandler : ICommandHandler<RemoveCardToolCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveCardToolCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(RemoveCardToolCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        if (!toolkit.RemoveCardTool(command.ToolName))
            throw new NotFoundException("CardTool", command.ToolName);

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class RemoveTimerToolCommandHandler : ICommandHandler<RemoveTimerToolCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveTimerToolCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(RemoveTimerToolCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        if (!toolkit.RemoveTimerTool(command.ToolName))
            throw new NotFoundException("TimerTool", command.ToolName);

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class RemoveCounterToolCommandHandler : ICommandHandler<RemoveCounterToolCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveCounterToolCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(RemoveCounterToolCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        if (!toolkit.RemoveCounterTool(command.ToolName))
            throw new NotFoundException("CounterTool", command.ToolName);

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class SetScoringTemplateCommandHandler : ICommandHandler<SetScoringTemplateCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SetScoringTemplateCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(SetScoringTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        var template = new ScoringTemplateConfig(
            dimensions: command.Dimensions,
            defaultUnit: command.DefaultUnit,
            scoreType: command.ScoreType
        );

        toolkit.SetScoringTemplate(template);

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class SetTurnTemplateCommandHandler : ICommandHandler<SetTurnTemplateCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SetTurnTemplateCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(SetTurnTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        var template = new TurnTemplateConfig(
            turnOrderType: command.TurnOrderType,
            phases: command.Phases
        );

        toolkit.SetTurnTemplate(template);

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class SetStateTemplateCommandHandler : ICommandHandler<SetStateTemplateCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SetStateTemplateCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(SetStateTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        var template = new StateTemplateDefinition(
            name: command.Name,
            category: command.Category,
            schemaJson: command.SchemaJson,
            description: command.Description
        );

        toolkit.SetStateTemplate(template);

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class ClearStateTemplateCommandHandler : ICommandHandler<ClearStateTemplateCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ClearStateTemplateCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(ClearStateTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        toolkit.SetStateTemplate(null);

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

// ========================================================================
// Template marketplace handlers
// ========================================================================

internal class SubmitTemplateForReviewCommandHandler : ICommandHandler<SubmitTemplateForReviewCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitTemplateForReviewCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(SubmitTemplateForReviewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        toolkit.SubmitForReview();

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class ApproveTemplateCommandHandler : ICommandHandler<ApproveTemplateCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveTemplateCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(ApproveTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        toolkit.ApproveTemplate(command.AdminUserId, command.Notes);

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class RejectTemplateCommandHandler : ICommandHandler<RejectTemplateCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RejectTemplateCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(RejectTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        toolkit.RejectTemplate(command.AdminUserId, command.Notes);

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class CloneFromTemplateCommandHandler : ICommandHandler<CloneFromTemplateCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CloneFromTemplateCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(CloneFromTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var template = await _repository.GetByIdAsync(command.TemplateId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit template", command.TemplateId.ToString());

        if (template.TemplateStatus != Domain.Enums.TemplateStatus.Approved)
            throw new Middleware.Exceptions.ConflictException("Can only clone from approved templates.");

        var clone = new Domain.Entities.GameToolkit(
            id: Guid.NewGuid(),
            gameId: command.GameId,
            name: $"{template.Name} (from template)",
            createdByUserId: command.UserId);

        // Copy all tools from template
        foreach (var dice in template.DiceTools)
            clone.AddDiceTool(new DiceToolConfig(dice.Name, dice.DiceType, dice.Quantity, dice.CustomFaces, dice.IsInteractive, dice.Color));
        foreach (var card in template.CardTools)
            clone.AddCardTool(new CardToolConfig(card.Name, card.DeckType, card.CardCount, card.Shuffleable,
                card.DefaultZone, card.DefaultOrientation, card.CardEntries.ToList(),
                card.AllowDraw, card.AllowDiscard, card.AllowPeek, card.AllowReturnToDeck));
        foreach (var timer in template.TimerTools)
            clone.AddTimerTool(new TimerToolConfig(timer.Name, timer.DurationSeconds, timer.TimerType,
                timer.AutoStart, timer.Color, timer.IsPerPlayer, timer.WarningThresholdSeconds));
        foreach (var counter in template.CounterTools)
            clone.AddCounterTool(new CounterToolConfig(counter.Name, counter.MinValue, counter.MaxValue,
                counter.DefaultValue, counter.IsPerPlayer, counter.Icon, counter.Color));

        if (template.ScoringTemplate != null)
            clone.SetScoringTemplate(new ScoringTemplateConfig(
                template.ScoringTemplate.Dimensions, template.ScoringTemplate.DefaultUnit, template.ScoringTemplate.ScoreType));
        if (template.TurnTemplate != null)
            clone.SetTurnTemplate(new TurnTemplateConfig(template.TurnTemplate.TurnOrderType, template.TurnTemplate.Phases));

        await _repository.AddAsync(clone, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(clone);
    }
}
