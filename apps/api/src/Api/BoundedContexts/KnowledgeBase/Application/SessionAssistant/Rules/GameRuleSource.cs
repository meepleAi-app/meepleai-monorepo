using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.Middleware.Exceptions;

#pragma warning disable MA0048 // File name must match type name - the production rule source and its factory
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Rules;

/// <summary>
/// <c>cerca_regole</c> in produzione (spec §4.2). L'accesso lo decide il passo condiviso della pipeline,
/// <see cref="IAnswerPreconditions"/>, con l'identità di chi chiede, a ogni ricerca. Il recupero e il tier delle
/// citazioni sono quelli della chat sulle regole (<see cref="ITieredPassageSearch"/>, E1).
/// </summary>
internal sealed class GameRuleSource(IAnswerPreconditions preconditions, ITieredPassageSearch passages, AssistantRuleScope scope)
    : IAssistantRuleSource
{
    public async Task<AssistantRuleSearch> SearchAsync(string question, CancellationToken cancellationToken)
    {
        try
        {
            await preconditions.EnsureCanAskAboutGameAsync(scope.UserId, scope.UserRole, scope.GameId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RagOwnershipRequiredException)
        {
            // Non è un guasto: l'host non ha dichiarato di possedere il gioco. Lo strumento lo dice al modello,
            // che lo spiega all'host invece di rispondere a memoria.
            return AssistantRuleSearch.NotAccessible;
        }

        var found = await passages.SearchAsync(
            new AnswerRetrievalRequest(scope.GameId, question), scope.Profile, scope.Language, scope.UserId, cancellationToken)
            .ConfigureAwait(false);
        return new AssistantRuleSearch(
            found.Citations.Select(c => new AssistantRulePassage(c.SnippetPreview, c.PageNumber)).ToList(),
            found.Citations);
    }
}

internal sealed class AssistantRuleSourceFactory(IAnswerPreconditions preconditions, ITieredPassageSearch passages)
    : IAssistantRuleSourceFactory
{
    public IAssistantRuleSource Create(AssistantRuleScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return new GameRuleSource(preconditions, passages, scope);
    }
}
