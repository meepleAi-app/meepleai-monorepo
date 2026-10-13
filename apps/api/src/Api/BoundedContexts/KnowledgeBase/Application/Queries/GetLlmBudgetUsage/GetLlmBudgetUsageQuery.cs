using Api.Services;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetLlmBudgetUsage;

/// <summary>
/// #4169 (ADR-095 D8, pagina «Budget e quote»): il consumo del budget LLM come lo vede il gate.
/// </summary>
internal sealed record GetLlmBudgetUsageQuery : IQuery<LlmBudgetUsageDto>;

/// <summary>
/// Il consumo del giorno e del mese. <see cref="Available"/> è falso quando la spesa non si può
/// leggere: in quel caso il gate rifiuta le chiamate a pagamento, e la pagina lo deve dire.
/// </summary>
internal sealed record LlmBudgetUsageDto(
    bool Available,
    DateOnly? Day,
    LlmBudgetSpend? Today,
    LlmBudgetSpend? Month,
    bool IsDegraded);
