using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// Issue #3495 finding H7 (Slice E) — architecture gate: <b>no egress <c>HttpClient</c> may reach the
/// public internet without the SSRF connect-pin</b>.
/// <para>
/// The pin (<c>ConfigureSsrfPin</c> → <c>SsrfPinnedConnect</c>) is the ONLY place where DNS-rebinding
/// and redirect-to-internal are closed. A new <c>AddHttpClient</c> that forgets it re-opens the hole
/// silently, and no runtime test covers a client nobody wrote a test for. This gate enumerates EVERY
/// <c>AddHttpClient</c> registration in the API source and forces each one into exactly one of two
/// explicit buckets:
/// </para>
/// <list type="bullet">
/// <item><see cref="MustBePinned"/> — external egress: the registration chain MUST call
/// <c>ConfigureSsrfPin</c>.</item>
/// <item><see cref="Exempt"/> — internal/trusted targets (compose services, sidecars, monitoring)
/// with a written reason.</item>
/// </list>
/// <para>
/// A registration in neither bucket fails the test: adding an egress client is a security decision
/// that must be made deliberately, not by omission.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "3495")]
public sealed class EgressHttpClientPinArchitectureTests
{
    /// <summary>
    /// External egress clients: they dial hosts outside our infrastructure, so every connection MUST
    /// go through the SSRF connect-pin. Key = the client identifier as written at the registration
    /// site (the named-client string, or the implementation type for a typed client — which is the
    /// name <c>AddHttpClient&lt;TClient, TImpl&gt;</c> registers under).
    /// </summary>
    private static readonly Dictionary<string, string> MustBePinned = new(StringComparer.Ordinal)
    {
        ["BggCoverDownloader"] = "cover image fetched from a BGG-supplied URL (#3495 fix 3/N)",
        ["SsrfSafeHttpClient"] = "arbitrary admin-supplied URL, the manual cover sink (#3495 fix 5/N)",
        ["BggCatalogProvider"] = "boardgamegeek.com catalog XML",
        ["BggApiClient"] = "boardgamegeek.com XML API v2 (typed client)",
        ["BggApi"] = "boardgamegeek.com XML API v2 (named client shared by BggApiService/health check)",
        ["WikidataCatalogProvider"] = "query.wikidata.org SPARQL",
        ["WikimediaCommonsClient"] = "commons.wikimedia.org + upload.wikimedia.org",
        ["SlackWebhookClient"] = "admin-configured Slack webhook URL (#3495 fix 3/N)",
        ["SlackApi"] = "Slack API/webhook URLs stored per connection — caller-supplied absolute URI",
    };

    /// <summary>
    /// Registrations exempt from the pin, each with the reason it is not external egress. Internal
    /// compose/VPC services and sidecars are trusted by design: pinning them would break the very
    /// private addresses they are supposed to dial.
    /// </summary>
    private static readonly Dictionary<string, string> Exempt = new(StringComparer.Ordinal)
    {
        ["(default)"] = "IHttpClientFactory default client — no ambient egress of its own; every "
            + "security-relevant sink uses a named/typed registration above",
        ["Ollama"] = "self-hosted LLM runtime, private/compose address by design",
        ["ai-ollama"] = "health probe for the same self-hosted Ollama runtime",
        ["OpenRouter"] = "SaaS gateway on a hardcoded public endpoint; no user/DB input reaches the URL",
        ["OpenRouterService"] = "same hardcoded OpenRouter endpoint (typed admin client)",
        ["HuggingFace"] = "SaaS inference endpoint from static configuration; no user input in the URL",
        ["ResendClient"] = "Resend SaaS SDK endpoint, static configuration",
        ["Infisical"] = "secrets backend from static configuration",
        ["EmbeddingService"] = "internal Python microservice (compose network)",
        ["ai-embedding"] = "health probe for the internal embedding microservice",
        ["ai-reranker"] = "health probe for the internal reranker microservice",
        ["CrossEncoderRerankerClient"] = "internal reranker microservice (compose network)",
        ["ai-unstructured"] = "health probe for the internal unstructured microservice",
        ["UnstructuredService"] = "internal unstructured microservice (compose network)",
        ["UnstructuredPdfTextExtractor.HiResClientName"] = "internal unstructured microservice, hi-res profile",
        ["ai-orchestrator"] = "health probe for the internal orchestration microservice",
        ["OrchestrationService"] = "internal orchestration microservice (compose network)",
        ["SmolDoclingService"] = "internal SmolDocling microservice (compose network)",
        ["smoldocling-photo-preprocessor"] = "internal SmolDocling microservice, photo pre-processing profile",
        ["SmoldoclingTableExtractor.NamedClientKey"] = "internal SmolDocling microservice, table-extraction profile",
        ["DockerProxyService"] = "docker socket proxy sidecar (private address by design)",
        ["SeqQueryClient"] = "internal Seq log server (compose network)",
        ["PrometheusHttpClient"] = "internal Prometheus (compose network)",
        ["PrometheusLabelsClient"] = "internal Prometheus (compose network)",
        ["PrometheusClientService"] = "internal Prometheus (compose network)",
        ["SshTunnelSidecar"] = "SSH tunnel sidecar on the compose network",
        ["provider-probe"] = "admin-configured AI provider probe: admin-only surface with a hard 5s "
            + "budget; pinning it is tracked separately, it is not part of the user-reachable path",
    };

    private const string PinCall = "ConfigureSsrfPin";
    private const string PrimaryHandlerCall = "ConfigurePrimaryHttpMessageHandler";

    [Fact]
    public void EveryHttpClientRegistration_IsClassified()
    {
        var registrations = ScanRegistrations();

        registrations.Should().NotBeEmpty("the scanner must find the API's AddHttpClient registrations");

        var unclassified = registrations
            .Select(r => r.Name)
            .Where(name => !MustBePinned.ContainsKey(name) && !Exempt.ContainsKey(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        unclassified.Should().BeEmpty(
            "every AddHttpClient registration is a security decision (#3495 H7): add the client to "
            + "MustBePinned and call ConfigureSsrfPin if it dials the public internet, or to Exempt "
            + "with the reason it targets internal/trusted infrastructure. Unclassified:\n"
            + string.Join('\n', unclassified));
    }

    [Fact]
    public void EveryExternalEgressRegistration_IsPinned()
    {
        var unpinned = ScanRegistrations()
            .Where(r => MustBePinned.ContainsKey(r.Name) && !r.Statement.Contains(PinCall, StringComparison.Ordinal))
            .Select(r => $"{r.Location}  {r.Name} — {MustBePinned[r.Name]}")
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();

        unpinned.Should().BeEmpty(
            "external egress clients must dial through the SSRF connect-pin, otherwise DNS-rebinding "
            + "and redirect-to-internal are open on that sink (#3495 C1/H7). Missing ConfigureSsrfPin:\n"
            + string.Join('\n', unpinned));
    }

    [Fact]
    public void PinnedRegistrations_DoNotOverrideTheirOwnPrimaryHandler()
    {
        var conflicts = ScanRegistrations()
            .Where(r => r.Statement.Contains(PinCall, StringComparison.Ordinal)
                && r.Statement.Contains(PrimaryHandlerCall, StringComparison.Ordinal))
            .Select(r => $"{r.Location}  {r.Name}")
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();

        conflicts.Should().BeEmpty(
            "ConfigureSsrfPin IS the primary handler: a second ConfigurePrimaryHttpMessageHandler on "
            + "the same builder wins by last-registration and silently drops the pin. Pass the handler "
            + "tuning to ConfigureSsrfPin instead. Conflicts:\n" + string.Join('\n', conflicts));
    }

    // -----------------------------------------------------------------------
    // Source scanning
    // -----------------------------------------------------------------------

    private sealed record Registration(string Name, string Statement, string Location);

    private static List<Registration> ScanRegistrations()
    {
        var apiSrc = LocateApiSrc();
        var registrations = new List<Registration>();

        foreach (var path in Directory.EnumerateFiles(apiSrc, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(apiSrc, path).Replace('\\', '/');
            if (relative.StartsWith("bin/", StringComparison.Ordinal)
                || relative.StartsWith("obj/", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(path);
            foreach (var offset in SourceScanner.FindCodeOccurrences(text, "AddHttpClient"))
            {
                var after = offset + "AddHttpClient".Length;

                // Skip AddHttpClientInstrumentation and friends: only an exact call site counts.
                if (after < text.Length && (char.IsLetterOrDigit(text[after]) || text[after] == '_'))
                {
                    continue;
                }

                var statement = SourceScanner.ReadStatement(text, offset);
                var line = text.Take(offset).Count(c => c == '\n') + 1;
                registrations.Add(new Registration(
                    ParseClientName(text, after),
                    statement,
                    $"{relative}:{line}"));
            }
        }

        return registrations;
    }

    /// <summary>
    /// The identifier the registration is keyed by: the named-client literal, the implementation type
    /// of a typed client (what <c>AddHttpClient&lt;TClient, TImpl&gt;</c> names it), the verbatim
    /// expression when the name is a constant, or <c>(default)</c> for the factory default client.
    /// </summary>
    private static string ParseClientName(string text, int afterCall)
    {
        var index = SkipWhitespace(text, afterCall);
        if (index >= text.Length)
        {
            return "(default)";
        }

        if (text[index] == '<')
        {
            var end = MatchAngleBracket(text, index);
            var typeArguments = text[(index + 1)..end].Split(',');
            var last = typeArguments[^1].Trim();
            var dot = last.LastIndexOf('.');
            return dot >= 0 ? last[(dot + 1)..] : last;
        }

        if (text[index] != '(')
        {
            return "(default)";
        }

        var argument = SkipWhitespace(text, index + 1);
        if (argument >= text.Length || text[argument] == ')')
        {
            return "(default)";
        }

        if (text[argument] == '"')
        {
            var close = text.IndexOf('"', argument + 1);
            return close < 0 ? "(default)" : text[(argument + 1)..close];
        }

        // A non-literal first argument is either a name constant or the configure lambda. Lambdas and
        // service-collection arguments mean "default client"; anything else is a name expression.
        var token = ReadIdentifierLike(text, argument);
        return token.Length == 0 || token.Contains("=>", StringComparison.Ordinal)
            ? "(default)"
            : token;
    }

    private static string ReadIdentifierLike(string text, int start)
    {
        var end = start;
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_' || text[end] == '.'))
        {
            end++;
        }

        var token = text[start..end];
        // "client" / "sp" and other lambda parameters are not client names.
        return token.Contains('.', StringComparison.Ordinal) ? token : string.Empty;
    }

    /// <summary>
    /// Returns the whole fluent registration statement starting at <paramref name="start"/>: every
    /// chained call up to the terminating semicolon at bracket depth zero (so a lambda body's
    /// semicolons do not cut the chain short). Comments and string literals are STRIPPED from the
    /// returned text — the callers match method names against it, and this file's own registrations
    /// mention <c>ConfigurePrimaryHttpMessageHandler</c> in prose right next to the real call.
    /// </summary>

    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    private static int MatchAngleBracket(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                depth++;
            }
            else if (text[i] == '>')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return text.Length - 1;
    }

    private static string LocateApiSrc()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the test binary must live inside the meepleai-monorepo repo");
        var apiSrc = Path.Combine(dir!.FullName, "apps", "api", "src", "Api");
        Directory.Exists(apiSrc).Should().BeTrue($"Api source must exist at {apiSrc}");
        return apiSrc;
    }
}
