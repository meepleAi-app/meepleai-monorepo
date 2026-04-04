#pragma warning disable MA0048 // File name must match type name - Contains related DTOs
namespace Api.BoundedContexts.GameToolbox.Application.DTOs;

// === Response DTOs ===

internal record ToolboxDto(
    Guid Id,
    string Name,
    Guid? GameId,
    string Mode,
    SharedContextDto SharedContext,
    Guid? CurrentPhaseId,
    List<ToolboxToolDto> Tools,
    List<PhaseDto> Phases,
    DateTime CreatedAt,
    DateTime UpdatedAt);

internal record ToolboxToolDto(
    Guid Id,
    string Type,
    string Config,
    string State,
    bool IsEnabled,
    int Order);

internal record PhaseDto(
    Guid Id,
    string Name,
    int Order,
    List<Guid> ActiveToolIds);

internal record SharedContextDto(
    List<PlayerInfoDto> Players,
    int CurrentPlayerIndex,
    int CurrentRound,
    Dictionary<string, string> CustomProperties);

internal record PlayerInfoDto(
    string Name,
    string Color,
    string? AvatarUrl);

internal record ToolboxTemplateDto(
    Guid Id,
    string Name,
    Guid? GameId,
    string Mode,
    string Source,
    string ToolsJson,
    string PhasesJson,
    DateTime CreatedAt);

internal record CardDrawResultDto(
    List<DrawnCardDto> Cards,
    int RemainingInDeck);

internal record DrawnCardDto(
    Guid Id,
    string Name,
    string? Value,
    string? Suit,
    Dictionary<string, string> CustomProperties);

internal record AvailableToolDto(
    string Type,
    string DisplayName,
    string Category); // "Adapted" or "Native"

// === Request DTOs ===

internal record CreateToolboxRequest(
    string Name,
    Guid? GameId,
    string Mode = "Freeform");

internal record AddToolRequest(
    string Type,
    string Config = "{}");

internal record UpdateSharedContextRequest(
    List<PlayerInfoDto> Players,
    int CurrentPlayerIndex = 0,
    int CurrentRound = 1,
    Dictionary<string, string>? CustomProperties = null);

internal record AddPhaseRequest(
    string Name,
    List<Guid>? ActiveToolIds = null);

internal record ReorderRequest(
    List<Guid> OrderedIds);

internal record CreateCardDeckRequest(
    string Name,
    string DeckType = "Standard52",
    List<CardEntryRequest>? CustomCards = null);

internal record CardEntryRequest(
    string Name,
    string? Value = null,
    string? Suit = null,
    Dictionary<string, string>? CustomProperties = null);

internal record CreateToolboxTemplateRequest(
    string Name,
    Guid? GameId = null);

internal record ApplyToolboxTemplateRequest(
    Guid TemplateId,
    Guid? GameId = null);
