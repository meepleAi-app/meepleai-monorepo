namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.GetIndexerVersionRegistry;

/// <summary>
/// Public projection of an <see cref="Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects.IndexerVersion"/>.
/// </summary>
public sealed record IndexerVersionDto(string Version, string DisplayName, bool IsCurrent);
