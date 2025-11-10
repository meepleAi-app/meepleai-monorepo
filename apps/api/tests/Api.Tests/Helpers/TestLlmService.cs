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

        if (typeof(T) == typeof(ExplainOutline))
        {
            // TEST-827: Enhanced topic extraction with realistic outline generation
            var topic = ExtractTopicFromPrompt(userPrompt) ?? "Game Rules";
            var sections = GenerateRealisticOutlineSections(topic);

            var dto = new ExplainOutline(
                mainTopic: topic,
                sections: sections
            );

            return Task.FromResult(dto as T);
        }

        return Task.FromResult<T?>(null);
    }

    private static string? ExtractTopicFromPrompt(string prompt)
    {
        // TEST-827: Enhanced topic extraction with support for multiple prompt formats
        // Extract topic from various formats:
        // - "Topic: winning conditions"
        // - "explain topic: castling"
        // - "TOPIC: game setup"
        // - "about: scoring"
        // - "Explain 'winning conditions' for the game"
        var topicMarkers = new[] { "Topic:", "topic:", "explain topic:", "about:", "Explain '" };

        foreach (var marker in topicMarkers)
        {
            var index = prompt.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var start = index + marker.Length;
                var remainder = prompt.Substring(start).Trim();

                // Handle quoted topics like "Explain 'winning conditions'"
                if (marker.Contains("'"))
                {
                    var endQuote = remainder.IndexOf('\'');
                    if (endQuote > 0)
                    {
                        return remainder.Substring(0, endQuote).Trim();
                    }
                }

                // Extract first line or until punctuation
                var firstLine = remainder.Split('\n')[0].Trim();

                // Remove trailing punctuation/quotes
                firstLine = firstLine.TrimEnd('.', '?', '!', '"', '\'').Trim();

                if (!string.IsNullOrEmpty(firstLine))
                {
                    return firstLine;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// TEST-827: Generate realistic outline sections based on topic
    /// </summary>
    private static List<string> GenerateRealisticOutlineSections(string topic)
    {
        // Generate domain-specific sections based on topic keywords
        var topicLower = topic.ToLowerInvariant();

        // Winning/Victory conditions
        if (topicLower.Contains("win") || topicLower.Contains("victory") || topicLower.Contains("condition"))
        {
            return new List<string>
            {
                "Victory Conditions Overview",
                "Primary Win Conditions",
                "Alternative Victory Paths",
                "Tie-Breaking Rules",
                "Common Winning Strategies"
            };
        }

        // Setup/Preparation
        if (topicLower.Contains("setup") || topicLower.Contains("preparation") || topicLower.Contains("start"))
        {
            return new List<string>
            {
                "Initial Setup Requirements",
                "Component Placement",
                "Player Preparation Steps",
                "First Player Determination",
                "Setup Verification Checklist"
            };
        }

        // Scoring
        if (topicLower.Contains("scor") || topicLower.Contains("point"))
        {
            return new List<string>
            {
                "Scoring System Overview",
                "Point Calculation Methods",
                "Bonus Points and Multipliers",
                "Scoring Edge Cases",
                "Final Score Tallying"
            };
        }

        // Movement/Actions
        if (topicLower.Contains("move") || topicLower.Contains("action") || topicLower.Contains("turn"))
        {
            return new List<string>
            {
                "Available Actions Overview",
                "Movement Rules and Restrictions",
                "Turn Structure and Phases",
                "Action Priority and Timing",
                "Movement Examples and Scenarios"
            };
        }

        // Combat/Conflict
        if (topicLower.Contains("combat") || topicLower.Contains("battle") || topicLower.Contains("attack"))
        {
            return new List<string>
            {
                "Combat Basics and Prerequisites",
                "Attack Resolution Process",
                "Defense Mechanisms",
                "Damage Calculation",
                "Combat Outcome Effects"
            };
        }

        // Trading/Economy
        if (topicLower.Contains("trad") || topicLower.Contains("econom") || topicLower.Contains("resource"))
        {
            return new List<string>
            {
                "Trading Mechanics Overview",
                "Resource Types and Values",
                "Trade Negotiation Rules",
                "Economic Strategy Considerations",
                "Resource Management Tips"
            };
        }

        // Default/Generic structure for unknown topics
        return new List<string>
        {
            $"{topic} - Introduction",
            $"Core {topic} Mechanics",
            $"Detailed {topic} Rules",
            $"{topic} Strategy and Tips",
            $"Common {topic} Mistakes to Avoid",
            $"{topic} Advanced Concepts"
        };
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
