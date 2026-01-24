namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for game setup checklist with optional wizard mode.
/// </summary>
internal record ChecklistDto(
    Guid GameId,
    ChecklistItemDto[] Items,
    WizardStepDto[]? WizardSteps = null
);

/// <summary>
/// DTO for a single checklist item.
/// </summary>
internal record ChecklistItemDto(
    string Description,
    int Order,
    string? AdditionalInfo = null
);

/// <summary>
/// DTO for wizard step (progressive disclosure).
/// </summary>
internal record WizardStepDto(
    int Step,
    string Title,
    string Description,
    bool IsSkippable
);
