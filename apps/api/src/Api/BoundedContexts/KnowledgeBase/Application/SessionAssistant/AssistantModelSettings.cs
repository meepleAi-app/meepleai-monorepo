using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>Modello, riserva e limiti di un turno dell'assistente in partita (spec §3.2).</summary>
/// <remarks>
/// <c>MaxToolIterations</c> conta i giri <b>con strumenti</b>. Esauriti quelli, M.E.AI fa una chiamata
/// finale senza strumenti che obbliga il modello a rispondere: un turno costa al più
/// <c>MaxToolIterations + 1</c> chiamate al gateway (fissato da
/// <c>AssistantToolInvocationTests.Turn_ModelThatNeverStops_MakesNToolRoundsPlusOneFinalCallWithoutTools</c>).
/// </remarks>
internal sealed record AssistantModelSettings(
    string PrimaryModelId,
    string? FallbackModelId,
    double Temperature,
    int MaxTokens,
    int MaxToolIterations,
    Guid? ProfileVersionId,
    RequestSource Source);
