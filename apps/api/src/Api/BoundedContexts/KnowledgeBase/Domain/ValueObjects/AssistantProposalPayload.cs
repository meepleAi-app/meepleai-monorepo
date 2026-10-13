#pragma warning disable MA0048 // File name must match type name - the three jsonb documents of a proposal
namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>Ciò che la proposta scriverebbe. Per <c>AdvanceTurn</c> tutti i campi sono nulli.</summary>
public sealed record AssistantProposalPayload(Guid? PlayerId, string? PlayerName, string? Dimension, int? Round, int? Points);

/// <summary>
/// Lo stato della partita su cui la proposta è nata: il valore della cella per <c>Score</c>, l'indice di
/// turno per <c>AdvanceTurn</c>. Se alla conferma è cambiato, la proposta è superata.
/// </summary>
public sealed record AssistantProposalExpectation(int? CellValue, int? TurnIndex);

/// <summary>L'incremento che l'host ha confermato, quando ha corretto quello proposto.</summary>
public sealed record AssistantProposalConfirmation(int Points);
