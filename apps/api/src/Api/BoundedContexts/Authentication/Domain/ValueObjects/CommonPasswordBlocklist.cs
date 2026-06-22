using System.Reflection;

namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// I7 (auth security fixes): top-N common-password blocklist. Loaded once
/// from the embedded resource <c>CommonPasswords.txt</c> and queried via an
/// O(1) <see cref="HashSet{T}"/> on <see cref="PasswordHash.Create"/>.
///
/// The shipped seed (~115 entries) covers the highest-frequency offenders
/// from public breach corpora — "password", "qwerty123", "Welcome2025!",
/// admin defaults, and so on. Operators wanting the canonical
/// SecLists top-1000 / top-10k can replace the embedded file's contents
/// without touching code; the loader is content-agnostic.
///
/// Lookup is case-insensitive: real-world breach corpora prove that
/// case-tinkering ("Password" → "PASSWORD" → "password") is the first
/// thing attackers try, so blocking just the lowercase form would be
/// trivial to bypass.
/// </summary>
internal static class CommonPasswordBlocklist
{
    private const string ResourceName = "Api.BoundedContexts.Authentication.Domain.Resources.CommonPasswords.txt";

    // Lazy initialisation: read the embedded resource once on first use,
    // pin it as an immutable case-insensitive set for the rest of the
    // process lifetime. Loading ~115 strings is sub-millisecond — the
    // lazy wrapper is for "if no Create call ever fires, don't allocate"
    // rather than a perf concern.
    private static readonly Lazy<HashSet<string>> _entries = new(LoadEntries, isThreadSafe: true);

    /// <summary>
    /// Returns true iff the supplied plaintext appears in the embedded
    /// blocklist (case-insensitive). Whitespace-trimming the input is the
    /// caller's responsibility — we don't normalise "password " here
    /// because a trailing space technically yields a different password,
    /// and accepting the blocklist match for "password " would mask a
    /// real input bug at the call site.
    /// </summary>
    public static bool IsCommon(string plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
            return false;

        return _entries.Value.Contains(plaintext);
    }

    private static HashSet<string> LoadEntries()
    {
        var assembly = typeof(CommonPasswordBlocklist).Assembly;

        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' not found. Verify the file is " +
                "set as EmbeddedResource in Api.csproj and the namespace matches.");

        using var reader = new StreamReader(stream);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            // Skip blank lines and #-prefixed comments so the resource
            // file can carry attribution / source notes.
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            set.Add(trimmed);
        }

        return set;
    }
}
