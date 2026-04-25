namespace Api.BoundedContexts.SharedGameCatalog.Domain.Services;

/// <summary>
/// Domain-scoped embedding abstraction. Infrastructure provides an adapter
/// wrapping Api.Services.IEmbeddingService.
/// </summary>
public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
}
