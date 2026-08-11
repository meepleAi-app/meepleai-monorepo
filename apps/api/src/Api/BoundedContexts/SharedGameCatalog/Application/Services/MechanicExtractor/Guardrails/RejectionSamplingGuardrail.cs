using System.Text;
using System.Text.Json;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;

/// <summary>
/// T2 — detects long verbatim copying. Flags any contiguous N-gram (N =
/// <see cref="Configuration.MechanicGuardrailOptions.MaxConsecutiveSourceWords"/>) of normalized
/// words in a claim-bearing field that exact-matches a contiguous N-gram in the source chunk pool.
/// Citation <c>quote</c> fields are excluded (they are allowed verbatim, capped by T1).
/// Uses a rolling FNV-1a hash index (Rabin-Karp style) with token-equality confirmation.
/// </summary>
internal sealed class RejectionSamplingGuardrail : IMechanicGuardrail
{
    public string RuleFamily => "T2";
    public int Order => 30;

    private static readonly string[] ScannedFields = { "claim", "description", "text", "answer", "primary" };

    public Task<IReadOnlyList<MechanicValidationViolation>> EvaluateAsync(
        MechanicGuardrailContext context, CancellationToken cancellationToken)
    {
        var n = context.Options.MaxConsecutiveSourceWords;
        var violations = new List<MechanicValidationViolation>();

        if (n <= 0 || context.SourceChunks.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<MechanicValidationViolation>>(violations);
        }

        var sourceNgrams = new Dictionary<long, List<(int chunk, int offset, string[] gram)>>();
        foreach (var sc in context.SourceChunks)
        {
            var tokens = Tokenize(sc.Content);
            IndexNgrams(tokens, n, sc.ChunkIndex, sourceNgrams);
        }

        if (sourceNgrams.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<MechanicValidationViolation>>(violations);
        }

        MechanicJsonWalker.ForEachObject(context.Root, "$", (obj, path) =>
        {
            if (obj.ValueKind != JsonValueKind.Object)
            {
                return;
            }
            foreach (var field in ScannedFields)
            {
                if (!obj.TryGetProperty(field, out var el) || el.ValueKind != JsonValueKind.String)
                {
                    continue;
                }
                var candidate = Tokenize(el.GetString() ?? string.Empty);
                if (candidate.Length < n)
                {
                    continue;
                }

                for (var i = 0; i + n <= candidate.Length; i++)
                {
                    var hash = HashWindow(candidate, i, n);
                    if (!sourceNgrams.TryGetValue(hash, out var bucket))
                    {
                        continue;
                    }
                    var window = candidate.AsSpan(i, n);
                    foreach (var entry in bucket)
                    {
                        if (window.SequenceEqual(entry.gram))
                        {
                            var seq = string.Join(' ', window.ToArray());
                            violations.Add(new MechanicValidationViolation(
                                "T2_long_verbatim",
                                $"{n}-word sequence matches chunk #{entry.chunk} at offset {entry.offset}: '{seq}'",
                                $"{path}.{field}"));
                            return; // one violation per object is enough
                        }
                    }
                }
            }
        });

        return Task.FromResult<IReadOnlyList<MechanicValidationViolation>>(violations);
    }

    private static void IndexNgrams(string[] tokens, int n, int chunk,
        Dictionary<long, List<(int, int, string[])>> index)
    {
        for (var i = 0; i + n <= tokens.Length; i++)
        {
            var hash = HashWindow(tokens, i, n);
            var gram = tokens.AsSpan(i, n).ToArray();
            if (!index.TryGetValue(hash, out var list))
            {
                list = new List<(int, int, string[])>();
                index[hash] = list;
            }
            list.Add((chunk, i, gram));
        }
    }

    // FNV-1a over the joined normalized tokens of the window (deterministic).
    private static long HashWindow(string[] tokens, int start, int n)
    {
        unchecked
        {
            const long prime = 1099511628211L;
            var hash = 1469598103934665603L;
            for (var k = 0; k < n; k++)
            {
                foreach (var ch in tokens[start + k])
                {
                    hash ^= ch;
                    hash *= prime;
                }
                hash ^= ' ';
                hash *= prime;
            }
            return hash;
        }
    }

    // Normalize: lowercase, strip punctuation, collapse whitespace, NO stemming, NO diacritic folding.
    internal static string[] Tokenize(string s)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Append(char.ToLowerInvariant(ch));
            }
            else if (current.Length > 0)
            {
                result.Add(current.ToString());
                current.Clear();
            }
        }
        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }
        return result.ToArray();
    }
}
