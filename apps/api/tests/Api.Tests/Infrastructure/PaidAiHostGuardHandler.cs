namespace Api.Tests.Infrastructure;

/// <summary>
/// Fail-closed network guard enforcing REQ-AI-TEST-001: automated tests must never consume paid
/// AI tokens. Installed on every HttpClient produced by IHttpClientFactory in the Testing/CI
/// environment (see <see cref="PaidAiHostGuardExtensions"/>). Any request whose host resolves to a
/// paid LLM provider is blocked with <see cref="PaidAiCallInTestException"/> — <b>before</b> it
/// reaches the network — even if a real API key is configured. Local / self-hosted AI (embedding,
/// reranker, Ollama) is delegated normally.
/// </summary>
internal sealed class PaidAiHostGuardHandler : DelegatingHandler
{
    /// <summary>
    /// Registrable suffixes of paid AI providers used anywhere in the codebase. A request is
    /// blocked when its host equals one of these or is a subdomain of it. Extend this list when a
    /// new paid provider is added (e.g. a direct OpenAI/Anthropic client).
    /// </summary>
    private static readonly string[] PaidAiHostSuffixes =
    {
        "openrouter.ai",   // OpenRouterLlmClient — routes OpenAI/Anthropic/Google/DeepSeek
        "deepseek.com",    // DeepSeekLlmClient
        "openai.com",      // defensive: any direct OpenAI client
        "anthropic.com",   // defensive: any direct Anthropic client
    };

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var host = request.RequestUri?.Host;
        if (host != null && IsPaidAiHost(host))
        {
            throw new PaidAiCallInTestException(
                $"Blocked a test-time HTTP call to paid AI host '{host}' ({request.RequestUri}). " +
                "REQ-AI-TEST-001: automated tests must not consume AI tokens. Mock ILlmService / " +
                "ILlmClient (or the relevant IHttpClientFactory client) instead of hitting a real provider.");
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static bool IsPaidAiHost(string host)
    {
        // Normalize a trailing-dot absolute FQDN ("openrouter.ai.") so it cannot bypass the match.
        host = host.TrimEnd('.');

        foreach (var suffix in PaidAiHostSuffixes)
        {
            if (host.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Thrown by <see cref="PaidAiHostGuardHandler"/> when a test attempts a network call to a paid
/// AI provider. Failing the test is intentional (REQ-AI-TEST-001): tests must never spend tokens.
/// </summary>
internal sealed class PaidAiCallInTestException : Exception
{
    public PaidAiCallInTestException(string message) : base(message)
    {
    }
}
