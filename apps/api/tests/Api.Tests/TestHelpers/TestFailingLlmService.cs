using System.Runtime.CompilerServices;
using Api.Services;
using Api.Services.LlmClients;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Deterministic <see cref="ILlmService"/> test double that NEVER touches the network.
///
/// REQ-AI-TEST-001: automated tests must not consume paid AI tokens. Registered by the integration
/// and E2E WebApplicationFactories so the LLM path is fail-closed at the service level, independent
/// of whether an <c>OPENROUTER_API_KEY</c> happens to be present in the environment. It returns a
/// graceful failure — the same observable outcome as today's "provider not configured" path — so
/// endpoints degrade exactly as they do now (tests that tolerate/skip on failure keep passing).
/// Streaming methods yield an empty stream, mirroring the real HybridLlmService's yield-break on
/// non-completion (StreamChunk has no error channel by design).
///
/// Tests that need a positive LLM response override this with their own mock via
/// <c>WithWebHostBuilder(b =&gt; b.ConfigureTestServices(...))</c>, which runs after the factory and
/// therefore wins (see e.g. GlobalKbAskStreamEndpointTests).
///
/// This is defence-in-depth alongside <c>PaidAiHostGuardHandler</c>: the guard blocks paid AI hosts
/// at the HTTP layer (covering translation and any other HTTP path), while this fake keeps the
/// chat/RAG service path deterministic and network-free.
/// </summary>
internal sealed class TestFailingLlmService : ILlmService
{
    private const string Reason =
        "LLM is disabled in tests (REQ-AI-TEST-001: automated tests must not consume AI tokens).";

    public Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt, string userPrompt,
        RequestSource source = RequestSource.Manual, CancellationToken ct = default)
        => Task.FromResult(LlmCompletionResult.CreateFailure(Reason));

    public async IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string systemPrompt, string userPrompt,
        RequestSource source = RequestSource.Manual,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task<T?> GenerateJsonAsync<T>(
        string systemPrompt, string userPrompt,
        RequestSource source = RequestSource.Manual, CancellationToken ct = default) where T : class
        => Task.FromResult<T?>(null);

    public Task<LlmCompletionResult> GenerateMultimodalCompletionAsync(
        IReadOnlyList<LlmMessage> messages,
        RequestSource source = RequestSource.Manual, CancellationToken ct = default)
        => Task.FromResult(LlmCompletionResult.CreateFailure(Reason));

    public async IAsyncEnumerable<StreamChunk> GenerateMultimodalCompletionStreamAsync(
        IReadOnlyList<LlmMessage> messages,
        RequestSource source = RequestSource.Manual,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task<LlmCompletionResult> GenerateCompletionWithModelAsync(
        string explicitModel, string systemPrompt, string userPrompt,
        RequestSource source = RequestSource.Manual, int? maxTokens = null, CancellationToken ct = default)
        => Task.FromResult(LlmCompletionResult.CreateFailure(Reason));

    // GenerateCompletionWithModelFallbackAsync uses the default interface implementation
    // (delegates to GenerateCompletionWithModelAsync → CreateFailure).
}
