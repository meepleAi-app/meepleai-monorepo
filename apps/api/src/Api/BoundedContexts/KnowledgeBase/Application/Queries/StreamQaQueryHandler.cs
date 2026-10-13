using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// La chat in streaming (<c>POST /agents/qa/stream</c>). #4168: tutta la risposta sta nella pipeline
/// unica (ADR-095 D3), che legge il profilo pubblicato; qui resta solo la traduzione della query.
/// </summary>
internal sealed class StreamQaQueryHandler : IStreamingQueryHandler<StreamQaQuery, RagStreamingEvent>
{
    private readonly IAnswerPipeline _pipeline;

    public StreamQaQueryHandler(IAnswerPipeline pipeline)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    }

    public IAsyncEnumerable<RagStreamingEvent> Handle(StreamQaQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _pipeline.StreamAsync(
            new AnswerRequest(
                GameId: query.GameId,
                Query: query.Query,
                UserId: query.UserId,
                UserRole: query.UserRole,
                AcceptLanguage: query.AcceptLanguage,
                ThreadId: query.ThreadId,
                DocumentIds: query.DocumentIds,
                ResponseStyle: query.ResponseStyle,
                ContinuationContext: query.ContinuationContext),
            cancellationToken);
    }
}
