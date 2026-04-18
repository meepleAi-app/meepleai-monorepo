# Chat Inline Citations & Concise Responses — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make chat responses token-independent with inline citations, PDF page links, concise/detailed mode, and continuation mechanism.

**Architecture:** Backend post-processes LLM response to match RAG snippets, emits positioned citation events. Frontend renders highlighted spans with accordion expansion and PDF links. Prompt style (`concise`/`detailed`) controls response length; continuation token allows resuming truncated responses.

**Tech Stack:** C# (.NET 9), TypeScript (Next.js/React 19), SSE streaming, Tailwind CSS

**Spec:** `docs/superpowers/specs/2026-04-18-chat-inline-citations-design.md`

---

## File Map

### Backend — Create
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/InlineCitationMatcherService.cs` — text-snippet matching logic

### Backend — Modify
- `apps/api/src/Api/Models/Contracts.cs` — new event types + records
- `apps/api/src/Api/Services/ILlmService.cs` — add FinishReason to StreamChunk
- `apps/api/src/Api/Services/LlmClients/DeepSeekLlmClient.cs` — emit FinishReason in final chunk
- `apps/api/src/Api/Services/LlmClients/OpenRouterLlmClient.cs` — emit FinishReason in final chunk
- `apps/api/src/Api/Services/LlmClients/OllamaLlmClient.cs` — emit FinishReason in final chunk
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/StreamQaQueryHandler.cs` — emit new events, handle continuation
- `apps/api/src/Api/Routing/AiEndpoints.cs` — accept responseStyle/continuationToken in QaRequest
- `apps/api/src/Api/Services/PromptTemplateService.cs` — concise/detailed/continuation prompt variants

### Backend — Test
- `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Services/InlineCitationMatcherServiceTests.cs`

### Frontend — Create
- `apps/web/src/components/chat-unified/InlineCitationText.tsx` — text with highlighted citation spans + accordion
- `apps/web/src/components/chat-unified/CitationBlock.tsx` — chip citations below response
- `apps/web/src/components/chat-unified/ContinueButton.tsx` — "Continue response" button

### Frontend — Modify
- `apps/web/src/lib/api/clients/chatClient.ts` — QaStreamRequest fields + event types
- `apps/web/src/components/chat-unified/ChatMobile.tsx` — integrate new components, handle events 28/29
- `apps/web/src/components/chat-unified/ChatThreadView.tsx` — same for desktop

### Frontend — Test
- `apps/web/__tests__/components/chat-unified/InlineCitationText.test.tsx`
- `apps/web/__tests__/components/chat-unified/CitationBlock.test.tsx`
- `apps/web/__tests__/components/chat-unified/ContinueButton.test.tsx`

---

## Task 1: Backend contracts — new event types and records

**Files:**
- Modify: `apps/api/src/Api/Models/Contracts.cs:13-18,54-90`
- Modify: `apps/api/src/Api/Services/ILlmService.cs:14-18`

- [ ] **Step 1: Add responseStyle and continuationToken to QaRequest**

In `apps/api/src/Api/Models/Contracts.cs`, change the QaRequest record:

```csharp
internal record QaRequest(
    string gameId,
    string query,
    Guid? chatId = null,
    SearchMode searchMode = SearchMode.Hybrid,
    IReadOnlyList<Guid>? documentIds = null,
    string responseStyle = "concise",
    string? continuationToken = null);
```

- [ ] **Step 2: Add new event types to StreamingEventType enum**

After `CopyrightSanitized = 27`, add:

```csharp
    CopyrightSanitized = 27,       // Response body was sanitized due to verbatim copyright leak

    // Inline citation and continuation events (#chat-inline-citations)
    InlineCitation = 28,            // Positioned snippet matches within response text
    ContinuationAvailable = 29      // Response truncated, continuation available
```

- [ ] **Step 3: Add new streaming record types**

After the existing `StreamingComplete` record, add:

```csharp
internal record InlineCitationMatch(
    int StartOffset,
    int EndOffset,
    int SnippetIndex,
    int PageNumber,
    string PdfDocumentId,
    double Confidence);

internal record StreamingInlineCitations(
    IReadOnlyList<InlineCitationMatch> Citations);

internal record StreamingContinuation(
    string ContinuationToken,
    string Reason);
```

- [ ] **Step 4: Add FinishReason to StreamChunk**

In `apps/api/src/Api/Services/ILlmService.cs`, change:

```csharp
internal record StreamChunk(
    string? Content,
    LlmUsage? Usage = null,
    LlmCost? Cost = null,
    bool IsFinal = false,
    string? FinishReason = null);
```

- [ ] **Step 5: Build and verify**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded with 0 errors

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Models/Contracts.cs apps/api/src/Api/Services/ILlmService.cs
git commit -m "feat(kb): add inline citation and continuation contracts"
```

---

## Task 2: LLM clients — emit FinishReason in final chunk

**Files:**
- Modify: `apps/api/src/Api/Services/LlmClients/DeepSeekLlmClient.cs`
- Modify: `apps/api/src/Api/Services/LlmClients/OpenRouterLlmClient.cs`
- Modify: `apps/api/src/Api/Services/LlmClients/OllamaLlmClient.cs`

- [ ] **Step 1: DeepSeek — pass FinishReason in streaming final chunk**

In `DeepSeekLlmClient.cs`, find the streaming method where `IsFinal = true` is set. Change the final `StreamChunk` construction to include the finish_reason from the SSE response:

```csharp
// In the streaming loop, when emitting the final chunk:
yield return new StreamChunk(
    Content: content,
    Usage: usage,
    Cost: cost,
    IsFinal: true,
    FinishReason: finishReason);
```

The `finishReason` is already parsed from the SSE response JSON `choices[0].finish_reason`.

- [ ] **Step 2: OpenRouter — same pattern**

In `OpenRouterLlmClient.cs`, find the streaming final chunk emission and add `FinishReason`:

```csharp
yield return new StreamChunk(
    Content: content,
    Usage: usage,
    Cost: cost,
    IsFinal: true,
    FinishReason: finishReason);
```

- [ ] **Step 3: Ollama — same pattern**

In `OllamaLlmClient.cs`, Ollama uses `done: true` instead of `finish_reason`. Map it:

```csharp
yield return new StreamChunk(
    Content: content,
    Usage: usage,
    Cost: cost,
    IsFinal: true,
    FinishReason: isDone ? "stop" : null);
```

- [ ] **Step 4: Build and verify**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Services/LlmClients/
git commit -m "feat(kb): emit FinishReason in LLM streaming final chunks"
```

---

## Task 3: InlineCitationMatcherService — TDD

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/InlineCitationMatcherService.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Services/InlineCitationMatcherServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Models;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class InlineCitationMatcherServiceTests
{
    private readonly InlineCitationMatcherService _sut = new();

    [Fact]
    public void Match_ExactSubstring_ReturnsHighConfidence()
    {
        var answer = "Il gioco si svolge a turni. Ogni giocatore pesca una tessera e la posiziona sul tavolo.";
        var snippets = new[]
        {
            new Snippet("Ogni giocatore pesca una tessera e la posiziona sul tavolo.", "PDF:abc", 3, 0, 0.9f)
        };

        var result = _sut.Match(answer, snippets);

        result.Should().ContainSingle();
        result[0].SnippetIndex.Should().Be(0);
        result[0].Confidence.Should().BeGreaterOrEqualTo(0.9);
        result[0].StartOffset.Should().Be(28);
        result[0].EndOffset.Should().Be(86);
    }

    [Fact]
    public void Match_NoOverlap_ReturnsEmpty()
    {
        var answer = "Il gioco prevede carte e dadi.";
        var snippets = new[]
        {
            new Snippet("Le tessere hanno bordi colorati che devono combaciare.", "PDF:abc", 2, 0, 0.8f)
        };

        var result = _sut.Match(answer, snippets);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Match_FuzzyMatch_ReturnsMediumConfidence()
    {
        var answer = "Ogni giocatore prende una tessera e la mette sul tavolo.";
        var snippets = new[]
        {
            new Snippet("Ogni giocatore pesca una tessera e la posiziona sul tavolo.", "PDF:abc", 3, 0, 0.9f)
        };

        var result = _sut.Match(answer, snippets);

        result.Should().ContainSingle();
        result[0].Confidence.Should().BeInRange(0.6, 0.85);
    }

    [Fact]
    public void Match_OverlappingMatches_KeepsHighestConfidence()
    {
        var answer = "Ogni giocatore pesca una tessera e la posiziona sul tavolo.";
        var snippets = new[]
        {
            new Snippet("Ogni giocatore pesca una tessera", "PDF:abc", 3, 0, 0.9f),
            new Snippet("Ogni giocatore pesca una tessera e la posiziona sul tavolo.", "PDF:abc", 3, 0, 0.8f)
        };

        var result = _sut.Match(answer, snippets);

        // The longer exact match (snippet 1) should win on overlap
        result.Should().ContainSingle();
        result[0].SnippetIndex.Should().Be(1);
    }

    [Fact]
    public void Match_EmptyAnswer_ReturnsEmpty()
    {
        var result = _sut.Match("", new[] { new Snippet("text", "src", 1, 0, 0.5f) });
        result.Should().BeEmpty();
    }

    [Fact]
    public void Match_EmptySnippets_ReturnsEmpty()
    {
        var result = _sut.Match("some answer", Array.Empty<Snippet>());
        result.Should().BeEmpty();
    }

    [Fact]
    public void Match_ExtractsPdfDocumentId_FromSourceField()
    {
        var answer = "Posizionare la tessera di partenza al centro del tavolo.";
        var snippets = new[]
        {
            new Snippet("Posizionare la tessera di partenza al centro del tavolo.", "PDF:c6336f55-d494-4f48-a79c-657ed7bd9abd", 2, 0, 0.9f)
        };

        var result = _sut.Match(answer, snippets);

        result.Should().ContainSingle();
        result[0].PdfDocumentId.Should().Be("c6336f55-d494-4f48-a79c-657ed7bd9abd");
        result[0].PageNumber.Should().Be(2);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "InlineCitationMatcherServiceTests" --no-restore`
Expected: Compilation error — `InlineCitationMatcherService` does not exist

- [ ] **Step 3: Implement InlineCitationMatcherService**

```csharp
using Api.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Matches LLM response text against RAG snippets to find inline citation positions.
/// Uses exact substring, fuzzy, and n-gram matching strategies.
/// </summary>
internal sealed class InlineCitationMatcherService
{
    private const int MinPhraseWords = 5;
    private const double MinConfidenceThreshold = 0.6;
    private const double ExactMatchConfidence = 1.0;
    private const double FuzzyMatchConfidence = 0.8;
    private const double NgramMatchConfidence = 0.65;
    private const double MaxLevenshteinRatio = 0.15;
    private const double MinNgramOverlap = 0.6;
    private const int NgramSize = 3;

    public IReadOnlyList<InlineCitationMatch> Match(string answer, IReadOnlyList<Snippet> snippets)
    {
        if (string.IsNullOrWhiteSpace(answer) || snippets.Count == 0)
            return Array.Empty<InlineCitationMatch>();

        var candidates = new List<InlineCitationMatch>();

        for (var i = 0; i < snippets.Count; i++)
        {
            var snippet = snippets[i];
            var pdfDocumentId = ExtractPdfDocumentId(snippet.source);
            var phrases = ExtractPhrases(snippet.text, MinPhraseWords);

            foreach (var phrase in phrases)
            {
                var match = FindBestMatch(answer, phrase, i, snippet.page, pdfDocumentId);
                if (match != null)
                    candidates.Add(match);
            }

            // Also try full snippet text as a phrase
            var fullMatch = FindBestMatch(answer, snippet.text, i, snippet.page, pdfDocumentId);
            if (fullMatch != null)
                candidates.Add(fullMatch);
        }

        return ResolveOverlaps(candidates);
    }

    private InlineCitationMatch? FindBestMatch(string answer, string phrase, int snippetIndex, int page, string pdfDocumentId)
    {
        if (phrase.Length < 15) return null; // Skip very short phrases

        // Strategy 1: Exact substring (case-insensitive)
        var idx = answer.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            return new InlineCitationMatch(idx, idx + phrase.Length, snippetIndex, page, pdfDocumentId, ExactMatchConfidence);
        }

        // Strategy 2: Fuzzy match — sliding window
        var maxDist = (int)(phrase.Length * MaxLevenshteinRatio);
        var bestFuzzy = FindFuzzyMatch(answer, phrase, maxDist);
        if (bestFuzzy != null)
        {
            return new InlineCitationMatch(bestFuzzy.Value.start, bestFuzzy.Value.end, snippetIndex, page, pdfDocumentId, FuzzyMatchConfidence);
        }

        // Strategy 3: N-gram overlap
        var ngramMatch = FindNgramMatch(answer, phrase);
        if (ngramMatch != null)
        {
            return new InlineCitationMatch(ngramMatch.Value.start, ngramMatch.Value.end, snippetIndex, page, pdfDocumentId, NgramMatchConfidence);
        }

        return null;
    }

    private static (int start, int end)? FindFuzzyMatch(string text, string pattern, int maxDist)
    {
        if (pattern.Length > text.Length) return null;

        var windowSize = pattern.Length;
        var bestDist = maxDist + 1;
        var bestStart = -1;

        for (var i = 0; i <= text.Length - windowSize; i++)
        {
            var window = text.Substring(i, windowSize);
            var dist = LevenshteinDistance(window.ToLowerInvariant(), pattern.ToLowerInvariant());
            if (dist < bestDist)
            {
                bestDist = dist;
                bestStart = i;
            }
        }

        if (bestStart >= 0 && bestDist <= maxDist)
            return (bestStart, bestStart + windowSize);

        return null;
    }

    private static (int start, int end)? FindNgramMatch(string text, string pattern)
    {
        var patternNgrams = GetNgrams(pattern.ToLowerInvariant(), NgramSize);
        if (patternNgrams.Count == 0) return null;

        var windowSize = Math.Min(pattern.Length + pattern.Length / 3, text.Length);
        var bestOverlap = 0.0;
        var bestStart = -1;
        var bestEnd = -1;

        for (var i = 0; i <= text.Length - Math.Min(windowSize, pattern.Length); i++)
        {
            var end = Math.Min(i + windowSize, text.Length);
            var windowNgrams = GetNgrams(text[i..end].ToLowerInvariant(), NgramSize);
            var intersection = patternNgrams.Intersect(windowNgrams).Count();
            var overlap = (double)intersection / patternNgrams.Count;

            if (overlap > bestOverlap)
            {
                bestOverlap = overlap;
                bestStart = i;
                bestEnd = end;
            }
        }

        if (bestOverlap >= MinNgramOverlap && bestStart >= 0)
            return (bestStart, bestEnd);

        return null;
    }

    private static HashSet<string> GetNgrams(string text, int n)
    {
        var ngrams = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i <= text.Length - n; i++)
            ngrams.Add(text.Substring(i, n));
        return ngrams;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var m = a.Length;
        var n = b.Length;
        var dp = new int[m + 1, n + 1];

        for (var i = 0; i <= m; i++) dp[i, 0] = i;
        for (var j = 0; j <= n; j++) dp[0, j] = j;

        for (var i = 1; i <= m; i++)
        for (var j = 1; j <= n; j++)
        {
            var cost = a[i - 1] == b[j - 1] ? 0 : 1;
            dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
        }

        return dp[m, n];
    }

    private static IReadOnlyList<InlineCitationMatch> ResolveOverlaps(List<InlineCitationMatch> candidates)
    {
        if (candidates.Count == 0) return Array.Empty<InlineCitationMatch>();

        var sorted = candidates.OrderByDescending(c => c.Confidence)
                               .ThenByDescending(c => c.EndOffset - c.StartOffset)
                               .ToList();

        var result = new List<InlineCitationMatch>();
        foreach (var candidate in sorted)
        {
            var overlaps = result.Any(r =>
                candidate.StartOffset < r.EndOffset && candidate.EndOffset > r.StartOffset);
            if (!overlaps)
                result.Add(candidate);
        }

        return result.OrderBy(r => r.StartOffset).ToList();
    }

    private static List<string> ExtractPhrases(string text, int minWords)
    {
        var sentences = text.Split(new[] { '.', '!', '?', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);
        return sentences
            .Select(s => s.Trim())
            .Where(s => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= minWords)
            .ToList();
    }

    private static string ExtractPdfDocumentId(string source)
    {
        if (source.StartsWith("PDF:", StringComparison.OrdinalIgnoreCase))
            return source[4..];
        return source;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "InlineCitationMatcherServiceTests" --no-restore`
Expected: All 7 tests pass

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/InlineCitationMatcherService.cs
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Services/InlineCitationMatcherServiceTests.cs
git commit -m "feat(kb): add InlineCitationMatcherService with TDD tests"
```

---

## Task 4: Prompt template variants — concise/detailed/continuation

**Files:**
- Modify: `apps/api/src/Api/Services/PromptTemplateService.cs`

- [ ] **Step 1: Add response style prompt instructions**

Add a new method to `PromptTemplateService`:

```csharp
/// <summary>
/// Returns the response-style instruction to append to the system prompt.
/// </summary>
internal static string GetResponseStyleInstruction(string responseStyle)
{
    return responseStyle switch
    {
        "detailed" => "\n\nIMPORTANT RESPONSE STYLE: Rispondi in modo completo e approfondito. Spiega ogni aspetto rilevante con esempi dal regolamento. Usa sottosezioni se necessario.",
        "continuation" => "\n\nIMPORTANT: Continua la risposta precedente da dove ti eri fermato. Non ripetere ciò che hai già detto. Prosegui direttamente.",
        _ => "\n\nIMPORTANT RESPONSE STYLE: Rispondi in modo sintetico e strutturato (max 200 parole). Concentrati sul rispondere alla domanda, non riscrivere il regolamento. Usa elenchi puntati. Non citare le pagine nel testo — le citazioni vengono gestite automaticamente dal sistema."
    };
}
```

- [ ] **Step 2: Build and verify**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Services/PromptTemplateService.cs
git commit -m "feat(kb): add concise/detailed/continuation prompt variants"
```

---

## Task 5: StreamQaQueryHandler — emit new events + continuation

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/StreamQaQueryHandler.cs`
- Modify: `apps/api/src/Api/Routing/AiEndpoints.cs`

- [ ] **Step 1: Register InlineCitationMatcherService in handler constructor**

Add `InlineCitationMatcherService` as a constructor parameter and field in `StreamQaQueryHandler`.

- [ ] **Step 2: Add responseStyle to max tokens selection**

In `AskStreamInternalAsync`, before the LLM call, determine max tokens based on responseStyle:

```csharp
var maxTokens = query.ResponseStyle switch
{
    "detailed" => 2000,
    "continuation" => 2000,
    _ => 800 // concise
};
```

- [ ] **Step 3: Append response style instruction to system prompt**

Before the LLM call, after `BuildLlmPromptsAsync`:

```csharp
var (systemPrompt, userPrompt) = await BuildLlmPromptsAsync(...);
systemPrompt += PromptTemplateService.GetResponseStyleInstruction(query.ResponseStyle ?? "concise");

// If continuation, prepend partial answer to user prompt for context
if (!string.IsNullOrEmpty(query.ContinuationContext))
{
    userPrompt = $"[Risposta parziale precedente: {query.ContinuationContext}]\n\n{userPrompt}";
}
```

- [ ] **Step 4: Emit InlineCitation and ContinuationAvailable after Complete**

After the existing `yield return CreateEvent(StreamingEventType.Complete, ...)`:

```csharp
// Inline citation matching
var inlineCitations = _citationMatcher.Match(answerBuilder.ToString(), snippets!);
if (inlineCitations.Count > 0)
{
    yield return CreateEvent(StreamingEventType.InlineCitation,
        new StreamingInlineCitations(inlineCitations));
}

// Continuation available if truncated
if (llmFinishReason == "length")
{
    var continuationToken = Convert.ToBase64String(
        System.Text.Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                gameId = query.GameId,
                originalQuery = query.Query,
                partialAnswer = answerBuilder.ToString(),
                threadId = query.ThreadId
            })));
    yield return CreateEvent(StreamingEventType.ContinuationAvailable,
        new StreamingContinuation(continuationToken, "max_tokens_reached"));
}
```

- [ ] **Step 5: Track FinishReason from LLM stream chunks**

In the `await foreach` loop for LLM tokens, capture FinishReason:

```csharp
string? llmFinishReason = null;
await foreach (var chunk in _llmService.GenerateCompletionStreamAsync(...))
{
    // ... existing token handling ...
    if (chunk.IsFinal && chunk.FinishReason != null)
    {
        llmFinishReason = chunk.FinishReason;
    }
}
```

- [ ] **Step 6: Add ResponseStyle and ContinuationContext to StreamQaQuery**

Add `ResponseStyle` and `ContinuationContext` parameters to the `StreamQaQuery` record.

- [ ] **Step 7: Update AiEndpoints to pass new fields**

In `AiEndpoints.cs`, in `HandleQaStreamAsync`, construct the `StreamQaQuery` with the new fields from `QaRequest`:

```csharp
// Handle continuation token
string? continuationContext = null;
if (!string.IsNullOrEmpty(req.continuationToken))
{
    var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(req.continuationToken));
    var ctx = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(decoded);
    continuationContext = ctx.GetProperty("partialAnswer").GetString();
}

var query = new StreamQaQuery(
    req.gameId, req.query, req.chatId, req.documentIds,
    req.responseStyle, continuationContext);
```

- [ ] **Step 8: Build and verify**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/StreamQaQueryHandler.cs
git add apps/api/src/Api/Routing/AiEndpoints.cs
git commit -m "feat(kb): emit inline citations and continuation events in QA stream"
```

---

## Task 6: Frontend — chatClient.ts updates

**Files:**
- Modify: `apps/web/src/lib/api/clients/chatClient.ts`

- [ ] **Step 1: Update QaStreamRequest**

```typescript
export interface QaStreamRequest {
  gameId: string;
  query: string;
  chatId?: string;
  responseStyle?: 'concise' | 'detailed';
  continuationToken?: string;
}
```

- [ ] **Step 2: Add event type constants**

```typescript
export const QA_EVENT_TYPES = {
  STATE_UPDATE: 0,
  CITATIONS: 1,
  COMPLETE: 4,
  TOKEN: 7,
  FOLLOW_UP: 8,
  ERROR: 9,
  INLINE_CITATION: 28,
  CONTINUATION_AVAILABLE: 29,
} as const;
```

- [ ] **Step 3: Add InlineCitation types**

```typescript
export interface InlineCitationMatch {
  startOffset: number;
  endOffset: number;
  snippetIndex: number;
  pageNumber: number;
  pdfDocumentId: string;
  confidence: number;
}

export interface ContinuationData {
  continuationToken: string;
  reason: string;
}
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/api/clients/chatClient.ts
git commit -m "feat(chat): update QaStreamRequest with responseStyle and citation types"
```

---

## Task 7: Frontend — InlineCitationText component

**Files:**
- Create: `apps/web/src/components/chat-unified/InlineCitationText.tsx`
- Create: `apps/web/__tests__/components/chat-unified/InlineCitationText.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { InlineCitationText } from '@/components/chat-unified/InlineCitationText';
import type { InlineCitationMatch } from '@/lib/api/clients/chatClient';

const mockSnippets = [
  { text: 'Full chunk text from the rulebook about game setup.', source: 'PDF:abc-123', page: 3, line: 0, score: 0.9 },
];

describe('InlineCitationText', () => {
  it('renders plain text when no citations', () => {
    render(<InlineCitationText text="Hello world" citations={[]} snippets={[]} />);
    expect(screen.getByText('Hello world')).toBeInTheDocument();
  });

  it('renders highlighted span for citation', () => {
    const citations: InlineCitationMatch[] = [
      { startOffset: 0, endOffset: 5, snippetIndex: 0, pageNumber: 3, pdfDocumentId: 'abc-123', confidence: 1.0 },
    ];
    render(<InlineCitationText text="Hello world" citations={citations} snippets={mockSnippets} />);
    const highlight = screen.getByTestId('citation-highlight-0');
    expect(highlight).toBeInTheDocument();
    expect(highlight).toHaveTextContent('Hello');
  });

  it('expands accordion on click', () => {
    const citations: InlineCitationMatch[] = [
      { startOffset: 0, endOffset: 5, snippetIndex: 0, pageNumber: 3, pdfDocumentId: 'abc-123', confidence: 1.0 },
    ];
    render(<InlineCitationText text="Hello world" citations={citations} snippets={mockSnippets} />);
    fireEvent.click(screen.getByTestId('citation-highlight-0'));
    expect(screen.getByTestId('citation-accordion-0')).toBeInTheDocument();
    expect(screen.getByText(/Full chunk text from the rulebook/)).toBeInTheDocument();
  });

  it('renders PDF link icon', () => {
    const citations: InlineCitationMatch[] = [
      { startOffset: 0, endOffset: 5, snippetIndex: 0, pageNumber: 3, pdfDocumentId: 'abc-123', confidence: 1.0 },
    ];
    render(<InlineCitationText text="Hello world" citations={citations} snippets={mockSnippets} />);
    const pdfLink = screen.getByTestId('pdf-link-0');
    expect(pdfLink).toHaveAttribute('href', '/api/v1/pdfs/abc-123/download#page=3');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm test -- --run InlineCitationText`
Expected: FAIL — module not found

- [ ] **Step 3: Implement InlineCitationText**

```tsx
'use client';

import React, { useState } from 'react';
import { ExternalLink, ChevronDown, ChevronUp } from 'lucide-react';
import type { InlineCitationMatch } from '@/lib/api/clients/chatClient';
import { cn } from '@/lib/utils';

interface SnippetData {
  text: string;
  source: string;
  page: number;
  line: number;
  score: number;
}

interface InlineCitationTextProps {
  text: string;
  citations: InlineCitationMatch[];
  snippets: SnippetData[];
}

export function InlineCitationText({ text, citations, snippets }: InlineCitationTextProps) {
  const [expandedCitations, setExpandedCitations] = useState<Set<number>>(new Set());

  if (citations.length === 0) {
    return <p className="whitespace-pre-wrap break-words">{text}</p>;
  }

  const sorted = [...citations].sort((a, b) => a.startOffset - b.startOffset);
  const segments: React.ReactNode[] = [];
  let lastEnd = 0;

  sorted.forEach((citation, idx) => {
    // Text before this citation
    if (citation.startOffset > lastEnd) {
      segments.push(
        <span key={`text-${idx}`}>{text.slice(lastEnd, citation.startOffset)}</span>
      );
    }

    const citedText = text.slice(citation.startOffset, citation.endOffset);
    const isExpanded = expandedCitations.has(idx);
    const snippet = snippets[citation.snippetIndex];

    segments.push(
      <React.Fragment key={`cite-${idx}`}>
        <span
          className={cn(
            'bg-amber-500/10 border-l-2 border-amber-500 px-1 cursor-pointer',
            'hover:bg-amber-500/20 transition-colors inline'
          )}
          onClick={() => {
            setExpandedCitations(prev => {
              const next = new Set(prev);
              if (next.has(idx)) next.delete(idx);
              else next.add(idx);
              return next;
            });
          }}
          title={`Pagina ${citation.pageNumber}`}
          data-testid={`citation-highlight-${idx}`}
        >
          {citedText}
          <span className="inline-flex items-center ml-1 text-amber-600 dark:text-amber-400">
            {isExpanded ? (
              <ChevronUp className="h-3 w-3 inline" />
            ) : (
              <ChevronDown className="h-3 w-3 inline" />
            )}
          </span>
        </span>
        <a
          href={`/api/v1/pdfs/${citation.pdfDocumentId}/download#page=${citation.pageNumber}`}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center ml-0.5 text-amber-600 hover:text-amber-700 dark:text-amber-400 dark:hover:text-amber-300"
          data-testid={`pdf-link-${idx}`}
          title={`Apri PDF — Pagina ${citation.pageNumber}`}
        >
          <ExternalLink className="h-3 w-3" />
        </a>
        {isExpanded && snippet && (
          <div
            className="block my-2 p-3 rounded-lg bg-amber-500/5 border border-amber-500/20 text-sm"
            data-testid={`citation-accordion-${idx}`}
          >
            <div className="flex items-center gap-2 mb-1 text-xs text-amber-600 dark:text-amber-400 font-semibold font-nunito">
              <span>Pagina {citation.pageNumber}</span>
            </div>
            <p className="text-muted-foreground font-nunito whitespace-pre-wrap">{snippet.text}</p>
          </div>
        )}
      </React.Fragment>
    );

    lastEnd = citation.endOffset;
  });

  // Remaining text after last citation
  if (lastEnd < text.length) {
    segments.push(<span key="text-end">{text.slice(lastEnd)}</span>);
  }

  return <div className="whitespace-pre-wrap break-words">{segments}</div>;
}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm test -- --run InlineCitationText`
Expected: All 4 tests pass

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/chat-unified/InlineCitationText.tsx
git add apps/web/__tests__/components/chat-unified/InlineCitationText.test.tsx
git commit -m "feat(chat): add InlineCitationText component with TDD tests"
```

---

## Task 8: Frontend — CitationBlock component

**Files:**
- Create: `apps/web/src/components/chat-unified/CitationBlock.tsx`
- Create: `apps/web/__tests__/components/chat-unified/CitationBlock.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { CitationBlock } from '@/components/chat-unified/CitationBlock';

const mockSnippets = [
  { text: 'Setup instructions for the game.', source: 'PDF:abc-123', page: 3, line: 0, score: 0.9 },
  { text: 'Scoring rules at end of game.', source: 'PDF:abc-123', page: 5, line: 0, score: 0.8 },
];

describe('CitationBlock', () => {
  it('renders citation chips for each snippet', () => {
    render(<CitationBlock snippets={mockSnippets} excludeIndices={new Set()} />);
    expect(screen.getByText('Pagina 3')).toBeInTheDocument();
    expect(screen.getByText('Pagina 5')).toBeInTheDocument();
  });

  it('excludes snippets already shown inline', () => {
    render(<CitationBlock snippets={mockSnippets} excludeIndices={new Set([0])} />);
    expect(screen.queryByText('Pagina 3')).not.toBeInTheDocument();
    expect(screen.getByText('Pagina 5')).toBeInTheDocument();
  });

  it('expands accordion on chip click', () => {
    render(<CitationBlock snippets={mockSnippets} excludeIndices={new Set()} />);
    fireEvent.click(screen.getByText('Pagina 3'));
    expect(screen.getByText(/Setup instructions/)).toBeInTheDocument();
  });

  it('renders nothing when all snippets excluded', () => {
    const { container } = render(<CitationBlock snippets={mockSnippets} excludeIndices={new Set([0, 1])} />);
    expect(container.firstChild).toBeNull();
  });
});
```

- [ ] **Step 2: Implement CitationBlock**

```tsx
'use client';

import React, { useState } from 'react';
import { FileText, ExternalLink, ChevronDown, ChevronUp } from 'lucide-react';
import { cn } from '@/lib/utils';

interface SnippetData {
  text: string;
  source: string;
  page: number;
  line: number;
  score: number;
}

interface CitationBlockProps {
  snippets: SnippetData[];
  excludeIndices: Set<number>;
}

function extractPdfId(source: string): string {
  return source.startsWith('PDF:') ? source.slice(4) : source;
}

export function CitationBlock({ snippets, excludeIndices }: CitationBlockProps) {
  const [expanded, setExpanded] = useState<number | null>(null);

  const visible = snippets
    .map((s, i) => ({ ...s, originalIndex: i }))
    .filter(s => !excludeIndices.has(s.originalIndex));

  if (visible.length === 0) return null;

  return (
    <div className="mt-3 flex flex-wrap gap-2">
      {visible.map((snippet) => {
        const pdfId = extractPdfId(snippet.source);
        const isExpanded = expanded === snippet.originalIndex;

        return (
          <div key={snippet.originalIndex} className="flex flex-col">
            <div className="flex items-center gap-1">
              <button
                onClick={() => setExpanded(isExpanded ? null : snippet.originalIndex)}
                className={cn(
                  'inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-nunito font-medium transition-all',
                  'border border-amber-500/30 hover:border-amber-500/60',
                  isExpanded
                    ? 'bg-amber-500/15 text-amber-700 dark:text-amber-300'
                    : 'bg-amber-500/5 text-amber-600 dark:text-amber-400 hover:bg-amber-500/10'
                )}
                data-testid={`citation-chip-${snippet.originalIndex}`}
              >
                <FileText className="h-3 w-3" />
                Pagina {snippet.page}
                {isExpanded ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />}
              </button>
              <a
                href={`/api/v1/pdfs/${pdfId}/download#page=${snippet.page}`}
                target="_blank"
                rel="noopener noreferrer"
                className="text-amber-600 hover:text-amber-700 dark:text-amber-400"
                title={`Apri PDF — Pagina ${snippet.page}`}
              >
                <ExternalLink className="h-3 w-3" />
              </a>
            </div>
            {isExpanded && (
              <div className="mt-1.5 p-3 rounded-lg bg-amber-500/5 border border-amber-500/20 text-sm max-w-md">
                <p className="text-muted-foreground font-nunito whitespace-pre-wrap">{snippet.text}</p>
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 3: Run tests**

Run: `cd apps/web && pnpm test -- --run CitationBlock`
Expected: All 4 tests pass

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/chat-unified/CitationBlock.tsx
git add apps/web/__tests__/components/chat-unified/CitationBlock.test.tsx
git commit -m "feat(chat): add CitationBlock component with TDD tests"
```

---

## Task 9: Frontend — ContinueButton component

**Files:**
- Create: `apps/web/src/components/chat-unified/ContinueButton.tsx`
- Create: `apps/web/__tests__/components/chat-unified/ContinueButton.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ContinueButton } from '@/components/chat-unified/ContinueButton';

describe('ContinueButton', () => {
  it('renders with correct text', () => {
    render(<ContinueButton onContinue={() => {}} isLoading={false} />);
    expect(screen.getByText('Continua la risposta')).toBeInTheDocument();
  });

  it('calls onContinue when clicked', () => {
    const onContinue = vi.fn();
    render(<ContinueButton onContinue={onContinue} isLoading={false} />);
    fireEvent.click(screen.getByRole('button'));
    expect(onContinue).toHaveBeenCalledOnce();
  });

  it('shows loading state', () => {
    render(<ContinueButton onContinue={() => {}} isLoading={true} />);
    expect(screen.getByText('Continuando...')).toBeInTheDocument();
    expect(screen.getByRole('button')).toBeDisabled();
  });
});
```

- [ ] **Step 2: Implement ContinueButton**

```tsx
'use client';

import { ArrowRight } from 'lucide-react';
import { cn } from '@/lib/utils';

interface ContinueButtonProps {
  onContinue: () => void;
  isLoading: boolean;
}

export function ContinueButton({ onContinue, isLoading }: ContinueButtonProps) {
  return (
    <button
      onClick={onContinue}
      disabled={isLoading}
      className={cn(
        'mt-3 inline-flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-nunito font-medium transition-all',
        isLoading
          ? 'bg-muted text-muted-foreground cursor-not-allowed'
          : 'bg-amber-500/10 text-amber-700 dark:text-amber-300 border border-amber-500/30 hover:bg-amber-500/20 hover:border-amber-500/50 cursor-pointer'
      )}
      data-testid="continue-button"
    >
      {isLoading ? (
        <>
          <div className="h-3.5 w-3.5 border-2 border-amber-500/30 border-t-amber-500 rounded-full animate-spin" />
          Continuando...
        </>
      ) : (
        <>
          Continua la risposta
          <ArrowRight className="h-3.5 w-3.5" />
        </>
      )}
    </button>
  );
}
```

- [ ] **Step 3: Run tests**

Run: `cd apps/web && pnpm test -- --run ContinueButton`
Expected: All 3 tests pass

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/chat-unified/ContinueButton.tsx
git add apps/web/__tests__/components/chat-unified/ContinueButton.test.tsx
git commit -m "feat(chat): add ContinueButton component with TDD tests"
```

---

## Task 10: Integrate new components in ChatMobile and ChatThreadView

**Files:**
- Modify: `apps/web/src/components/chat-unified/ChatMobile.tsx`
- Modify: `apps/web/src/components/chat-unified/ChatThreadView.tsx`

- [ ] **Step 1: Add imports in ChatMobile**

```typescript
import { InlineCitationText } from './InlineCitationText';
import { CitationBlock } from './CitationBlock';
import { ContinueButton } from './ContinueButton';
import { QA_EVENT_TYPES, type InlineCitationMatch, type ContinuationData } from '@/lib/api/clients/chatClient';
```

- [ ] **Step 2: Add citation state to ChatMobile**

Add to LocalMessage interface:

```typescript
interface LocalMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
  citations?: CitationItem[];
  followUpQuestions?: string[];
  inlineCitations?: InlineCitationMatch[];
  snippets?: Array<{ text: string; source: string; page: number; line: number; score: number }>;
  continuationToken?: string;
}
```

- [ ] **Step 3: Handle events 28 and 29 in ChatMobile qaStream handler**

In the `for await` loop of the qaStream path, add cases:

```typescript
case QA_EVENT_TYPES.INLINE_CITATION: {
  const data = event.data as { citations: InlineCitationMatch[] };
  if (data.citations) {
    setMessages(prev =>
      prev.map(m =>
        m.id === assistantMsgId ? { ...m, inlineCitations: data.citations } : m
      )
    );
  }
  break;
}
case QA_EVENT_TYPES.CONTINUATION_AVAILABLE: {
  const data = event.data as ContinuationData;
  if (data.continuationToken) {
    setMessages(prev =>
      prev.map(m =>
        m.id === assistantMsgId ? { ...m, continuationToken: data.continuationToken } : m
      )
    );
  }
  break;
}
```

Also save snippets from the Citations event (type 1):

```typescript
case QA_EVENT_TYPES.CITATIONS: {
  const data = event.data as { snippets?: Array<{ text: string; source: string; page: number; line: number; score: number }> };
  if (data.snippets) {
    setMessages(prev =>
      prev.map(m =>
        m.id === assistantMsgId ? { ...m, snippets: data.snippets, citations: data.snippets as unknown as CitationItem[] } : m
      )
    );
  }
  break;
}
```

- [ ] **Step 4: Update MessageBubble to use InlineCitationText**

In `ChatMobile.tsx`, update the assistant message rendering in `MessageBubble`:

```tsx
function MessageBubble({ message, onContinue, isContinuing }: { message: LocalMessage; onContinue?: (token: string) => void; isContinuing?: boolean }) {
  const isUser = message.role === 'user';
  const hasInlineCitations = !isUser && message.inlineCitations && message.inlineCitations.length > 0;
  const inlineSnippetIndices = new Set(message.inlineCitations?.map(c => c.snippetIndex) ?? []);

  return (
    <div className={cn('flex', isUser ? 'justify-end' : 'justify-start')}>
      <div className={cn('max-w-[85%] rounded-2xl px-4 py-2.5 text-sm font-nunito', /* existing styles */)}>
        {hasInlineCitations ? (
          <InlineCitationText
            text={message.content}
            citations={message.inlineCitations!}
            snippets={message.snippets ?? []}
          />
        ) : (
          <p className="whitespace-pre-wrap break-words">{message.content}</p>
        )}
        {/* Citation block for non-inline snippets */}
        {!isUser && message.snippets && message.snippets.length > 0 && (
          <CitationBlock snippets={message.snippets} excludeIndices={inlineSnippetIndices} />
        )}
        {/* Continue button */}
        {!isUser && message.continuationToken && onContinue && (
          <ContinueButton
            onContinue={() => onContinue(message.continuationToken!)}
            isLoading={isContinuing ?? false}
          />
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 5: Add continuation handler in ChatMobile**

```typescript
const handleContinue = useCallback(
  (continuationToken: string) => {
    void (async () => {
      setIsSending(true);
      const abortController = new AbortController();
      qaAbortRef.current = abortController;
      try {
        // Find the message to append to
        const lastAssistant = messages.findLast(m => m.role === 'assistant');
        if (!lastAssistant) return;
        let appendedContent = lastAssistant.content;

        for await (const event of qaStream(
          { gameId: thread!.gameId!, query: '', continuationToken },
          abortController.signal
        )) {
          if (event.type === QA_EVENT_TYPES.TOKEN) {
            const token = typeof event.data === 'string' ? event.data : ((event.data as { token?: string })?.token ?? '');
            if (token) {
              appendedContent += token;
              const content = appendedContent;
              setMessages(prev =>
                prev.map(m => m.id === lastAssistant.id ? { ...m, content, continuationToken: undefined } : m)
              );
            }
          }
        }
      } catch { /* handled */ }
      finally {
        setIsSending(false);
        qaAbortRef.current = null;
      }
    })();
  },
  [messages, thread?.gameId]
);
```

- [ ] **Step 6: Pass responseStyle in qaStream calls**

Update the qaStream call to include `responseStyle: 'concise'`:

```typescript
for await (const event of qaStream(
  { gameId: thread.gameId!, query: text, chatId: threadId, responseStyle: 'concise' },
  abortController.signal
))
```

- [ ] **Step 7: Apply same changes to ChatThreadView**

Repeat steps 1-6 for `ChatThreadView.tsx` — same pattern, same components, same event handling.

- [ ] **Step 8: Typecheck**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 9: Commit**

```bash
git add apps/web/src/components/chat-unified/ChatMobile.tsx
git add apps/web/src/components/chat-unified/ChatThreadView.tsx
git commit -m "feat(chat): integrate inline citations, citation block, and continue button"
```

---

## Task 11: Final integration test and cleanup

- [ ] **Step 1: Build backend**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 2: Run backend unit tests**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "Category=Unit" --no-restore`
Expected: All tests pass

- [ ] **Step 3: Run frontend tests**

Run: `cd apps/web && pnpm test -- --run`
Expected: All tests pass

- [ ] **Step 4: Typecheck frontend**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 5: Final commit if needed**

```bash
git add -A && git commit -m "chore: final cleanup for inline citations feature"
```
