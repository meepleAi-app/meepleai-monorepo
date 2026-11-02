using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Moq;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4819: i doppi di <see cref="ICitedPageTrust"/> per i test degli handler che citano una pagina. Il predefinito
/// crede a ogni pagina, cioè il comportamento che quegli handler avevano prima di #4819.
/// </summary>
internal static class CitedPageTrustTestDoubles
{
    /// <summary>Nessun documento è privo di pagina: ogni citazione tiene la sua.</summary>
    public static ICitedPageTrust AllPagesBelieved() => WithoutAPage();

    /// <summary>
    /// I documenti dati non sanno dire la pagina dei loro passaggi; per gli altri vale quella dichiarata. Come il
    /// servizio vero, riporta solo i documenti che gli si CHIEDONO: un handler che chiedesse dell'id sbagliato (quello
    /// vettoriale invece di quello del PDF) non vedrebbe mai il documento segnalato.
    /// </summary>
    public static ICitedPageTrust WithoutAPage(params string[] documentIds)
    {
        var flagged = new HashSet<string>(documentIds, StringComparer.OrdinalIgnoreCase);
        var trust = new Mock<ICitedPageTrust>();
        trust
            .Setup(t => t.WithoutAPageAsync(It.IsAny<IEnumerable<string?>>(), It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<string?>, CancellationToken>((asked, _) =>
                Task.FromResult<IReadOnlySet<string>>(
                    asked
                        .Where(id => id is not null && flagged.Contains(id))
                        .Select(id => id!)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)));
        return trust.Object;
    }
}
