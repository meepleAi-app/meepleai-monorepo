# Copyright Tier in RAG Pipeline

## Overview

MeepleAI's RAG pipeline applies a multi-layer copyright defense to prevent verbatim leak of protected content (e.g., third-party board game rulebooks) to users who have not declared ownership of the game.

**Compliance posture (alpha):** internal policy only. No legal framework contracts active. Pre-distribution, fail-open priority over strict compliance. Future posture managed via issue #448.

## Defense Layers

### Layer 1 — Tier resolution (`CopyrightTierResolver`)

File: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/CopyrightTierResolver.cs`

Cascade rules applied per retrieved chunk:

1. **Copyright-free license** (CreativeCommons, PublicDomain) → `Full`
2. **Non-protected document category** (not Rulebook/Expansion/Errata) → `Full`
3. **User uploaded AND owns the game** (both conditions) → `Full`
4. **Default** → `Protected`

Ownership check: `CopyrightDataProjection.CheckOwnershipAsync` filters on `OwnershipDeclaredAt != null`.

### Layer 2 — Prompt-level gate (`RagPromptAssemblyService`)

File: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs`

When at least one retrieved chunk has `CopyrightTier.Protected`, the system prompt includes a conditional `## Copyright Notice` block instructing the LLM to paraphrase Protected content. Chunks are annotated in the prompt with `Copyright: FULL` or `Copyright: PROTECTED` for LLM self-awareness.

Instruction localization follows `AgentDefinition.ChatLanguage` (ISO 639-1, `"auto"` normalized to `"it"` in alpha).

### Layer 3 — DTO sanitization (`ChatWithSessionAgentCommandHandler`)

File: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs`

When serializing the SSE `StreamingComplete` event, `CitationDto.SnippetPreview` is nullified for `CopyrightTier.Protected` entries. Frontend (`RuleSourceCard`, `CitationSheet`) shows `ParaphrasedSnippet` extracted from the LLM response via `[ref:documentId:pageNum]` markers.

### Layer 4 — Response-body gate (`ICopyrightLeakGuard`)

File: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/NgramCopyrightLeakGuard.cs`

After the LLM stream completes and before citation DTO serialization, `ChatWithSessionAgentCommandHandler` invokes `ICopyrightLeakGuard.ScanAsync` passing the full response body and the subset of citations with `CopyrightTier=Protected`. The default implementation `NgramCopyrightLeakGuard` performs case-insensitive, Unicode-punctuation-normalized token comparison seeking consecutive runs of `VerbatimRunThreshold` (default 12) tokens matching any Protected chunk's `FullText`. The reported `RunLength` reports the actual extended match length, not the threshold, for observability calibration.

**Failure posture (alpha):** fail-open. Scan errors are logged and incremented to `copyright.guard.scan_errors` counter; the response is allowed through. Post-alpha, configure `FailureMode=FailClosed` via `CopyrightLeakGuardOptions` (requires #448 implementation of the switch).

**Recovery action:** on `HasLeak=true`, emit SSE event `StreamingEventType.CopyrightSanitized` (id 27) with the localized fallback message from `ICopyrightFallbackMessageProvider`. The response body persisted in `ChatMessage.CitationsJson` is the fallback; the LLM-streamed tokens were already emitted to the client (see Known limits below).

**Configuration (`appsettings.json` → `Copyright` section):**

| Key | Default | Meaning |
|---|---|---|
| `VerbatimRunThreshold` | 12 | Minimum consecutive-word run length to flag as leak |
| `ScanTimeoutMs` | 500 | Maximum ms allowed per scan before cancellation |
| `FailureMode` | `FailOpen` | Reserved for #448 — currently unused beyond logging |
| `RecoveryAction` | `FallbackCanned` | Reserved for #448 — currently hardcoded fallback |

**Observability:**

| Metric | Type | Tags |
|---|---|---|
| `meepleai.rag.copyright.instruction.injected` | counter | has_protected, agent_language |
| `meepleai.rag.copyright.verbatim_run.detected` | counter | run_length, document_id |
| `meepleai.rag.copyright.guard.scan_errors` | counter | error_type |
| `meepleai.rag.copyright.guard.scan_duration` | histogram | _(none)_ |

## Known limits

- `SnippetPreview` is truncated to 120 characters at chunk retrieval time (`RagPromptAssemblyService:379`); full chunk text is preserved in-memory via `ChunkCitation.FullText [JsonIgnore]`.
- `ParaphraseExtractor.ComputeOverlap` uses word-set similarity (threshold 0.7), not consecutive-run matching.
- No retroactive scan on historical messages (FullText is in-memory only, not persisted).
- LLM-streamed tokens are emitted to client **before** the post-stream scan. `CopyrightSanitized` SSE event signals client to replace the rendered body. A frontend handler for this event is a follow-up issue out of this PR's scope.
- No game canonical glossary whitelist — phrases like "Terraforming Rating" may produce false positives. Tracked in #448 C1.

## Related issues

- #446 — Refactor: wire agent language (closed)
- #447 — Layer 4 response-body scan (this PR)
- #448 — Future enhancements (glossary, streaming-aware buffer, legal workflow)
