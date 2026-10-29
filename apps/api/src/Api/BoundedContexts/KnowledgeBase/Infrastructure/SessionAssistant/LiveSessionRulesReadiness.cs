using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// <see cref="IAssistantRulesReadiness"/> sopra la definizione di «regolamento pronto» che il resto del prodotto
/// già usa: <see cref="GetKbReadinessQuery"/>, la
/// stessa del controllo alla creazione di una sessione (almeno un PDF <c>Ready</c> con un indice
/// <c>completed</c>). Un avviso del pannello e un 422 dell'avvio non devono contraddirsi.
/// </summary>
/// <remarks>
/// Quella definizione è calcolata per i giochi del catalogo. Per un gioco privato non si dice niente (<c>null</c>):
/// non è stabilito che l'assistente ne possa consultare il regolamento (#4244), e un avviso sbagliato è peggio
/// di nessun avviso.
/// </remarks>
internal sealed class LiveSessionRulesReadiness(IMediator mediator, MeepleAiDbContext db) : IAssistantRulesReadiness
{
    public async Task<bool?> HasIndexedRulesAsync(Guid? gameId, CancellationToken cancellationToken)
    {
        if (gameId is not { } id)
        {
            return null;
        }

        var isCatalogGame = await db.SharedGames
            .AsNoTracking()
            .AnyAsync(g => g.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (!isCatalogGame)
        {
            return null;
        }

        var readiness = await mediator.Send(new GetKbReadinessQuery(id), cancellationToken).ConfigureAwait(false);
        return readiness.IsReady;
    }
}
