namespace Api.BoundedContexts.AgentMemory.Application.DTOs;

/// <summary>
/// DTO representing aggregated counts for the SP6 §F drawer ConnectionBar.
/// Wires the 4 pip counts (agent · game · session · kb) from the house-rule context.
/// See issue #2492 for semantic counts definition.
/// </summary>
internal record HouseRulesMetadataDto(
    int AgentCount,
    int GameCount,
    int SessionCount,
    int KbCount);
