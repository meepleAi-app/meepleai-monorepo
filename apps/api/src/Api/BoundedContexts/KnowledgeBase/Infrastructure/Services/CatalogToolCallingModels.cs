using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// <see cref="IToolCallingModelCatalog"/> letto dal catalogo <c>AiModelConfiguration</c>, come
/// <see cref="CatalogModelPriceList"/> legge i prezzi. Senza istantanea: è una lettura per turno, su una
/// tabella di poche righe, e la risposta deve seguire subito una modifica del catalogo.
/// </summary>
internal sealed class CatalogToolCallingModels(IAiModelConfigurationRepository repository) : IToolCallingModelCatalog
{
    public async Task<IReadOnlySet<string>> GetToolCallingModelIdsAsync(CancellationToken cancellationToken)
    {
        var models = await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return models
            .Where(m => m.Settings.SupportsToolCalling)
            .Select(m => m.ModelId)
            .ToHashSet(StringComparer.Ordinal);
    }
}
