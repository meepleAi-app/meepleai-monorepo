namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools;

/// <summary>Una proposta salvata: non è ancora registrata, lo diventa con la conferma dell'host.</summary>
internal sealed record ProposalCreated(Guid ProposalId, string Summary, string Note);
