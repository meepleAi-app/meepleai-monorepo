using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.ToolState;
using Api.BoundedContexts.GameManagement.Application.DTOs.ToolState;
using Api.BoundedContexts.GameManagement.Domain.Entities.ToolState;
using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.ToolState;

/// <summary>
/// Initializes all tool states for a session from its linked toolkit.
/// Reads the toolkit's tool configs and creates a ToolState entity for each.
/// Issue #4754: ToolState Entity + Toolkit ↔ Session Integration.
/// </summary>
internal class InitializeToolStatesCommandHandler
    : ICommandHandler<InitializeToolStatesCommand, IReadOnlyList<ToolStateDto>>
{
    private readonly IToolStateRepository _toolStateRepository;
    private readonly IGameToolkitRepository _toolkitRepository;
    private readonly IUnitOfWork _unitOfWork;

    public InitializeToolStatesCommandHandler(
        IToolStateRepository toolStateRepository,
        IGameToolkitRepository toolkitRepository,
        IUnitOfWork unitOfWork)
    {
        _toolStateRepository = toolStateRepository ?? throw new ArgumentNullException(nameof(toolStateRepository));
        _toolkitRepository = toolkitRepository ?? throw new ArgumentNullException(nameof(toolkitRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<IReadOnlyList<ToolStateDto>> Handle(
        InitializeToolStatesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _toolkitRepository.GetByIdAsync(command.ToolkitId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        // Check if tool states already exist for this session
        var existing = await _toolStateRepository.GetBySessionIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false);
        if (existing.Count > 0)
            throw new ConflictException("ToolStates already initialized for this session");

        var toolStates = new List<Domain.Entities.ToolState.ToolState>();

        // Create ToolState for each dice tool
        foreach (var dice in toolkit.DiceTools)
        {
            var initialState = CreateInitialDiceState(dice);
            toolStates.Add(new Domain.Entities.ToolState.ToolState(
                Guid.NewGuid(), command.SessionId, command.ToolkitId,
                dice.Name, ToolType.Dice, initialState));
        }

        // Create ToolState for each counter tool
        foreach (var counter in toolkit.CounterTools)
        {
            var initialState = CreateInitialCounterState(counter);
            toolStates.Add(new Domain.Entities.ToolState.ToolState(
                Guid.NewGuid(), command.SessionId, command.ToolkitId,
                counter.Name, ToolType.Counter, initialState));
        }

        // Create ToolState for each card tool
        foreach (var card in toolkit.CardTools)
        {
            var initialState = CreateInitialCardState(card);
            toolStates.Add(new Domain.Entities.ToolState.ToolState(
                Guid.NewGuid(), command.SessionId, command.ToolkitId,
                card.Name, ToolType.Card, initialState));
        }

        // Create ToolState for each timer tool
        foreach (var timer in toolkit.TimerTools)
        {
            var initialState = CreateInitialTimerState(timer);
            toolStates.Add(new Domain.Entities.ToolState.ToolState(
                Guid.NewGuid(), command.SessionId, command.ToolkitId,
                timer.Name, ToolType.Timer, initialState));
        }

        if (toolStates.Count > 0)
        {
            await _toolStateRepository.AddRangeAsync(toolStates, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return toolStates.Select(ToolStateMapper.ToDto).ToList();
    }

    private static string CreateInitialDiceState(DiceToolConfig dice)
    {
        var state = new
        {
            diceType = dice.DiceType.ToString(),
            quantity = dice.Quantity,
            customFaces = dice.CustomFaces,
            lastRoll = (int[]?)null,
            rollHistory = Array.Empty<object>()
        };
        return JsonSerializer.Serialize(state);
    }

    private static string CreateInitialCounterState(CounterToolConfig counter)
    {
        var state = new
        {
            minValue = counter.MinValue,
            maxValue = counter.MaxValue,
            defaultValue = counter.DefaultValue,
            isPerPlayer = counter.IsPerPlayer,
            currentValue = counter.DefaultValue,
            playerValues = new Dictionary<string, int>(StringComparer.Ordinal)
        };
        return JsonSerializer.Serialize(state);
    }

    private static string CreateInitialCardState(CardToolConfig card)
    {
        var state = new
        {
            deckType = card.DeckType,
            totalCards = card.CardCount,
            shuffleable = card.Shuffleable,
            drawPile = card.CardCount,
            discardPile = 0,
            drawnCards = Array.Empty<string>()
        };
        return JsonSerializer.Serialize(state);
    }

    private static string CreateInitialTimerState(TimerToolConfig timer)
    {
        var state = new
        {
            durationSeconds = timer.DurationSeconds,
            timerType = timer.TimerType,
            autoStart = timer.AutoStart,
            isPerPlayer = timer.IsPerPlayer,
            warningThresholdSeconds = timer.WarningThresholdSeconds,
            remainingSeconds = timer.DurationSeconds,
            isRunning = false,
            startedAt = (DateTime?)null
        };
        return JsonSerializer.Serialize(state);
    }
}

/// <summary>
/// Rolls dice for a specific dice tool in a session.
/// Generates random values and appends to roll history.
/// Issue #4754: ToolState Entity + Toolkit ↔ Session Integration.
/// </summary>
internal class RollDiceCommandHandler : ICommandHandler<RollDiceCommand, ToolStateDto>
{
    private readonly IToolStateRepository _toolStateRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RollDiceCommandHandler(IToolStateRepository toolStateRepository, IUnitOfWork unitOfWork)
    {
        _toolStateRepository = toolStateRepository ?? throw new ArgumentNullException(nameof(toolStateRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ToolStateDto> Handle(RollDiceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolState = await _toolStateRepository
            .GetBySessionAndToolNameAsync(command.SessionId, command.ToolName, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("ToolState", $"{command.SessionId}:{command.ToolName}");

        if (toolState.ToolType != ToolType.Dice)
            throw new BadRequestException($"Tool '{command.ToolName}' is not a dice tool");

        var currentState = JsonSerializer.Deserialize<JsonElement>(toolState.StateDataJson);
        var diceTypeStr = currentState.GetProperty("diceType").GetString() ?? "D6";
        var quantity = currentState.GetProperty("quantity").GetInt32();

        // Parse dice type and generate rolls
        var maxValue = GetDiceMaxValue(diceTypeStr, currentState);
        var rolls = GenerateRolls(quantity, maxValue);

        // Build updated state
        var rollHistory = new List<JsonElement>();
        if (currentState.TryGetProperty("rollHistory", out var existingHistory)
            && existingHistory.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in existingHistory.EnumerateArray())
                rollHistory.Add(entry);
        }

        // Keep last 50 rolls
        var newEntry = JsonSerializer.SerializeToElement(new
        {
            values = rolls,
            playerId = command.PlayerId,
            rolledAt = DateTime.UtcNow
        });
        rollHistory.Add(newEntry);
        if (rollHistory.Count > 50)
            rollHistory.RemoveRange(0, rollHistory.Count - 50);

        var updatedState = new
        {
            diceType = diceTypeStr,
            quantity,
            customFaces = currentState.TryGetProperty("customFaces", out var cf) && cf.ValueKind != JsonValueKind.Null
                ? JsonSerializer.Deserialize<string[]>(cf.GetRawText())
                : null,
            lastRoll = rolls,
            rollHistory
        };

        toolState.UpdateState(JsonSerializer.Serialize(updatedState));
        await _toolStateRepository.UpdateAsync(toolState, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolStateMapper.ToDto(toolState);
    }

    private static int GetDiceMaxValue(string diceTypeStr, JsonElement state)
    {
        if (Enum.TryParse<DiceType>(diceTypeStr, true, out var diceType) && diceType != DiceType.Custom)
            return (int)diceType;

        // Custom dice - use number of custom faces
        if (state.TryGetProperty("customFaces", out var faces)
            && faces.ValueKind == JsonValueKind.Array
            && faces.GetArrayLength() > 0)
        {
            return faces.GetArrayLength();
        }

        return 6; // Fallback to D6
    }

    private static int[] GenerateRolls(int quantity, int maxValue)
    {
        var rolls = new int[quantity];
        for (var i = 0; i < quantity; i++)
            rolls[i] = Random.Shared.Next(1, maxValue + 1);
        return rolls;
    }
}

/// <summary>
/// Updates a counter tool value for a player in a session.
/// Applies the change delta and records history.
/// Issue #4754: ToolState Entity + Toolkit ↔ Session Integration.
/// </summary>
internal class UpdateCounterCommandHandler : ICommandHandler<UpdateCounterCommand, ToolStateDto>
{
    private readonly IToolStateRepository _toolStateRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCounterCommandHandler(IToolStateRepository toolStateRepository, IUnitOfWork unitOfWork)
    {
        _toolStateRepository = toolStateRepository ?? throw new ArgumentNullException(nameof(toolStateRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ToolStateDto> Handle(UpdateCounterCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolState = await _toolStateRepository
            .GetBySessionAndToolNameAsync(command.SessionId, command.ToolName, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("ToolState", $"{command.SessionId}:{command.ToolName}");

        if (toolState.ToolType != ToolType.Counter)
            throw new BadRequestException($"Tool '{command.ToolName}' is not a counter tool");

        var currentState = JsonSerializer.Deserialize<JsonElement>(toolState.StateDataJson);
        var minValue = currentState.GetProperty("minValue").GetInt32();
        var maxValue = currentState.GetProperty("maxValue").GetInt32();
        var defaultValue = currentState.GetProperty("defaultValue").GetInt32();
        var isPerPlayer = currentState.GetProperty("isPerPlayer").GetBoolean();

        // Deserialize player values
        var playerValues = new Dictionary<string, int>(StringComparer.Ordinal);
        if (currentState.TryGetProperty("playerValues", out var pv)
            && pv.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in pv.EnumerateObject())
                playerValues[prop.Name] = prop.Value.GetInt32();
        }

        int currentValue;
        if (isPerPlayer)
        {
            if (!playerValues.TryGetValue(command.PlayerId, out currentValue))
                currentValue = defaultValue;

            var newValue = Math.Clamp(currentValue + command.Change, minValue, maxValue);
            playerValues[command.PlayerId] = newValue;
        }
        else
        {
            currentValue = currentState.TryGetProperty("currentValue", out var cv)
                ? cv.GetInt32()
                : defaultValue;

            currentValue = Math.Clamp(currentValue + command.Change, minValue, maxValue);
        }

        var updatedState = new
        {
            minValue,
            maxValue,
            defaultValue,
            isPerPlayer,
            currentValue = isPerPlayer ? defaultValue : currentValue,
            playerValues
        };

        toolState.UpdateState(JsonSerializer.Serialize(updatedState));
        await _toolStateRepository.UpdateAsync(toolState, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolStateMapper.ToDto(toolState);
    }
}
