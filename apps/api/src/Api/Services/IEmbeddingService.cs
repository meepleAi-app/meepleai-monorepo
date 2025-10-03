namespace Api.Services;

public interface IEmbeddingService
{
    Task<EmbeddingResult> GenerateEmbeddingsAsync(List<string> texts, CancellationToken ct = default);
    Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
}
