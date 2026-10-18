#pragma warning disable MA0048 // File name must match type name - the results the tools return
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools;

/// <summary>Una proposta salvata: non è ancora registrata, lo diventa con la conferma dell'host.</summary>
internal sealed record ProposalCreated(Guid ProposalId, string Summary, string Note);

/// <summary>Ciò che cerca_regole ha trovato, con una nota su come usarlo. Una lista vuota non è un errore (#4411).</summary>
internal sealed record RulePassagesFound(IReadOnlyList<AssistantRulePassage> Passaggi, string Nota);
