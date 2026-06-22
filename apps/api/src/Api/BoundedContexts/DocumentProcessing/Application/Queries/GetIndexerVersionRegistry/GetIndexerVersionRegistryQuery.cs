using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.GetIndexerVersionRegistry;

/// <summary>
/// Returns selectable indexer versions for the admin dropdown.
/// Issue #1673.
/// </summary>
internal sealed record GetIndexerVersionRegistryQuery : IQuery<IReadOnlyList<IndexerVersionDto>>;
