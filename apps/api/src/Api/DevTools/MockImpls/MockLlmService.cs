using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.Scenarios;
using Api.Services;
using Api.Services.LlmClients;

namespace Api.DevTools.MockImpls;

/// <summary>
/// Deterministic mock of ILlmService.
/// Returns scenario-driven responses; SSE stream splits text into 5 chunks + final.
/// </summary>
internal sealed class MockLlmService : ILlmService
{
    private readonly ScenarioLoader _scenarios;

    /// <summary>Creates a new MockLlmService.</summary>
    public MockLlmService(ScenarioLoader scenarios)
    {
        _scenarios = scenarios;
    }

    /// <inheritdoc />
    public Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default)
    {
        var response = BuildResponse(userPrompt);
        var usage = new LlmUsage(
            PromptTokens: EstimateTokens(systemPrompt + userPrompt),
            CompletionTokens: EstimateTokens(response),
            TotalTokens: 0);
        return Task.FromResult(LlmCompletionResult.CreateSuccess(response, usage, LlmCost.Empty));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string systemPrompt,
        string userPrompt,
        RequestSource source = RequestSource.Manual,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var full = BuildResponse(userPrompt);
        var chunkSize = System.Math.Max(1, full.Length / 5);
        for (int i = 0; i < full.Length; i += chunkSize)
        {
            ct.ThrowIfCancellationRequested();
            var piece = full.Substring(i, System.Math.Min(chunkSize, full.Length - i));
            yield return new StreamChunk(Content: piece);
            await Task.Yield();
        }
        yield return new StreamChunk(
            Content: null,
            Usage: new LlmUsage(EstimateTokens(userPrompt), EstimateTokens(full), 0),
            Cost: LlmCost.Empty,
            IsFinal: true);
    }

    /// <inheritdoc />
    public Task<T?> GenerateJsonAsync<T>(
        string systemPrompt,
        string userPrompt,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default) where T : class
    {
        try
        {
            var instance = System.Activator.CreateInstance<T>();
            return Task.FromResult<T?>(instance);
        }
        catch
        {
            return Task.FromResult<T?>(null);
        }
    }

    /// <inheritdoc />
    public Task<LlmCompletionResult> GenerateMultimodalCompletionAsync(
        IReadOnlyList<LlmMessage> messages,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default)
    {
        // Extract text from messages for mock response
        var userText = string.Join(" ", messages
            .Where(m => string.Equals(m.Role, "user", System.StringComparison.Ordinal))
            .SelectMany(m => m.Content.OfType<TextContentPart>().Select(t => t.Text)));
        var systemText = string.Join(" ", messages
            .Where(m => string.Equals(m.Role, "system", System.StringComparison.Ordinal))
            .SelectMany(m => m.Content.OfType<TextContentPart>().Select(t => t.Text)));
        return GenerateCompletionAsync(systemText, userText, source, ct);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamChunk> GenerateMultimodalCompletionStreamAsync(
        IReadOnlyList<LlmMessage> messages,
        RequestSource source = RequestSource.Manual,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var userText = string.Join(" ", messages
            .Where(m => string.Equals(m.Role, "user", System.StringComparison.Ordinal))
            .SelectMany(m => m.Content.OfType<TextContentPart>().Select(t => t.Text)));
        var systemText = string.Join(" ", messages
            .Where(m => string.Equals(m.Role, "system", System.StringComparison.Ordinal))
            .SelectMany(m => m.Content.OfType<TextContentPart>().Select(t => t.Text)));
        await foreach (var chunk in GenerateCompletionStreamAsync(systemText, userText, source, ct).ConfigureAwait(false))
        {
            yield return chunk;
        }
    }

    /// <inheritdoc />
    public Task<LlmCompletionResult> GenerateCompletionWithModelAsync(
        string explicitModel,
        string systemPrompt,
        string userPrompt,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default)
    {
        return GenerateCompletionAsync(systemPrompt, userPrompt, source, ct);
    }

    private string BuildResponse(string userPrompt)
    {
        // Load scenario data if available; fall back to generic deterministic response.
        var scenario = _scenarios.Load("llm-default");
        var scenarioHint = scenario.ValueKind == System.Text.Json.JsonValueKind.Object
            && scenario.TryGetProperty("response", out var prop)
                ? prop.GetString()
                : null;

        return scenarioHint
            ?? $"[mock-llm] You asked: \"{userPrompt.Trim()}\". This is a deterministic mock response generated by MockLlmService for development purposes.";
    }

    private static int EstimateTokens(string text)
    {
        return (text?.Length ?? 0) / 4;
    }
}
