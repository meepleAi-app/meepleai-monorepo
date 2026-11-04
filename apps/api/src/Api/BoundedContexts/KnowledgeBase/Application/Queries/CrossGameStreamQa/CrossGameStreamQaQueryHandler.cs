using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.CrossGameStreamQa;

/// <summary>
/// La ricerca della KB globale in streaming. #4168: la risposta sta in
/// <see cref="ICrossGameAnswerFlow"/>, sugli stessi passi della pipeline unica (ADR-095 D3); qui
/// resta solo la traduzione della query.
/// </summary>
internal sealed class CrossGameStreamQaQueryHandler : IStreamingQueryHandler<CrossGameStreamQaQuery, RagStreamingEvent>
{
    private readonly ICrossGameAnswerFlow _flow;

    public CrossGameStreamQaQueryHandler(ICrossGameAnswerFlow flow)
    {
        _flow = flow ?? throw new ArgumentNullException(nameof(flow));
    }

    public IAsyncEnumerable<RagStreamingEvent> Handle(CrossGameStreamQaQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _flow.StreamAsync(
            new CrossGameAnswerRequest(query.Query, query.UserId, query.Role, query.AgentLanguage, query.AcceptLanguage),
            cancellationToken);
    }
}
