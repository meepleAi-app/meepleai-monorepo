# Orphan Components Follow-up Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Sections A and C use checkbox (`- [ ]`) syntax for tracking. Section B requires a human decision before execution.

**Goal:** Resolve the 3 pieces of residual work left after the main orphan components execution plan (PRs #298/#303/#304/#305/#306/#307 merged 2026-04-09).

**Scope:** 3 independent tracks — they can be executed in any order or in parallel by different engineers.

**Tech Stack:** Next.js 16 + React 19 + Vitest (frontend), .NET 9 (backend), GitHub Actions CI.

---

## Track Summary

| Track | Ticket | Effort | Risk | Prerequisites | Current Owner |
|---|---|---|---|---|---|
| **A** | meepleAi-app/meepleai-monorepo#292 | L (16-20h) | HIGH | Characterization safety net partially done | TBD |
| **B** | meepleAi-app/meepleai-monorepo#294 | S-M (decision + execution) | LOW-MEDIUM | **Product/backend decision required** | TBD |
| **C** | meepleAi-app/meepleai-monorepo#293 | S (4-6h) | LOW | Deploy staging access | TBD |

---

# Track A — #292 ChatMessageList Refactor (Monolithic)

**Issue:** meepleAi-app/meepleai-monorepo#292
**Branch:** `feature/issue-292-chatmessagelist-refactor`
**Parent:** `main-dev`

**Current state:**
- ✅ 9 characterization tests added in PR #307 (5 CRITICAL feedback + 2 streaming + 2 visibility)
- ⏳ 12 characterization tests pending (7 HIGH + 5 MEDIUM)
- ⏳ Refactor itself (311 → ~150 lines)

**Why monolithic:** User explicitly chose "monolithic" over "split into 3 sub-tickets" during the autonomous execution planning. One branch, one PR, one review.

**Exclusive lock:** No other PR may touch `chat-unified/*` during execution (Newman recommendation from spec-panel review).

**Safety net:** The refactor commit MUST pass all 21 characterization tests (9 existing + 12 new) before merge. Any regression = revert.

## Phase A.1 — Complete characterization test suite (12 tests)

**Reference:** `docs/development/chat-message-list-behavior-baseline.md` (full list with file:line citations)

### A.1.1 — HIGH priority tests (7)

**File:** `apps/web/src/components/chat-unified/__tests__/ChatMessageList.test.tsx`

- [ ] **Step 1: Write test `model_downgrade_local_fallback_banner`**

```tsx
// Add to describe block 'ChatMessageList — model downgrade characterization'
it('renders model downgrade banner with isLocalFallback flag', () => {
  render(
    <ChatMessageList
      {...defaultProps}
      messages={[]}
      streamState={{
        ...baseStream,
        modelDowngrade: {
          isLocalFallback: true,
          originalModel: 'gpt-4o',
          fallbackModel: 'llama3:8b',
          upgradeMessage: false,
          reason: 'rate_limited',
        },
      }}
    />
  );
  const banner = screen.getByTestId('model-downgrade-banner');
  expect(banner).toHaveAttribute('role', 'alert');
  expect(banner).toHaveTextContent(/llama3:8b/);
});
```

- [ ] **Step 2: Run test to confirm GREEN (characterization — current behavior is correct)**

```bash
pnpm -C apps/web test ChatMessageList --run
```

- [ ] **Step 3: Write test `model_downgrade_upgrade_message`**

```tsx
it('renders premium upgrade link when upgradeMessage=true', () => {
  render(
    <ChatMessageList
      {...defaultProps}
      messages={[]}
      streamState={{
        ...baseStream,
        modelDowngrade: {
          isLocalFallback: false,
          originalModel: 'gpt-4o',
          fallbackModel: 'gpt-3.5-turbo',
          upgradeMessage: true,
          reason: 'rate_limited',
        },
      }}
    />
  );
  const link = screen.getByRole('link', { name: /premium/i });
  expect(link).toHaveAttribute('href', '/pricing');
});
```

- [ ] **Step 4: Write test `tts_speaker_button_conditional_render`**

First, update the TtsSpeakerButton mock to be observable:

```tsx
// Replace vi.mock('../TtsSpeakerButton', ...) at top of file
vi.mock('../TtsSpeakerButton', () => ({
  TtsSpeakerButton: ({ text }: { text: string }) => (
    <button type="button" data-testid="tts-speaker" data-text={text}>
      speak
    </button>
  ),
}));
```

Then the test:

```tsx
it('renders TTS speaker button only when isTtsSupported + ttsEnabled both true', () => {
  const msg: ChatMessageItem = { id: 'a', role: 'assistant', content: 'Ciao' };

  const { rerender } = render(
    <ChatMessageList
      {...defaultProps}
      messages={[msg]}
      isTtsSupported={false}
      ttsEnabled={false}
    />
  );
  expect(screen.queryByTestId('tts-speaker')).toBeNull();

  rerender(
    <ChatMessageList
      {...defaultProps}
      messages={[msg]}
      isTtsSupported={true}
      ttsEnabled={false}
    />
  );
  expect(screen.queryByTestId('tts-speaker')).toBeNull();

  rerender(
    <ChatMessageList
      {...defaultProps}
      messages={[msg]}
      isTtsSupported={true}
      ttsEnabled={true}
    />
  );
  expect(screen.getByTestId('tts-speaker')).toHaveAttribute('data-text', 'Ciao');
});
```

- [ ] **Step 5: Write test `citations_multiple_per_message`**

Update RuleSourceCard mock to be observable:

```tsx
vi.mock('../RuleSourceCard', () => ({
  RuleSourceCard: ({ citations }: { citations: Array<{ id: string }> }) => (
    <div data-testid="rule-source-card" data-count={citations.length}>
      {citations.map(c => (
        <span key={c.id}>{c.id}</span>
      ))}
    </div>
  ),
}));
```

Test:

```tsx
it('passes all citations to RuleSourceCard (not just first)', () => {
  const msg: ChatMessageItem = {
    id: 'a',
    role: 'assistant',
    content: 'Risposta con fonti',
    citations: [
      { id: 'cit-1', label: 'Regolamento §1', page: 1 },
      { id: 'cit-2', label: 'Regolamento §2', page: 2 },
      { id: 'cit-3', label: 'Regolamento §3', page: 3 },
    ] as ChatMessageItem['citations'],
  };
  render(<ChatMessageList {...defaultProps} messages={[msg]} gameTitle="Catan" />);

  const card = screen.getByTestId('rule-source-card');
  expect(card).toHaveAttribute('data-count', '3');
});
```

- [ ] **Step 6: Write test `last_assistant_message_strategy_tier_badge`**

Update ResponseMetaBadge mock:

```tsx
vi.mock('../ResponseMetaBadge', () => ({
  ResponseMetaBadge: ({ strategyTier }: { strategyTier: string | null }) => (
    <span data-testid="strategy-badge" data-tier={strategyTier ?? 'null'} />
  ),
}));
```

Test:

```tsx
it('renders ResponseMetaBadge only on last assistant message when not streaming', () => {
  const messages: ChatMessageItem[] = [
    { id: 'a1', role: 'assistant', content: 'First' },
    { id: 'u1', role: 'user', content: 'Question' },
    { id: 'a2', role: 'assistant', content: 'Second (last)' },
  ];
  render(
    <ChatMessageList
      {...defaultProps}
      messages={messages}
      streamState={{ ...baseStream, strategyTier: 'Balanced' }}
    />
  );

  const badges = screen.getAllByTestId('strategy-badge');
  // Only the last assistant message should have the badge rendered
  expect(badges).toHaveLength(1);
  expect(badges[0]).toHaveAttribute('data-tier', 'Balanced');
});
```

- [ ] **Step 7: Write test `feedback_buttons_only_on_assistant`**

Already covered by PR #307's `does NOT render feedback buttons on user messages`. **Skip this — duplicate.**

- [ ] **Step 8: Write test `window_slide_exact_boundary_51_messages`**

```tsx
it('handles exact WINDOW_SIZE+1 boundary (51 messages)', () => {
  const messages = makeMessages(51);
  render(<ChatMessageList {...defaultProps} messages={messages} />);
  // 50 visible, 1 hidden
  expect(screen.getAllByTestId(/^message-/).length).toBe(50);
  expect(screen.getByText('Messaggio numero 50')).toBeInTheDocument();
  expect(screen.queryByText('Messaggio numero 0')).toBeNull();
  // "1 messaggi precedenti" button visible
  expect(screen.getByRole('button', { name: /1 messaggi? precedenti/ })).toBeInTheDocument();
});
```

- [ ] **Step 9: Run all tests — expect GREEN**

```bash
pnpm -C apps/web test ChatMessageList --run
# Expected: 15 (from PR #307) + 6 (new HIGH) = 21 tests passing
```

- [ ] **Step 10: Commit HIGH priority tests**

```bash
git checkout -b feature/issue-292-chatmessagelist-refactor
git config branch.feature/issue-292-chatmessagelist-refactor.parent main-dev
git add apps/web/src/components/chat-unified/__tests__/ChatMessageList.test.tsx
git commit -m "test(chat): add HIGH priority characterization tests (#292)

Adds 6 HIGH priority characterization tests from G0.1 audit:
- model_downgrade_local_fallback_banner
- model_downgrade_upgrade_message
- tts_speaker_button_conditional_render
- citations_multiple_per_message
- last_assistant_message_strategy_tier_badge
- window_slide_exact_boundary_51_messages

Updates TtsSpeakerButton, RuleSourceCard, ResponseMetaBadge mocks
to be observable (with data-testid + props-as-attributes).

Refs meepleAi-app/meepleai-monorepo#292
Refs docs/development/chat-message-list-behavior-baseline.md"
```

### A.1.2 — MEDIUM priority tests (5)

- [ ] **Step 11: Write test `streaming_status_message_display`**

Already covered by PR #307's `shows status message with role='status' and aria-live='polite'`. **Skip — duplicate.**

- [ ] **Step 12: Write test `feedback_gameId_null_hides_buttons`**

Already covered by PR #307. **Skip — duplicate.**

- [ ] **Step 13: Write test `technical_details_panel_visibility`**

Update TechnicalDetailsPanel mock:

```tsx
vi.mock('../TechnicalDetailsPanel', () => ({
  TechnicalDetailsPanel: ({ debugSteps, isAdmin }: { debugSteps: unknown[]; isAdmin: boolean }) => (
    <div data-testid="tech-details" data-steps={debugSteps.length} data-admin={String(isAdmin)} />
  ),
}));
```

Test:

```tsx
it('renders TechnicalDetailsPanel only when isEditor && isLastAssistant && debugSteps.length > 0', () => {
  const msg: ChatMessageItem = { id: 'a', role: 'assistant', content: 'Response' };

  // Case 1: not editor → hidden
  const { rerender } = render(
    <ChatMessageList
      {...defaultProps}
      messages={[msg]}
      isEditor={false}
      streamState={{ ...baseStream, debugSteps: [{ step: 'mock' }] }}
    />
  );
  expect(screen.queryByTestId('tech-details')).toBeNull();

  // Case 2: editor + empty debugSteps → hidden
  rerender(
    <ChatMessageList
      {...defaultProps}
      messages={[msg]}
      isEditor={true}
      streamState={{ ...baseStream, debugSteps: [] }}
    />
  );
  expect(screen.queryByTestId('tech-details')).toBeNull();

  // Case 3: editor + debugSteps → visible with isAdmin propagated
  rerender(
    <ChatMessageList
      {...defaultProps}
      messages={[msg]}
      isEditor={true}
      isAdmin={true}
      streamState={{ ...baseStream, debugSteps: [{ step: 'mock' }] }}
    />
  );
  const panel = screen.getByTestId('tech-details');
  expect(panel).toHaveAttribute('data-steps', '1');
  expect(panel).toHaveAttribute('data-admin', 'true');
});
```

- [ ] **Step 14: Write test `empty_citations_list_no_render`**

```tsx
it('does NOT render RuleSourceCard when citations array is empty', () => {
  const msg: ChatMessageItem = {
    id: 'a',
    role: 'assistant',
    content: 'No sources',
    citations: [],
  };
  render(<ChatMessageList {...defaultProps} messages={[msg]} />);
  expect(screen.queryByTestId('rule-source-card')).toBeNull();
});
```

- [ ] **Step 15: Write test `message_content_whitespace_preserved`**

```tsx
it('preserves whitespace in message content (whitespace-pre-wrap)', () => {
  const msg: ChatMessageItem = {
    id: 'a',
    role: 'assistant',
    content: 'Line 1\n\nLine 3',
  };
  render(<ChatMessageList {...defaultProps} messages={[msg]} />);
  const contentEl = screen.getByText(/Line 1/);
  // The container should have whitespace-pre-wrap class
  expect(contentEl.className).toContain('whitespace-pre-wrap');
});
```

- [ ] **Step 16: Run all tests — expect GREEN (21 total or 24 if skips removed)**

```bash
pnpm -C apps/web test ChatMessageList --run
```

- [ ] **Step 17: Commit MEDIUM priority tests**

```bash
git commit -am "test(chat): add MEDIUM priority characterization tests (#292)

Adds 3 MEDIUM priority characterization tests:
- technical_details_panel_visibility (3 conditional branches)
- empty_citations_list_no_render
- message_content_whitespace_preserved

Skipped as duplicates of PR #307:
- streaming_status_message_display
- feedback_gameId_null_hides_buttons
- feedback_buttons_only_on_assistant

Refs meepleAi-app/meepleai-monorepo#292"
```

## Phase A.2 — Refactor ChatMessageList

**Goal:** Replace inline message rendering (311 lines) with composition using the existing `<ChatMessage>` atom and `<MeepleAvatar>`. Target ~150 lines for the orchestrator.

**Strategy:** Adapter pattern — create `toChatMessageProps()` to bridge type shapes. Keep windowing, feedback handling, streaming bubble in the list. Extract per-message rendering to `<ChatMessage>`.

### A.2.1 — Create the type adapter

- [ ] **Step 18: Verify ChatMessage component API**

```bash
cat apps/web/src/components/ui/meeple/chat-message.tsx | head -100
```

Capture the `ChatMessageProps` interface. Note the differences from `ChatMessageItem`:
- `role: 'user' | 'assistant'` — same
- `content: string` — same
- `confidence?: number` — ChatMessage expects it; ChatMessageItem does not have it (currently only in streaming state)
- `citations?: Citation[]` — ChatMessage uses its own Citation type; may differ from `@/types` Citation
- `agentType?: 'tutor' | 'arbitro' | 'stratega' | 'narratore'` — ChatMessage-specific
- `avatar?: { src?, fallback }` — ChatMessage-specific (for user messages)
- `onFeedback?: (value) => void` — ChatMessage exposes this

- [ ] **Step 19: Create adapter file**

File: `apps/web/src/components/chat-unified/utils/toChatMessageProps.ts`

```ts
/**
 * Adapter: ChatMessageItem (ChatMessageList state) → ChatMessageProps (ChatMessage atom).
 *
 * Introduced as part of ChatMessageList refactor (#292) to bridge the two
 * separate data shapes without modifying either type surface.
 */

import type { ChatMessageItem } from '../ChatMessageList';
import type { ChatMessageProps } from '@/components/ui/meeple/chat-message';
import type { Citation as MessageCitation } from '@/types';

interface AdapterContext {
  /** Whether this is the final assistant message (enables strategy tier badge rendering) */
  isLastAssistant: boolean;
  /** Current game title for citation source label */
  gameTitle?: string;
  /** Feedback current value for this message id */
  feedbackValue: 'helpful' | 'not-helpful' | null;
  /** Feedback loading state for this message id */
  feedbackLoading: boolean;
  /** Feedback change handler */
  onFeedbackChange: (
    value: 'helpful' | 'not-helpful' | null,
    comment?: string
  ) => void;
}

export function toChatMessageProps(
  item: ChatMessageItem,
  ctx: AdapterContext
): ChatMessageProps {
  return {
    role: item.role,
    content: item.content,
    timestamp: item.timestamp,
    citations: mapCitations(item.citations),
    feedbackValue: ctx.feedbackValue,
    feedbackLoading: ctx.feedbackLoading,
    onFeedbackChange: ctx.onFeedbackChange,
    // NOTE: agentType + confidence are not yet surfaced from ChatMessageList state.
    // They remain undefined and ChatMessage handles the fallback.
  };
}

function mapCitations(
  src: MessageCitation[] | undefined
): ChatMessageProps['citations'] {
  if (!src || src.length === 0) return undefined;
  return src.map(c => ({
    id: c.id,
    label: c.label ?? 'Fonte',
    page: c.page,
    source: c.source,
  }));
}
```

- [ ] **Step 20: Write test for adapter**

File: `apps/web/src/components/chat-unified/utils/__tests__/toChatMessageProps.test.ts`

```ts
import { describe, it, expect, vi } from 'vitest';

import { toChatMessageProps } from '../toChatMessageProps';

import type { ChatMessageItem } from '../../ChatMessageList';

describe('toChatMessageProps', () => {
  const baseItem: ChatMessageItem = {
    id: 'msg-1',
    role: 'assistant',
    content: 'Hello',
  };

  const baseCtx = {
    isLastAssistant: false,
    gameTitle: 'Catan',
    feedbackValue: null,
    feedbackLoading: false,
    onFeedbackChange: vi.fn(),
  } as const;

  it('maps basic role and content', () => {
    const result = toChatMessageProps(baseItem, baseCtx);
    expect(result.role).toBe('assistant');
    expect(result.content).toBe('Hello');
  });

  it('propagates feedback state', () => {
    const handler = vi.fn();
    const result = toChatMessageProps(baseItem, {
      ...baseCtx,
      feedbackValue: 'helpful',
      feedbackLoading: true,
      onFeedbackChange: handler,
    });
    expect(result.feedbackValue).toBe('helpful');
    expect(result.feedbackLoading).toBe(true);
    expect(result.onFeedbackChange).toBe(handler);
  });

  it('maps citations array', () => {
    const result = toChatMessageProps(
      {
        ...baseItem,
        citations: [
          { id: 'c1', label: 'Rule 1', page: 2, source: 'rules.pdf' },
        ],
      },
      baseCtx
    );
    expect(result.citations).toHaveLength(1);
    expect(result.citations?.[0]).toMatchObject({
      id: 'c1',
      label: 'Rule 1',
      page: 2,
    });
  });

  it('returns undefined citations when source is empty', () => {
    const result = toChatMessageProps({ ...baseItem, citations: [] }, baseCtx);
    expect(result.citations).toBeUndefined();
  });
});
```

- [ ] **Step 21: Run adapter test — expect GREEN**

```bash
pnpm -C apps/web test toChatMessageProps --run
```

- [ ] **Step 22: Commit adapter**

```bash
git add apps/web/src/components/chat-unified/utils/toChatMessageProps.ts \
        apps/web/src/components/chat-unified/utils/__tests__/toChatMessageProps.test.ts
git commit -m "refactor(chat): add ChatMessageItem → ChatMessage adapter (#292)

Bridges the two separate data shapes without modifying either type
surface. Adapter takes a ChatMessageItem + context (feedback state,
game title, isLastAssistant flag) and returns ChatMessageProps.

Step 1 of the ChatMessageList refactor pipeline.

Refs meepleAi-app/meepleai-monorepo#292"
```

### A.2.2 — Replace inline message block with `<ChatMessage>`

- [ ] **Step 23: Read current inline rendering (lines ~150-220 of ChatMessageList.tsx)**

Identify the block that renders each message. It's inside the `.map()` call after the windowing slice. Note all the props being used: `role`, `content`, `citations`, `isLastAssistant` check, `FeedbackButtons` wiring.

- [ ] **Step 24: Replace inline block with ChatMessage composition**

Pseudo-diff (actual implementation will be larger):

```tsx
// Before (inline, ~70 lines per message):
{windowedMessages.map((msg, idx) => {
  const isLastAssistant = ...;
  return (
    <div key={msg.id} className={cn(...)}>
      {/* role-based wrapper */}
      {/* avatar */}
      {/* content */}
      {/* citations via RuleSourceCard */}
      {/* feedback buttons */}
      {/* technical details */}
      {/* TTS */}
    </div>
  );
})}

// After (composed, ~15 lines per message):
{windowedMessages.map((msg, idx) => {
  const isLastAssistant = isLastAssistantMessage(windowedMessages, idx);
  return (
    <React.Fragment key={msg.id}>
      <ChatMessage
        {...toChatMessageProps(msg, {
          isLastAssistant,
          gameTitle,
          feedbackValue: feedbackMap.get(msg.id) ?? null,
          feedbackLoading: feedbackLoadingMap.get(msg.id) ?? false,
          onFeedbackChange: (value, comment) =>
            handleFeedback(msg.id, value, comment),
        })}
      />
      {isLastAssistant && isEditor && streamState.debugSteps.length > 0 && (
        <TechnicalDetailsPanel
          debugSteps={streamState.debugSteps}
          executionId={streamState.executionId}
          showDebugLink={isAdmin}
        />
      )}
      {isLastAssistant && streamState.strategyTier && !streamState.isStreaming && (
        <ResponseMetaBadge strategyTier={streamState.strategyTier} />
      )}
    </React.Fragment>
  );
})}
```

**Note:** `TechnicalDetailsPanel` and `ResponseMetaBadge` are kept outside `<ChatMessage>` because they are context-dependent (only on the last message, only in certain conditions) and `<ChatMessage>` does not expose slots for them.

- [ ] **Step 25: Run all ChatMessageList tests — expect GREEN (safety net check)**

```bash
pnpm -C apps/web test ChatMessageList --run
```

If any test fails: STOP. The refactor introduced a regression. Fix before continuing.

- [ ] **Step 26: Manual smoke test on dev server**

```bash
pnpm -C apps/web dev
# Navigate to a chat thread, send a message, verify:
# - Message renders correctly
# - Streaming bubble appears during response
# - Citations render after response
# - Feedback buttons appear on assistant messages
# - TTS button appears when enabled
# - Model downgrade banner appears if triggered
```

- [ ] **Step 27: Commit Phase A.2.2**

```bash
git commit -am "refactor(chat): replace inline message block with ChatMessage (#292)

Replaces ~70 lines of per-message inline JSX with composition:
  <ChatMessage {...toChatMessageProps(msg, ctx)} />

Keeps orchestration logic in ChatMessageList:
- Windowing (WINDOW_SIZE=50 slice)
- Per-message feedback state (feedbackMap, feedbackLoadingMap)
- handleFeedback API round-trip
- isLastAssistant derivation
- TechnicalDetailsPanel (context-dependent)
- ResponseMetaBadge (context-dependent)
- Streaming bubble + status message + model downgrade banner

All 24 characterization tests still pass.

Refs meepleAi-app/meepleai-monorepo#292"
```

### A.2.3 — Wire MeepleAvatar state mapping

- [ ] **Step 28: Verify MeepleAvatar state enum**

`MeepleAvatarState = 'idle' | 'thinking' | 'confident' | 'searching' | 'uncertain'`

- [ ] **Step 29: Add state derivation helper**

File: `apps/web/src/components/chat-unified/utils/deriveMeepleAvatarState.ts`

```ts
import type { MeepleAvatarState } from '@/components/ui/meeple/meeple-avatar';
import type { StreamStateForMessages } from '../ChatMessageList';

/**
 * Derives the MeepleAvatar state from the current stream state.
 *
 * Mapping:
 * - isStreaming + currentAnswer empty → 'searching' (thinking/fetching context)
 * - isStreaming + currentAnswer non-empty → 'thinking' (actively generating)
 * - !isStreaming + strategyTier 'Strict' → 'confident'
 * - !isStreaming + strategyTier 'Balanced' → 'idle'
 * - !isStreaming + strategyTier 'Loose' → 'uncertain'
 * - modelDowngrade present → 'uncertain' (fallback model = lower confidence)
 * - default → 'idle'
 */
export function deriveMeepleAvatarState(
  stream: StreamStateForMessages
): MeepleAvatarState {
  if (stream.modelDowngrade) return 'uncertain';
  if (stream.isStreaming) {
    return stream.currentAnswer.length === 0 ? 'searching' : 'thinking';
  }
  switch (stream.strategyTier) {
    case 'Strict':
      return 'confident';
    case 'Loose':
      return 'uncertain';
    case 'Balanced':
    default:
      return 'idle';
  }
}
```

- [ ] **Step 30: Write test for state derivation**

File: `apps/web/src/components/chat-unified/utils/__tests__/deriveMeepleAvatarState.test.ts`

```ts
import { describe, it, expect } from 'vitest';

import { deriveMeepleAvatarState } from '../deriveMeepleAvatarState';

describe('deriveMeepleAvatarState', () => {
  const base = {
    isStreaming: false,
    currentAnswer: '',
    statusMessage: null,
    strategyTier: null,
    executionId: null,
    debugSteps: [],
    modelDowngrade: null,
  };

  it('returns searching when streaming with empty answer', () => {
    expect(deriveMeepleAvatarState({ ...base, isStreaming: true })).toBe('searching');
  });

  it('returns thinking when streaming with partial answer', () => {
    expect(
      deriveMeepleAvatarState({ ...base, isStreaming: true, currentAnswer: 'Ciao' })
    ).toBe('thinking');
  });

  it('returns uncertain when modelDowngrade present', () => {
    expect(
      deriveMeepleAvatarState({
        ...base,
        modelDowngrade: {
          isLocalFallback: true,
          originalModel: 'gpt-4',
          fallbackModel: 'llama3',
          upgradeMessage: false,
          reason: 'rate_limited',
        },
      })
    ).toBe('uncertain');
  });

  it('maps Strict → confident', () => {
    expect(deriveMeepleAvatarState({ ...base, strategyTier: 'Strict' })).toBe('confident');
  });

  it('maps Balanced → idle', () => {
    expect(deriveMeepleAvatarState({ ...base, strategyTier: 'Balanced' })).toBe('idle');
  });

  it('maps Loose → uncertain', () => {
    expect(deriveMeepleAvatarState({ ...base, strategyTier: 'Loose' })).toBe('uncertain');
  });

  it('defaults to idle', () => {
    expect(deriveMeepleAvatarState(base)).toBe('idle');
  });
});
```

- [ ] **Step 31: Integrate avatar state into ChatMessage composition**

Update `toChatMessageProps.ts` signature:

```ts
interface AdapterContext {
  // ... existing fields
  meepleAvatarState: MeepleAvatarState;
}

// In the return:
return {
  // ... existing fields
  meepleAvatarState: ctx.meepleAvatarState,
};
```

And in `ChatMessageList.tsx` message map:

```tsx
const avatarState = deriveMeepleAvatarState(streamState);
// ...
<ChatMessage
  {...toChatMessageProps(msg, {
    // ... existing context
    meepleAvatarState: avatarState,
  })}
/>
```

- [ ] **Step 32: Run tests — expect GREEN**

```bash
pnpm -C apps/web test chat-unified --run
```

- [ ] **Step 33: Commit Phase A.2.3**

```bash
git commit -am "refactor(chat): wire MeepleAvatar state from stream state (#292)

Adds deriveMeepleAvatarState() mapping StreamStateForMessages to the
5-state MeepleAvatar enum:
- isStreaming + empty → searching
- isStreaming + content → thinking
- modelDowngrade → uncertain
- strategyTier Strict/Balanced/Loose → confident/idle/uncertain

Refs meepleAi-app/meepleai-monorepo#292"
```

### A.2.4 — Cleanup orphan annotations

- [ ] **Step 34: Remove @status ORPHAN from chat-message.tsx**

Delete the ORPHAN JSDoc block (added in PR #298). The component now has a real production consumer (ChatMessageList).

- [ ] **Step 35: Remove @status ORPHAN from meeple-avatar.tsx**

Same. MeepleAvatar is now transitively consumed via ChatMessage.

- [ ] **Step 36: Remove [ORPHAN] prefix from showcase metadata**

File: `apps/web/src/components/showcase/stories/metadata.ts`

```diff
-    description:
-      '[ORPHAN] Message bubble with role/confidence/feedback. ChatMessageList renders inline instead — refactor pending.',
+    description:
+      'Message bubble with role-based layout, AI confidence badge, and feedback buttons.',
```

```diff
-    description:
-      '[ORPHAN] Animated meeple AI assistant with 5 states. Transitively orphan via ChatMessage.',
+    description:
+      'Animated meeple character representing the AI assistant with 5 activity states.',
```

- [ ] **Step 37: Commit cleanup**

```bash
git commit -am "chore(showcase): promote ChatMessage + MeepleAvatar from orphan status (#292)

Removes @status ORPHAN JSDoc and [ORPHAN] showcase prefixes.
Both components now have real production consumers via ChatMessageList
refactor.

Refs meepleAi-app/meepleai-monorepo#292"
```

### A.2.5 — Final verification

- [ ] **Step 38: Verify ChatMessageList is ≤ 150 lines**

```bash
wc -l apps/web/src/components/chat-unified/ChatMessageList.tsx
```

Target: ≤ 150. Current (pre-refactor): 311. If > 150, investigate which sections can still be extracted.

- [ ] **Step 39: Run full frontend test suite**

```bash
pnpm -C apps/web test --run
```

All tests must pass. No new failures, no skipped tests.

- [ ] **Step 40: Typecheck + lint**

```bash
pnpm -C apps/web typecheck
pnpm -C apps/web lint
```

Both must be clean.

- [ ] **Step 41: Push + PR**

```bash
git push -u origin feature/issue-292-chatmessagelist-refactor

gh pr create --repo meepleAi-app/meepleai-monorepo \
  --base main-dev \
  --head feature/issue-292-chatmessagelist-refactor \
  --title "refactor(chat): compose ChatMessageList with ChatMessage atom" \
  --body-file docs/development/pr-body-292.md
```

(PR body template: reference all 24 characterization tests, note LOC reduction, link to characterization baseline doc.)

- [ ] **Step 42: Request architect code review**

```bash
gh pr edit <PR_NUMBER> --add-reviewer <architect-handle>
```

**Do NOT auto-merge.** This refactor is HIGH risk and requires human review per Nygard's recommendation from the spec-panel review.

- [ ] **Step 43: After human approval, merge**

```bash
gh pr merge <PR_NUMBER> --merge --delete-branch
```

**Post-merge smoke test in staging:** 5 manual chat exchanges with streaming + citations + feedback + model downgrade.

---

# Track B — #294 AgentStatsDisplay Unblock

**Issue:** meepleAi-app/meepleai-monorepo#294 (currently CLOSED as blocked)
**Status:** ⏸ Requires human decision before execution

## The problem (recap)

`AgentStatsDisplay` consumes `AgentMetadata` type which requires:
- `invocationCount: number` (mandatory)
- `capabilities: Array<'RAG' | 'Vision' | 'Code' | ...>` (mandatory)
- `avgResponseTime?: number`

`AgentDefinitionDto` (actual backend schema) exposes:
- `id, name, description, type, config, strategyName, strategyParameters, prompts, tools, kbCardIds, chatLanguage, isActive, status, createdAt, updatedAt`

**Missing telemetry:** invocationCount, avgResponseTime, capabilities.

## Decision tree

### Path A — Extend backend with telemetry endpoint

**Effort:** M (backend + frontend, ~8-12h)
**Risk:** MEDIUM (backend schema change, new endpoint contract)
**Value:** HIGHEST (enables full AgentStatsDisplay + unlocks future telemetry features)

**Steps:**

1. **Backend:** Add domain command `GetAgentMetricsQuery` in `AgentMetrics` bounded context (or extend existing `AgentManagement` BC)
2. **Backend:** Add repository query to aggregate from request log / telemetry table
   - `SELECT agent_id, COUNT(*), AVG(response_time_ms) FROM agent_invocations GROUP BY agent_id`
   - If no telemetry table exists: add one with migration
3. **Backend:** Add endpoint `GET /api/admin/agent-definitions/:id/metrics`
4. **Backend:** Add integration test
5. **Frontend:** Add API client method in `agent-definitions.api.ts`
6. **Frontend:** Create mapper `mapAgentDefinitionWithMetricsToMetadata`
7. **Frontend:** Update `BuilderTable` to render `<AgentStatsDisplay>` in a new column
8. **Frontend:** Remove `@status ORPHAN` from `AgentStatsDisplay.tsx`
9. **Frontend:** Remove `[ORPHAN]` from showcase metadata
10. **Open 2 PRs:** one for backend, one for frontend (frontend depends on backend merge)
11. **Reopen + close #294** with reference to the 2 PRs

**Required capabilities data:** where does "capabilities: RAG | Vision | Code" come from? It's not in `AgentDefinitionDto`. Options:
- Derive from `tools: ToolConfig[]` — if `tools` contains a RAG tool, capability = 'RAG'. Needs mapping table.
- Add a new `capabilities` field to `AgentDefinitionDto`
- Hardcode based on strategy (Strict = RAG, Loose = Functions, etc.)

**Recommended:** derive from tools via mapping table (most evolvable).

### Path B — Refactor component to accept AgentDefinitionDto

**Effort:** S (~4h)
**Risk:** LOW
**Value:** MEDIUM (removes orphan, but drops capabilities/telemetry features)

**Steps:**

1. Create new component `AgentDefinitionStatsDisplay.tsx` that takes `AgentDefinitionDto` directly
2. Render: name, status, model (from config.model), temperature, isActive badge
3. **Skip** capabilities, invocationCount, avgResponseTime (not available)
4. Delete old `AgentStatsDisplay.tsx`
5. Delete old `agent-stats-display.story.tsx` + update metadata
6. Integrate in `BuilderTable` as a new column
7. Open 1 PR

**Downside:** loses the capabilities tags feature that made `AgentStatsDisplay` distinctive. Practically equivalent to a glorified model+status display.

### Path C — Delete component entirely

**Effort:** S (~2h)
**Risk:** NONE
**Value:** LOW (but highest code hygiene)

**Steps:**

1. Delete `apps/web/src/components/ui/agent/AgentStatsDisplay.tsx`
2. Delete `apps/web/src/components/ui/agent/__tests__/AgentStatsDisplay.test.tsx` (if exists)
3. Delete `apps/web/src/components/showcase/stories/agent-stats-display.story.tsx`
4. Remove from `showcase/stories/index.ts` + `metadata.ts`
5. Remove from `admin/ui-library/component-map.ts` + `config/component-registry.ts`
6. **Consider:** is `AgentMetadata` type still used anywhere? If not, delete it too.
7. Open 1 PR
8. #294 stays closed (already closed as blocked)

**Upside:** cleanest outcome if we accept that the feature was never going to be built.

## Recommendation

**Path C — delete.** Rationale:
- 2 epics have passed (Epic #4068 Issue #4184) without the telemetry endpoint being built
- No product forcing function has emerged
- `AgentCharacterSheet` covers the detail view need (RPG-style)
- `BuilderTable` columns already cover name/status/model
- YAGNI: deleting is reversible from git history

**Alternative recommendation (if telemetry is roadmapped):** Path A with clear timeline commitment. Do NOT start Path A without a product owner signing off on the backend effort.

**Avoid Path B:** it creates a degraded version of an already-degraded component.

## Decision required

- [ ] Product/tech lead chooses Path A / B / C
- [ ] If Path A: schedule backend work in sprint
- [ ] If Path B: open sub-ticket
- [ ] If Path C: execute immediately (lowest-risk track)

**Signed:** ________________
**Date:** ________________

---

# Track C — #293 PageTransition Post-merge Validation

**Issue:** meepleAi-app/meepleai-monorepo#293 (already closed via PR #304)
**Branch needed:** `chore/page-transition-staging-validation` (only if changes needed)

**Why:** PR #304 merged the integration behind `NEXT_PUBLIC_ENABLE_PAGE_TRANSITIONS=false` (default OFF). Actual user-visible validation requires enabling the flag in staging and measuring impact.

## Phase C.1 — Staging deploy with flag enabled

- [ ] **Step 1: Identify staging env var configuration location**

```bash
cd infra && make secrets-sync
cat infra/secrets/web.secret | grep NEXT_PUBLIC
```

If `NEXT_PUBLIC_ENABLE_PAGE_TRANSITIONS` is not listed, add it.

- [ ] **Step 2: Enable the flag in staging secrets**

Edit `infra/secrets/web.secret` (on staging server):

```bash
ssh meepleai.app
cd ~/meepleai/infra
# Edit web.secret to add:
# NEXT_PUBLIC_ENABLE_PAGE_TRANSITIONS=true
vim secrets/web.secret
```

**Note:** `NEXT_PUBLIC_*` vars are **build-time**, so the web image must be rebuilt (not just restarted).

- [ ] **Step 3: Trigger staging rebuild**

```bash
cd infra && make staging-rebuild-web
# OR (if that target doesn't exist):
docker compose -f docker-compose.staging.yml up -d --build web
```

- [ ] **Step 4: Verify the flag is active**

```bash
curl -s https://meepleai.app/chat | grep -i "NEXT_PUBLIC_ENABLE_PAGE_TRANSITIONS" || true
# Alternatively, open the browser DevTools and inspect the built JS
```

## Phase C.2 — Manual QA

- [ ] **Step 5: Navigate to `/chat` route**

Open https://meepleai.app/chat in Chrome (latest).

- [ ] **Step 6: Test case — Empty state → thread**

1. Start from `/chat` with no active thread
2. Click "New thread" button
3. Observe: should see a fade-in transition on the new thread view
4. **Pass criteria:** smooth fade, no flicker, no missing content flash

- [ ] **Step 7: Test case — Thread A → Thread B switch**

1. Have 2 threads in the sidebar
2. Click Thread A
3. Click Thread B
4. **Pass criteria:** visible fade-out of A + fade-in of B (~300ms), no layout shift

- [ ] **Step 8: Test case — Back navigation**

1. Navigate to Thread A
2. Browser back button
3. **Pass criteria:** smooth transition, correct history stack

- [ ] **Step 9: Test case — Streaming in progress during navigation**

1. Start a chat message in Thread A
2. During SSE streaming, click Thread B
3. **Pass criteria:** no UI corruption, streaming aborts cleanly, Thread B renders

- [ ] **Step 10: Test case — Hydration check**

Open DevTools Console before navigating. Navigate 5 times.
**Pass criteria:** Zero warnings about "Hydration failed" or "text content did not match"

- [ ] **Step 11: Document findings**

Update `docs/development/page-transition-decision.md` with a new section:

```markdown
## Staging validation results (YYYY-MM-DD)

- [x] Empty → thread transition: PASS
- [x] Thread switch: PASS / FAIL (detail)
- [x] Back nav: PASS
- [x] Streaming interruption: PASS / FAIL (detail)
- [x] Hydration: PASS / FAIL (detail)

**Verdict:** GO / NO-GO for production rollout.
**Signed:** _____________________
**Date:** _____________________
```

## Phase C.3 — Lighthouse performance check

- [ ] **Step 12: Baseline Lighthouse run (flag OFF)**

```bash
# Temporarily disable the flag in staging OR use a local build
NEXT_PUBLIC_ENABLE_PAGE_TRANSITIONS=false pnpm -C apps/web build
npx lighthouse https://meepleai.app/chat --only-categories=performance \
  --output=json --output-path=./lighthouse-baseline.json
```

- [ ] **Step 13: Flag ON Lighthouse run**

```bash
NEXT_PUBLIC_ENABLE_PAGE_TRANSITIONS=true pnpm -C apps/web build
npx lighthouse https://meepleai.app/chat --only-categories=performance \
  --output=json --output-path=./lighthouse-flag-on.json
```

- [ ] **Step 14: Compare key metrics**

```bash
jq '.categories.performance.score' lighthouse-baseline.json
jq '.categories.performance.score' lighthouse-flag-on.json
# Delta should be ≤ 5% (≤ -0.05)

jq '.audits."largest-contentful-paint".numericValue' lighthouse-baseline.json
jq '.audits."largest-contentful-paint".numericValue' lighthouse-flag-on.json
# LCP delta should be ≤ 100ms
```

**Pass criteria:** Both deltas within tolerance.

- [ ] **Step 15: Document performance findings**

Append to `docs/development/page-transition-decision.md`:

```markdown
## Lighthouse delta (YYYY-MM-DD)

| Metric | Baseline (flag OFF) | Flag ON | Delta | Verdict |
|---|---|---|---|---|
| Performance score | X | Y | Z | OK / FAIL |
| LCP (ms) | X | Y | Z | OK / FAIL |
| FCP (ms) | X | Y | Z | OK / FAIL |
| CLS | X | Y | Z | OK / FAIL |
```

## Phase C.4 — Production rollout decision

- [ ] **Step 16: Review manual QA + Lighthouse results**

If all GO:
- [ ] **Step 17a: Open PR to enable flag in production**

```bash
# Edit infra/secrets/web.secret or equivalent
# Commit + PR to main-dev + merge + redeploy
```

If any NO-GO:
- [ ] **Step 17b: Rollback flag in staging**

```bash
# Set NEXT_PUBLIC_ENABLE_PAGE_TRANSITIONS=false
# Rebuild web image
# Document failure modes in decision doc
# Open follow-up ticket to fix the specific issue
```

## Phase C.5 — Close the loop

- [ ] **Step 18: Comment on Issue #293**

```
Post-merge validation complete:
- Staging deploy: [link to deploy log]
- Manual QA: [PASS/FAIL with details]
- Lighthouse delta: [summary]
- Production rollout: [enabled YYYY-MM-DD / rolled back]

See docs/development/page-transition-decision.md for full results.
```

- [ ] **Step 19: If rollout succeeded, remove feature flag gate**

After ~1 week in production with no issues, remove the `PAGE_TRANSITIONS_ENABLED` check in `(chat)/layout.tsx` and always wrap with `<PageTransition>`. Open follow-up PR.

---

## Execution Matrix

| Track | Status | Prerequisites | Estimated wall-clock | Owner |
|---|---|---|---|---|
| A — #292 refactor | Not started | Characterization tests in PR #307 (done) | 16-20h | TBD (requires architect review) |
| B — #294 unblock | **Blocked on decision** | Product/tech lead chooses path | Varies (2h / 4h / 12h) | TBD |
| C — #293 validation | Not started | Staging deploy access | 4-6h | TBD |

## Dependencies

- **Track A** does NOT depend on Track B or Track C
- **Track B** depends on a decision, not on other tracks
- **Track C** does NOT depend on Track A or Track B
- All 3 tracks can run in parallel with different engineers

## Suggested execution order (single engineer)

1. **Track C first** — lowest effort, fastest feedback, validates that Phase 2b PR #304 works
2. **Track B next** — just the decision (~30 min), then execute whichever path is chosen
3. **Track A last** — largest effort, requires full attention + architect review

## Risk Register

| Risk | Severity | Mitigation |
|---|---|---|
| Track A refactor breaks production chat | CRITICAL | 24 characterization tests as safety net + architect review + staging smoke test |
| Track B Path A backend work slips | MEDIUM | Set hard deadline; fall back to Path C if missed |
| Track C PageTransition causes hydration mismatch in production | MEDIUM | Feature flag allows instant rollback (set env var + redeploy) |
| Concurrent git activity from other agents | LOW | Use branches for each track; rebase before push |

## Self-Review (writing-plans skill)

- [x] **Spec coverage:** 3 tracks map 1:1 to the 3 follow-up points
- [x] **No placeholders:** Track A has concrete code; Track B is explicit about the decision; Track C has concrete commands
- [x] **Type consistency:** `ChatMessageItem`, `ChatMessageProps`, `AgentMetadata`, `AgentDefinitionDto` referenced consistently
- [x] **Exit criteria:** each phase has measurable DoD
- [x] **Risk register:** explicit with mitigation
- [x] **Execution matrix:** clear owner + dependencies + parallel vs serial guidance

## Execution Handoff

**Do NOT execute autonomously.** This plan contains:
1. A **HIGH-risk refactor** (Track A) that needs architect review
2. A **pending human decision** (Track B) that cannot be delegated
3. A **production deploy** (Track C) that needs staging validation

Recommended: review this plan first, then decide which tracks to execute and under what supervision level. Tracks A and C can be inline-executed with checkpoints; Track B is a gate.
