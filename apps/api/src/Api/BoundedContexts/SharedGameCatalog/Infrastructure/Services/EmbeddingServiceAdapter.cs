using Api.BoundedContexts.SharedGameCatalog.Domain.Services;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Infrastructure adapter that fulfills the bounded-context-scoped
/// <see cref="IEmbeddingService"/> contract by delegating to the application-wide
/// <see cref="Api.Services.IEmbeddingService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Introduced as part of ADR-051 Sprint 1 (AI comprehension validation). The
/// SharedGameCatalog domain owns its own narrow embedding abstraction so that
/// matching/validation handlers never depend on the broader Api.Services surface
/// (multi-language embeddings, model metadata, batch APIs). This adapter provides
/// the single binding required by the DI container.
/// </para>
/// <para>
/// On failure from the underlying service, the adapter throws
/// <see cref="InvalidOperationException"/> with the upstream error message so the
/// command handler can surface the failure through the standard MediatR pipeline.
/// </para>
/// </remarks>
internal sealed class EmbeddingServiceAdapter : IEmbeddingService
{
    private readonly Api.Services.IEmbeddingService _inner;

    public EmbeddingServiceAdapter(Api.Services.IEmbeddingService inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var result = await _inner.GenerateEmbeddingAsync(text, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Embedding generation failed: {result.ErrorMessage ?? "unknown error"}");
        }

        if (result.Embeddings.Count == 0)
        {
            throw new InvalidOperationException(
                "Embedding generation returned no vectors for the requested text.");
        }

        return result.Embeddings[0];
    }
}
