# ChatMessage API Compatibility Report (#292 Spike)

**Date:** 2026-04-09
**Spike branch:** `chore/issue-292-spike`
**Issue:** meepleAi-app/meepleai-monorepo#292
**Follow-up plan:** `docs/development/orphan-components-follow-up-plan-v2.md` Phase A.0

## Executive summary

Refactoring `ChatMessageList` to compose with `<ChatMessage>` is **feasible** but requires a **new refactor strategy (Option ε)** not covered by the v2 plan's original α/β/γ options.

**TL;DR:**
- `<ChatMessage>` is self-contained and renders avatar + bubble + content + confidence + agentType + citations (its own way) + feedback internally
- **Citation types are incompatible** between `@/types.Citation` (RAG) and `ChatMessage.Citation` (simple)
- **Option ε (recommended):** Use `<ChatMessage>` for role/content/feedback/avatar/timestamp. Keep `<RuleSourceCard>` (citations) + context-dependent pieces (TechnicalDetailsPanel, ResponseMetaBadge, streaming bubble, status, model downgrade) **outside** `<ChatMessage>` in the orchestrator.
- **Revised LOC target:** `ChatMessageList.tsx` → ~200 lines (not 150). The gain is real (~35% reduction) but less dramatic than v1/v2 estimated.

## Field-by-field comparison

### `ChatMessageItem` (source, `chat-unified/ChatMessageList.tsx`)

```ts
interface ChatMessageItem {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp?: string;
  citations?: import('@/types').Citation[];
  followUpQuestions?: string[];
}
```

### `ChatMessageProps` (target, `ui/meeple/chat-message.tsx`)

```ts
interface ChatMessageProps {
  role: 'user' | 'assistant';
  content: string;
  agentType?: 'tutor' | 'arbitro' | 'stratega' | 'narratore';
  confidence?: number;
  citations?: Citation[]; // local Citation type — INCOMPATIBLE with @/types
  timestamp?: string | Date;
  avatar?: { src?: string; fallback: string };
  isTyping?: boolean;
  className?: string;
  onCitationClick?: (documentId: string, pageNumber: number) => void;
  feedback?: FeedbackValue;
  onFeedbackChange?: (feedback: FeedbackValue, comment?: string) => Promise<void>;
  isFeedbackLoading?: boolean;
  showFeedback?: boolean;
}
```

### Mapping table

| Field | ChatMessageItem (source) | ChatMessageProps (target) | Adapter action |
|---|---|---|---|
| `role` | ✅ `'user' \| 'assistant'` | ✅ same | 1:1 pass-through |
| `content` | ✅ `string` | ✅ `string` | 1:1 |
| `timestamp` | ✅ `string?` | ✅ `string \| Date?` | 1:1 |
| `agentType` | ❌ not in source | ✅ optional | **SKIP** (undefined — adapter leaves it out; follow-up to wire if backend exposes per-message agent type) |
| `confidence` | ❌ not in source | ✅ optional (0-100) | **SKIP** (undefined — ChatMessage shows no confidence badge; acceptable degradation) |
| `citations` | ✅ `@/types.Citation[]?` | ⚠️ `ChatMessage.Citation[]?` (**INCOMPATIBLE**) | **DO NOT PASS** — keep RuleSourceCard in orchestrator |
| `avatar` (user) | ❌ not in source | ✅ `{ src?, fallback }` | **HARDCODED** — `{ fallback: 'U' }` for now (backend doesn't expose user avatar per message) |
| `isTyping` | ❌ not in source | ✅ | **SKIP** (ChatMessageList has separate streaming bubble, not per-message typing) |
| `onCitationClick` | ❌ not in source | ✅ | **SKIP** (citations stay outside) |
| `feedback` | ✅ derived from `feedbackMap.get(msg.id) ?? null` | ✅ `FeedbackValue` | via `ctx` param |
| `onFeedbackChange` | ✅ curry of `handleFeedback(msg.id, ...)` | ✅ `(v, c) => Promise<void>` | via `ctx` param (closure per message) |
| `isFeedbackLoading` | ✅ `feedbackLoadingMap.get(msg.id) ?? false` | ✅ `boolean` | via `ctx` param |
| `showFeedback` | ✅ computed: `msg.role === 'assistant' && !!gameId && !!threadId` | ✅ `boolean` | via `ctx` param |

### Citation types (the critical incompatibility)

```ts
// apps/web/src/types/domain.ts (used by ChatMessageList + RuleSourceCard)
interface Citation {
  documentId: string;
  pageNumber: number;
  snippet: string;
  relevanceScore: number;
  copyrightTier: 'full' | 'protected';
  paraphrasedSnippet?: string;
  isPublic?: boolean;
}

// apps/web/src/components/ui/meeple/chat-message.tsx (local to ChatMessage)
interface Citation {
  id: string;
  label: string;
  page?: number;
  source?: string;
}
```

These are **different domain concepts**:
- `@/types.Citation` is a **RAG citation** with relevance scoring + copyright tier
- `ChatMessage.Citation` is a **simple reference** label

A naive mapping (`id ← documentId`, `label ← snippet.slice(0,50)`, `page ← pageNumber`) **would lose**:
- `relevanceScore` (ranking info for UX)
- `copyrightTier` (legal gate: Full/Protected rendering)
- `paraphrasedSnippet` (AI-generated alternative for protected tier)
- `isPublic` (upsell CTA copy)

All of these are rendered by `<RuleSourceCard>` in production. Replacing `<RuleSourceCard>` with `<ChatCitationLink>` (ChatMessage's internal citation rendering) would be a **functional regression**.

**Decision:** do NOT pass citations to ChatMessage. Keep `<RuleSourceCard>` rendering in the orchestrator.

## Feedback slot analysis

**Does ChatMessage render feedback buttons internally?** ✅ Yes (lines 304-315 of chat-message.tsx).

**Conditions:** `shouldShowFeedback = isAssistant && showFeedback && !isTyping && onFeedbackChange`

**Wiring:** ChatMessage imports and renders `<FeedbackButtons value={feedback} onFeedbackChange={onFeedbackChange} isLoading={isFeedbackLoading} showCommentOnNegative size="sm" />` — exactly the same component and props as ChatMessageList uses inline today.

**Implication:** The adapter can pass `feedback` + `onFeedbackChange` + `isFeedbackLoading` + `showFeedback` and ChatMessage will render the feedback UI. **Orchestrator should NOT render FeedbackButtons separately** — that would be a duplicate.

**Edge case:** `handleFeedback` in ChatMessageList currently has signature `(messageId, value, comment?)`. ChatMessage expects `(value, comment?) => Promise<void>`. The adapter needs to curry:

```ts
const onFeedbackChange = async (value: FeedbackValue, comment?: string) => {
  await handleFeedback(msg.id, value, comment);
};
```

One closure per message in the `.map()` call.

## MeepleAvatar slot analysis

**Does ChatMessage render MeepleAvatar internally?** ✅ Yes (line 245).

**State derivation:** ChatMessage **computes the avatar state internally** via `getAvatarState(confidence, isTyping)`:
- `isTyping` → `'thinking'`
- `confidence === undefined` → `'idle'`
- `confidence >= 85` → `'confident'`
- `confidence >= 70` → `'searching'`
- `confidence < 70` → `'uncertain'`

**Implication:** The adapter does **not** need to pass avatar state. Since `ChatMessageItem` doesn't expose `confidence`, the avatar will render as `'idle'` for all assistant messages.

**Future improvement:** If the backend exposes per-message confidence (e.g., from `streamState.strategyTier` or a new field on `ChatMessageItem`), wire it through the adapter. Not in scope for #292.

**v2 plan's `deriveMeepleAvatarState` helper is UNNECESSARY** — skip Phase A.2.4 entirely.

## Refactor strategy decision: Option ε

### What v2 plan offered (none fit cleanly):

- **Option α** — "ChatMessage compatible as-is, pure composition": ❌ Citation incompatibility + missing confidence field
- **Option β** — "Extend ChatMessage with new props": ❌ Unnecessary (ChatMessage already has the slots we need); would just rename existing props
- **Option γ** — "Keep feedback outside ChatMessage": ❌ Wastes ChatMessage's internal FeedbackButtons wiring

### New option ε (adopted):

**"Adapter with context-dependent children in orchestrator, citations preserved outside"**

1. **Inside `<ChatMessage>`** (via adapter):
   - `role`, `content`, `timestamp`
   - `feedback`, `onFeedbackChange`, `isFeedbackLoading`, `showFeedback`
   - `avatar: { fallback: 'U' }` (for user messages)
2. **Outside `<ChatMessage>`** (in orchestrator):
   - `<RuleSourceCard>` for citations (preserves RAG/copyright handling)
   - `<TechnicalDetailsPanel>` (editor-only, last assistant, debug steps)
   - `<ResponseMetaBadge>` (last assistant, strategy tier, not streaming)
   - Streaming bubble (separate component path)
   - Status message + model downgrade banner (list-level, not per-message)
3. **Skipped entirely** (not worth the effort for #292):
   - `confidence` (requires backend change)
   - `agentType` per message (requires backend change)
   - `isTyping` (ChatMessageList has a separate streaming bubble, not per-message)
   - User avatar src (backend doesn't expose per-message)
   - Phase A.2.4 MeepleAvatar derivation (avatar state is internal to ChatMessage)

## Pseudo-diff (for Phase A.2.3)

### BEFORE (current ChatMessageList.tsx inline block, ~70 lines per message)

```tsx
{windowedMessages.map((msg, localIdx) => {
  const absoluteIdx = windowStart + localIdx;
  const isLastAssistant = /* inline logic */;
  return (
    <div key={msg.id} className={cn(/* role-based classes */)}>
      {/* Avatar (user vs assistant) */}
      {/* Message bubble with content */}
      {/* Confidence badge (unused — ChatMessageItem has no confidence) */}
      {/* Citations via RuleSourceCard */}
      {/* Feedback buttons (inline) */}
      {/* Technical details panel */}
      {/* Strategy tier badge */}
      {/* TTS speaker button */}
    </div>
  );
})}
```

### AFTER (Option ε, ~25 lines per message)

```tsx
{windowedMessages.map((msg, localIdx) => {
  const absoluteIdx = windowStart + localIdx;
  const isLast = isLastAssistantMessage(messages, absoluteIdx);
  return (
    <React.Fragment key={msg.id}>
      <ChatMessage {...toChatMessageProps(msg, {
        feedback: feedbackMap.get(msg.id) ?? null,
        isFeedbackLoading: feedbackLoadingMap.get(msg.id) ?? false,
        showFeedback: !!gameId && !!threadId,
        onFeedbackChange: async (value, comment) => {
          await handleFeedback(msg.id, value, comment);
        },
      })} />

      {/* Citations: kept OUTSIDE ChatMessage to preserve RuleSourceCard */}
      {msg.role === 'assistant' && msg.citations && msg.citations.length > 0 && (
        <RuleSourceCard citations={msg.citations} gameTitle={gameTitle} />
      )}

      {/* Context-dependent children (only on last assistant message) */}
      {isLast && isEditor && streamState.debugSteps.length > 0 && (
        <TechnicalDetailsPanel
          debugSteps={streamState.debugSteps}
          executionId={streamState.executionId}
          showDebugLink={isAdmin}
        />
      )}
      {isLast && streamState.strategyTier && !streamState.isStreaming && (
        <ResponseMetaBadge strategyTier={streamState.strategyTier} />
      )}

      {/* TTS speaker button (if supported + enabled) */}
      {msg.role === 'assistant' && isTtsSupported && ttsEnabled && (
        <TtsSpeakerButton text={msg.content} />
      )}
    </React.Fragment>
  );
})}
```

## Impact on target LOC

| Metric | Current (post PR #307) | After Option ε |
|---|---|---|
| Total LOC `ChatMessageList.tsx` | 311 | **~200** (−35%) |
| Per-message inline JSX | ~70 lines | ~25 lines |
| Per-message JSX (non-count) | ~50 lines | ~15 lines |

**Revised target: ≤ 210 lines** (down from v2's 150 target — the citation + context-dependent rendering is unavoidable).

**v1/v2 estimate was wrong** because it assumed the orchestrator could delegate all per-message rendering to ChatMessage. The citation incompatibility + context-dependent pieces (TechnicalDetailsPanel, ResponseMetaBadge) prevent full delegation.

## Adapter contract (for Phase A.2.2)

```ts
// apps/web/src/components/chat-unified/utils/toChatMessageProps.ts

import type { ChatMessageItem } from '../ChatMessageList';
import type { ChatMessageProps } from '@/components/ui/meeple/chat-message';
import type { FeedbackValue } from '@/components/ui/meeple/feedback-buttons';

export interface ToChatMessagePropsContext {
  /** Current feedback value for this message (from feedbackMap) */
  feedback: FeedbackValue;
  /** Whether feedback submission is in-flight for this message */
  isFeedbackLoading: boolean;
  /** Whether feedback buttons should be visible (gameId && threadId truthy) */
  showFeedback: boolean;
  /** Curried feedback handler with messageId bound */
  onFeedbackChange: (value: FeedbackValue, comment?: string) => Promise<void>;
}

export function toChatMessageProps(
  item: ChatMessageItem,
  ctx: ToChatMessagePropsContext
): ChatMessageProps {
  const base: ChatMessageProps = {
    role: item.role,
    content: item.content,
    timestamp: item.timestamp,
    feedback: ctx.feedback,
    isFeedbackLoading: ctx.isFeedbackLoading,
    showFeedback: ctx.showFeedback,
    onFeedbackChange: ctx.onFeedbackChange,
  };

  if (item.role === 'user') {
    base.avatar = { fallback: 'U' };
  }

  // NOTE: citations deliberately NOT passed — kept outside as <RuleSourceCard>
  // NOTE: confidence/agentType not yet available from ChatMessageItem
  // NOTE: isTyping is handled separately via the streaming bubble, not per message

  return base;
}
```

## Impact on Phase A.2 plan

| Phase | Status after spike |
|---|---|
| A.2.1 `isLastAssistantMessage` helper | ✅ Unchanged — still needed |
| A.2.2 Adapter `toChatMessageProps` | ✅ Changed — see contract above |
| A.2.3 Replace inline message block | ✅ Changed — see pseudo-diff above |
| A.2.4 MeepleAvatar state wiring | ❌ **SKIP** — ChatMessage derives avatar state internally from confidence; ChatMessageItem doesn't expose confidence so we leave it `undefined` and avatar is `'idle'` |
| A.2.5 Cleanup orphan annotations | ✅ Changed — only `chat-message.tsx` gets promoted. `meeple-avatar.tsx` stays orphan (it's only imported by `chat-message.tsx`, which IS now consumed, so it's transitively consumed too — remove its `@status ORPHAN` annotation as well) |

## Risk assessment

| Risk | Severity | Mitigation |
|---|---|---|
| ChatMessage's internal `<p className="... whitespace-pre-wrap">` differs from current ChatMessageList content rendering | LOW | visually identical; whitespace test from A.1 covers it |
| ChatMessage's bubble style (`bg-muted text-muted-foreground` for assistant, `bg-primary/10 text-foreground ml-auto` for user) may differ from current styling | MEDIUM | Manual smoke test + screenshot comparison in Phase A.3 |
| Confidence badge disappears (ChatMessageItem has no confidence) | **ACCEPTED** | Production currently doesn't show confidence per message either — the badge would only appear if we wire it; leaving as-is is no regression |
| Citations render in RuleSourceCard outside the bubble visual grouping | LOW | Current behavior is identical (ChatMessageList already renders RuleSourceCard outside the bubble wrapper in most cases) |
| `avatar: { fallback: 'U' }` hardcoded for all user messages | LOW | Same as current (ChatMessageList doesn't personalize user avatars either) |

## Recommendation

**Proceed with Option ε** for Phase A.2. Revised LOC target: ≤ 210 lines. Phase A.2.4 (MeepleAvatar wiring) is **skipped**. Phase A.2.5 promotes **both** `chat-message.tsx` and `meeple-avatar.tsx` from orphan (meeple-avatar becomes transitively consumed).

## Checklist

- [x] ChatMessage API fully documented
- [x] Citation incompatibility identified and resolved (keep RuleSourceCard outside)
- [x] Feedback slot wiring confirmed (pass via adapter, orchestrator does NOT duplicate)
- [x] Avatar state derivation confirmed internal to ChatMessage (skip A.2.4)
- [x] Revised LOC target set (210, not 150)
- [x] Adapter contract written
- [x] Pseudo-diff for A.2.3 written
- [x] Skipped fields documented (`confidence`, `agentType`, `isTyping`, user avatar src)
- [x] Phase A.2.4 marked as SKIP
- [x] Risk register updated

## Next steps

1. Merge this spike report (Phase A.0 done)
2. Execute Phase A.1 — characterization tests (parallel-safe)
3. Execute Phase A.2 — refactor per Option ε (sequential: A.2.1 → A.2.2 → A.2.3 → A.2.5; skip A.2.4)
4. Execute Phase A.3 — PR with architect review (human gate)
