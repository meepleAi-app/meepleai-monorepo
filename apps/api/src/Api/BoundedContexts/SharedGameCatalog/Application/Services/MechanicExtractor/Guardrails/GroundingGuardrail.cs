using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;

/// <summary>
/// T3b — semantic grounding. For each claim-bearing object with citations, embeds the claim text
/// and its cited chunk and computes cosine similarity; flags claims below
/// <see cref="Configuration.MechanicGuardrailOptions.MinClaimGroundingSimilarity"/>.
/// Fails closed (blocks the section) if the embedding service is unavailable — IP &gt; latency in M1.
/// </summary>
internal sealed class GroundingGuardrail : IMechanicGuardrail
{
    private readonly IEmbeddingService _embeddings;

    public GroundingGuardrail(IEmbeddingService embeddings) => _embeddings = embeddings;

    public string RuleFamily => "T3";
    public int Order => 40;

    private static readonly string[] ClaimFields = { "claim", "description", "text", "answer", "primary" };

    public async Task<IReadOnlyList<MechanicValidationViolation>> EvaluateAsync(
        MechanicGuardrailContext context, CancellationToken cancellationToken)
    {
        var violations = new List<MechanicValidationViolation>();
        var targets = new List<(string claim, string path, MechanicSourceChunk chunk)>();

        MechanicJsonWalker.ForEachObject(context.Root, "$", (obj, path) =>
        {
            if (obj.ValueKind != JsonValueKind.Object)
            {
                return;
            }
            string? claim = null;
            foreach (var f in ClaimFields)
            {
                if (obj.TryGetProperty(f, out var e) && e.ValueKind == JsonValueKind.String)
                {
                    var s = e.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        claim = s;
                        break;
                    }
                }
            }
            if (claim is null)
            {
                return;
            }
            if (!obj.TryGetProperty("citations", out var cits) || cits.ValueKind != JsonValueKind.Array)
            {
                return;
            }
            foreach (var c in cits.EnumerateArray())
            {
                var chunk = ResolveChunk(c, context.SourceChunks);
                if (chunk != null)
                {
                    targets.Add((claim!, path, chunk));
                }
            }
        });

        foreach (var (claim, path, chunk) in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var a = await _embeddings.EmbedAsync(claim, cancellationToken).ConfigureAwait(false);
                var b = await _embeddings.EmbedAsync(chunk.Content, cancellationToken).ConfigureAwait(false);
                var cos = Cosine(a, b);
                if (cos < context.Options.MinClaimGroundingSimilarity)
                {
                    violations.Add(new MechanicValidationViolation(
                        "T3_grounding",
                        $"claim '{Trunc(claim)}' has cosine {cos:0.00} with cited chunk #{chunk.ChunkIndex} " +
                        $"(page {chunk.PageNumber}), below threshold {context.Options.MinClaimGroundingSimilarity}",
                        path));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                violations.Add(new MechanicValidationViolation(
                    "T3_grounding_unavailable",
                    $"embedding service unavailable, failing closed: {ex.Message}",
                    path));
                break; // outage is global; no point retrying each claim
            }
        }

        return violations;
    }

    private static MechanicSourceChunk? ResolveChunk(JsonElement citation, IReadOnlyList<MechanicSourceChunk> pool)
    {
        if (citation.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        if (citation.TryGetProperty("chunk_id", out var idEl) && idEl.ValueKind == JsonValueKind.String
            && Guid.TryParse(idEl.GetString(), out var cid))
        {
            var byId = pool.FirstOrDefault(p => p.ChunkId == cid);
            if (byId != null)
            {
                return byId;
            }
        }
        if (citation.TryGetProperty("pdf_page", out var pEl) && pEl.ValueKind == JsonValueKind.Number
            && pEl.TryGetInt32(out var page))
        {
            return pool.FirstOrDefault(p => p.PageNumber == page);
        }
        return null;
    }

    internal static double Cosine(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
        {
            return 0;
        }
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        if (na <= 0d || nb <= 0d)
        {
            return 0;
        }
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    private static string Trunc(string s) => s.Length <= 50 ? s : s[..50];
}
