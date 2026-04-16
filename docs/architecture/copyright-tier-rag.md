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

### Layer 3 — DTO-level gate (`ChatWithSessionAgentCommandHandler`)

File: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs`

When serializing the SSE `StreamingComplete` event, `CitationDto.SnippetPreview` is nullified for `CopyrightTier.Protected` entries. Frontend (`RuleSourceCard`, `CitationSheet`) shows `ParaphrasedSnippet` extracted from the LLM response via `[ref:documentId:pageNum]` markers.

## Known limits

- `SnippetPreview` is truncated to 120 characters at chunk retrieval time (`RagPromptAssemblyService:379`); full chunk text is preserved in-memory via `ChunkCitation.FullText [JsonIgnore]`.
- `ParaphraseExtractor.ComputeOverlap` uses word-set similarity (threshold 0.7), not consecutive-run matching.
- No retroactive scan on historical messages (FullText is in-memory only, not persisted).

## Related issues

- #446 — Refactor: wire agent language
- #447 — Layer 3 response-body scan (TO BE ADDED to this doc when integrated)
- #448 — Future enhancements (glossary, streaming-aware buffer, legal workflow)
