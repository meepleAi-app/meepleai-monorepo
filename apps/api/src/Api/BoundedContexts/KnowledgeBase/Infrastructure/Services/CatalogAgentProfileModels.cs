using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// <see cref="IAgentProfileModelCatalogReader"/> letto dal catalogo <c>AiModelConfiguration</c>, senza
/// istantanea come <see cref="CatalogToolCallingModels"/>: si pubblica di rado, e un prezzo impostato
/// un attimo prima deve contare. «Con un prezzo» vale come per <see cref="CatalogModelPriceList"/>: un
/// modello che il registro dei costi sa prezzare, cioè in catalogo e in dollari. Un prezzo zero è un
/// prezzo, quello dei modelli gratuiti.
/// <para>
/// Il registro dei costi invece legge un'istantanea che si ricarica ogni minuto: se l'admin imposta
/// un prezzo e pubblica subito, per al più un minuto le risposte del modello nuovo possono finire nel
/// registro come «senza prezzo», con l'errore nei log e il contatore. La pubblicazione non aspetta
/// l'istantanea: il prezzo c'è, e la finestra è quella di ogni altra modifica del catalogo.
/// </para>
/// </summary>
internal sealed class CatalogAgentProfileModels(IAiModelConfigurationRepository repository) : IAgentProfileModelCatalogReader
{
    public async Task<AgentProfileModelCatalog> ReadAsync(CancellationToken cancellationToken)
    {
        var models = await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var priced = models
            .Where(m => string.Equals(m.Settings.Pricing.Currency, CatalogModelPriceList.PricingCurrency, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.ModelId)
            .ToHashSet(StringComparer.Ordinal);
        var toolCalling = models
            .Where(m => m.Settings.SupportsToolCalling)
            .Select(m => m.ModelId)
            .ToHashSet(StringComparer.Ordinal);
        return new AgentProfileModelCatalog(priced, toolCalling);
    }
}
