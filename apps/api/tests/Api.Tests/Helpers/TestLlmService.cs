using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Api.Models;
using Api.Services;

namespace Api.Tests.Helpers;

internal sealed class TestLlmService : ILlmService
{
    public Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default)
    {
        var snippet = ExtractFirstSnippet(userPrompt);
        string response;

        if (!string.IsNullOrEmpty(snippet) &&
            snippet.Contains("three in a row", StringComparison.OrdinalIgnoreCase))
        {
            response = "Players win by completing three marks in a row, as described in the rules.";
        }
        else if (!string.IsNullOrEmpty(snippet))
        {
            response = snippet;
        }
        else
        {
            response = "This is a deterministic test LLM response.";
        }

        // TEST-711: Calculate realistic token usage for test assertions
        var promptTokens = EstimateTokens(systemPrompt + userPrompt);
        var completionTokens = EstimateTokens(response);
        var usage = new LlmUsage(promptTokens, completionTokens, promptTokens + completionTokens);

        return Task.FromResult(LlmCompletionResult.CreateSuccess(response, usage));
    }

    /// <summary>
    /// Rough token estimation (1 token ≈ 4 characters for English text)
    /// </summary>
    private static int EstimateTokens(string text)
    {
        return Math.Max(1, (text?.Length ?? 0) / 4);
    }

    public async IAsyncEnumerable<string> GenerateCompletionStreamAsync(
        string systemPrompt,
        string userPrompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var result = await GenerateCompletionAsync(systemPrompt, userPrompt, ct);
        yield return result.Response;
    }

    public Task<T?> GenerateJsonAsync<T>(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default) where T : class
    {
        if (typeof(T) == typeof(FollowUpQuestionsDto))
        {
            var dto = new FollowUpQuestionsDto(new List<string>
            {
                "What happens if no player makes three in a row?",
                "How should players decide who starts first?",
                "Are diagonal wins treated differently?"
            });

            return Task.FromResult(dto as T);
        }

        return Task.FromResult<T?>(null);
    }

    private static string? ExtractFirstSnippet(string prompt)
    {
        // TEST-711: Support both RAG context markers
        // RagService uses "Relevant Rule Context:", ChessAgentService uses "CHESS KNOWLEDGE BASE:"
        var markers = new[] { "Relevant Rule Context:", "CHESS KNOWLEDGE BASE:", "CONTEXT FROM RULEBOOK" };

        int markerIndex = -1;
        string? foundMarker = null;
        foreach (var marker in markers)
        {
            markerIndex = prompt.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
            {
                foundMarker = marker;
                break;
            }
        }

        if (markerIndex < 0 || foundMarker == null)
        {
            return null;
        }

        var start = markerIndex + foundMarker.Length;

        // TEST-711: For ChessAgentService format, extract ALL context up to "QUESTION:" or "POSITION:"
        // Format is:
        // CHESS KNOWLEDGE BASE:
        // [Source 1]
        // <text>
        //
        // ---
        //
        // [Source 2]
        // <text>
        // ...
        // QUESTION:
        // <question>
        if (foundMarker == "CHESS KNOWLEDGE BASE:")
        {
            var questionIndex = prompt.IndexOf("\nQUESTION:", start, StringComparison.OrdinalIgnoreCase);
            var positionIndex = prompt.IndexOf("\nPOSITION:", start, StringComparison.OrdinalIgnoreCase);

            var endIndex = -1;
            if (questionIndex >= 0 && (positionIndex < 0 || questionIndex < positionIndex))
            {
                endIndex = questionIndex;
            }
            else if (positionIndex >= 0)
            {
                endIndex = positionIndex;
            }

            if (endIndex > start)
            {
                return prompt.Substring(start, endIndex - start).Trim();
            }
            // If no QUESTION or POSITION found, return everything after the marker
            return prompt.Substring(start).Trim();
        }

        // Original logic for "Relevant Rule Context:" format
        var remainder = prompt.Substring(start)
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrEmpty(line));

        if (string.IsNullOrEmpty(remainder))
        {
            return null;
        }

        if (remainder.Length > 2 && char.IsDigit(remainder[0]) && remainder[1] == '.')
        {
            return remainder.Substring(2).Trim();
        }

        if (remainder.StartsWith("- "))
        {
            return remainder.Substring(2).Trim();
        }

        return remainder;
    }
}
