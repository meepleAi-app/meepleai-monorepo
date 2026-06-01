using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.GetIndexerVersionRegistry;

internal sealed class GetIndexerVersionRegistryHandler
    : IQueryHandler<GetIndexerVersionRegistryQuery, IReadOnlyList<IndexerVersionDto>>
{
    public Task<IReadOnlyList<IndexerVersionDto>> Handle(
        GetIndexerVersionRegistryQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IndexerVersionDto> result = IndexerVersionRegistry.Selectable
            .Select(v => new IndexerVersionDto(
                Version: v.Version,
                DisplayName: v.DisplayName,
                IsCurrent: string.Equals(v.Version, IndexerVersionRegistry.Current.Version, StringComparison.Ordinal)))
            .ToList();
        return Task.FromResult(result);
    }
}
