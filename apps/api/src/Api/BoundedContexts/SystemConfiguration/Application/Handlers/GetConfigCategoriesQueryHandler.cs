using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles retrieval of all distinct configuration categories.
/// </summary>
public class GetConfigCategoriesQueryHandler : IQueryHandler<GetConfigCategoriesQuery, IReadOnlyList<string>>
{
    private readonly IConfigurationRepository _configurationRepository;

    public GetConfigCategoriesQueryHandler(IConfigurationRepository configurationRepository)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
    }

    public async Task<IReadOnlyList<string>> Handle(GetConfigCategoriesQuery query, CancellationToken cancellationToken)
    {
        var allConfigs = await _configurationRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var categories = allConfigs
            .Select(c => c.Category)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        return categories;
    }
}
