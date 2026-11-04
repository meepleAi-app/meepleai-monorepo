using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Api.Infrastructure.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.CrossGameStreamQa;

/// <summary>
/// La domanda su tutti i giochi accessibili (`POST /knowledge-base/ask/global`).
/// #4168: lingua della risposta, modello e numero di passaggi li decide la pipeline dal profilo
/// pubblicato. `AgentLanguage` è la lingua che il client chiede, se la chiede: vale quando la
/// domanda è troppo breve per rilevarla. Il `TopK` della richiesta non c'è più: lo decide il profilo.
/// </summary>
internal record CrossGameStreamQaQuery(
    string Query,
    Guid UserId,
    UserRole Role,
    string? AgentLanguage = null,
    string? AcceptLanguage = null
) : IStreamingQuery<RagStreamingEvent>;
