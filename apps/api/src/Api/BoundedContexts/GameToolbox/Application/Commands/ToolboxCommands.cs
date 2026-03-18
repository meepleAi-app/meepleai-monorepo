#pragma warning disable MA0048 // File name must match type name - Contains related commands
using Api.BoundedContexts.GameToolbox.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.GameToolbox.Application.Commands;

// === Toolbox CRUD ===

internal record CreateToolboxCommand(
    string Name,
    Guid? GameId,
    string Mode = "Freeform"
) : ICommand<ToolboxDto>;

internal record UpdateToolboxModeCommand(
    Guid ToolboxId,
    string Mode
) : ICommand<ToolboxDto>;

// === Tool Management ===

internal record AddToolToToolboxCommand(
    Guid ToolboxId,
    string Type,
    string Config = "{}"
) : ICommand<ToolboxToolDto>;

internal record RemoveToolFromToolboxCommand(
    Guid ToolboxId,
    Guid ToolId
) : ICommand<Unit>;

internal record ReorderToolsCommand(
    Guid ToolboxId,
    List<Guid> OrderedToolIds
) : ICommand<Unit>;

// === Shared Context ===

internal record UpdateSharedContextCommand(
    Guid ToolboxId,
    List<PlayerInfoDto> Players,
    int CurrentPlayerIndex,
    int CurrentRound,
    Dictionary<string, string> CustomProperties
) : ICommand<SharedContextDto>;

// === Phases ===

internal record AddPhaseCommand(
    Guid ToolboxId,
    string Name,
    List<Guid>? ActiveToolIds = null
) : ICommand<PhaseDto>;

internal record RemovePhaseCommand(
    Guid ToolboxId,
    Guid PhaseId
) : ICommand<Unit>;

internal record ReorderPhasesCommand(
    Guid ToolboxId,
    List<Guid> OrderedPhaseIds
) : ICommand<Unit>;

internal record AdvancePhaseCommand(
    Guid ToolboxId
) : ICommand<PhaseDto>;

// === Card Deck ===

internal record CreateCardDeckCommand(
    Guid ToolboxId,
    string Name,
    string DeckType = "Standard52",
    List<CardEntryRequest>? CustomCards = null
) : ICommand<ToolboxToolDto>;

internal record ShuffleCardDeckCommand(
    Guid ToolboxId,
    Guid DeckId
) : ICommand<Unit>;

internal record DrawCardsCommand(
    Guid ToolboxId,
    Guid DeckId,
    int Count = 1
) : ICommand<CardDrawResultDto>;

internal record ResetCardDeckCommand(
    Guid ToolboxId,
    Guid DeckId
) : ICommand<Unit>;

// === Templates ===

internal record CreateToolboxTemplateCommand(
    Guid ToolboxId,
    string Name,
    Guid? GameId = null
) : ICommand<ToolboxTemplateDto>;

internal record ApplyToolboxTemplateCommand(
    Guid TemplateId,
    Guid? GameId = null
) : ICommand<ToolboxDto>;
