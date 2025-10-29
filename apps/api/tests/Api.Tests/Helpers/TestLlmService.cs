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

        return Task.FromResult(LlmCompletionResult.CreateSuccess(response));
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
        const string marker = "Relevant Rule Context:";
        var markerIndex = prompt.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var start = markerIndex + marker.Length;
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
