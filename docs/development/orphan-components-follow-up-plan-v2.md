# Orphan Components Follow-up Plan v2

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. This plan uses checkbox (`- [ ]`) syntax for tracking.

> **Revision history:**
> - **v1** (2026-04-09) — Initial draft. Gaps identified by spec-panel critique: missing architecture spike, vague success criteria, fragile tests, undefined helpers.
> - **v2** (2026-04-09) — First revision. Incorporates feedback from Fowler, Wiegers, Newman, Nygard, Crispin, Cockburn.
> - **v2.1** (2026-04-09) — Amendments from v2 self-review (6 residual issues patched inline):
>   1. Adapter stub has lint-disable + CI grep guard (#292-SPIKE-STUB marker)
>   2. Track C prerequisite: named UX motion owner or blocking meta-ticket
>   3. CODEOWNERS append has conflict pre-check
>   4. 24h observation window has enforced timestamp gate with bash guard
>   5. `whitespace-pre-wrap` test has jsdom fallback strategy
>   6. Execution session structure split into Solo / Team paths (no contradiction)

**Goal:** Resolve the 3 residual follow-up items from the main orphan components execution plan (PRs #298/#303-#307 merged 2026-04-09).

**Scope:** 3 independent tracks, all gated by explicit pre-conditions. Tracks do NOT depend on each other; can run in parallel with different engineers.

**Tech Stack:** Next.js 16 + React 19 + Vitest (frontend), .NET 9 (backend), GitHub Actions CI.

---

## Track Summary

| Track | Ticket | Effort | Risk | Blocker | Owner |
|---|---|---|---|---|---|
| **A** | meepleAi-app/meepleai-monorepo#292 | L (20-28h incl. spike) | HIGH | None — spike unblocks refactor strategy | TBD (architect-reviewed) |
| **B** | meepleAi-app/meepleai-monorepo#294 | S-M (varies by path) | LOW-MEDIUM | **Decision deadline: 14 days** | Product/tech lead |
| **C** | meepleAi-app/meepleai-monorepo#293 | S (6-8h) | LOW | Staging access + UX sign-off | TBD |

## Agent Delegation Matrix

Which parts of this plan can be delegated to a Claude Code subagent vs require a human?

| Phase | Agent? | Reason |
|---|---|---|
| A.0 Verification Spike | ✅ Subagent | Read-only analysis → produces report |
| A.1 Characterization tests | ✅ Subagent | Mechanical TDD with clear templates |
| A.2 Refactor | ⚠️ Inline agent + human review | Multi-step refactor with architect review gate |
| A.3 Final verification + PR | ⚠️ Agent + human approval | Agent prepares PR; human merges |
| B Decision | 🚫 Human only | Product call |
| B Path C execution | ✅ Subagent | Delete-only mechanical work |
| B Path B execution | ⚠️ Agent + review | New component needs design input |
| B Path A execution | 🚫 Human only | Backend domain modeling |
| C.1 Deploy | 🚫 Human only | Staging access, SSH |
| C.2 Manual QA | 🚫 Human only | Visual inspection |
| C.3 Lighthouse | ✅ Agent (local) | Scripted comparison |
| C.4 Rollout decision | 🚫 Human only | Production gate |

---

# Track A — #292 ChatMessageList Refactor (Monolithic)

**Issue:** meepleAi-app/meepleai-monorepo#292
**Base branch:** `main-dev`
**Branches:**
- Phase A.0: `chore/issue-292-spike` (PR to main-dev, small, spike report)
- Phase A.1-A.3: `feature/issue-292-chatmessagelist-refactor` (PR to main-dev, main refactor)

**Current state:**
- ✅ 9 characterization tests in PR #307 (5 CRITICAL feedback + 2 streaming + 2 visibility)
- ⏳ Phase A.0 (spike) — not started, **BLOCKING** the refactor
- ⏳ 12 additional characterization tests (7 HIGH + 5 MEDIUM, minus 3 duplicates = 9 unique)
- ⏳ Refactor itself (pending spike outcome)

## 🔐 Exclusive Lock Protocol

**Why:** The refactor spans multiple commits in `chat-unified/*`. A parallel PR modifying the same directory will silently break the refactor base.

**Enforcement (before starting Phase A.1):**

- [ ] **Step 0.1:** Create tracking issue on GitHub

```bash
gh issue create --repo meepleAi-app/meepleai-monorepo \
  --title "LOCK: chat-unified/* for #292 refactor" \
  --body "Exclusive lock on \`apps/web/src/components/chat-unified/*\` during #292 refactor.

Effective: <start date>
Expires: <end date OR #292 merged>
Owner: <refactor author>

Other engineers: if you need to modify chat-unified/*, comment here and coordinate with owner before opening a PR." \
  --label "tech-debt,coordination"
```

- [ ] **Step 0.2:** Pin the lock issue (manual via GitHub UI or `gh api`)

- [ ] **Step 0.3:** Update `.github/CODEOWNERS` temporarily

**Newman amendment — conflict check first:**

```bash
# Verify no existing rule conflicts with the temp lock
if [ -f .github/CODEOWNERS ]; then
  grep -n "chat-unified" .github/CODEOWNERS && {
    echo "⚠️ Existing CODEOWNERS rule matches chat-unified. Review precedence (last-match-wins):"
    grep -n "chat-unified\|apps/web/src/components/chat" .github/CODEOWNERS
    echo "If your temp rule is LAST in the file, it takes precedence. Otherwise, reorder."
    read -p "Continue with append? [y/N] " confirm
    [[ "$confirm" =~ ^[Yy]$ ]] || exit 1
  }
fi

echo "" >> .github/CODEOWNERS
echo "# TEMPORARY LOCK for #292 refactor — remove when merged (see Step 43)" >> .github/CODEOWNERS
echo "/apps/web/src/components/chat-unified/ @<refactor-author>" >> .github/CODEOWNERS
git add .github/CODEOWNERS
git commit -m "chore(codeowners): temp lock chat-unified for #292 refactor"
```

Remember to **revert this** after merging (Step 43). The `# TEMPORARY LOCK` comment marker makes the removal grep-able:

```bash
# Revert command for Step 43:
sed -i '/# TEMPORARY LOCK for #292/,/^$/d' .github/CODEOWNERS
```

## Phase A.0 — Verification Spike (NEW — mandatory pre-phase)

**Why:** The v1 plan assumed `ChatMessage` atom exposes `feedbackValue`, `feedbackLoading`, `onFeedbackChange`, `meepleAvatarState` — but these fields may not exist. Running the refactor without verification has ~50% chance of discovering an incompatibility mid-stream.

**Output:** `docs/development/chat-message-api-compatibility.md` — a spike report that unblocks Phase A.1-A.3.

**Branch:** `chore/issue-292-spike`
**PR:** yes, small (docs-only)
**Effort:** 4-6h

### Phase A.0 steps

- [ ] **Step 0.4:** Read `apps/web/src/components/ui/meeple/chat-message.tsx` end-to-end

```bash
wc -l apps/web/src/components/ui/meeple/chat-message.tsx
cat apps/web/src/components/ui/meeple/chat-message.tsx
```

Capture the **actual `ChatMessageProps` interface** (not guessed).

- [ ] **Step 0.5:** Read `apps/web/src/components/ui/meeple/meeple-avatar.tsx`

Capture the **actual `MeepleAvatarState` enum** and whether it's exported.

- [ ] **Step 0.6:** Read `apps/web/src/components/chat-unified/ChatMessageList.tsx` end-to-end

Capture the **full `ChatMessageItem` interface + `StreamStateForMessages` type + all computed values** (isLastAssistant, feedbackMap, handleFeedback signature).

- [ ] **Step 0.7:** Produce the API diff table

Create `docs/development/chat-message-api-compatibility.md`:

```markdown
# ChatMessage API Compatibility Report (#292 Spike)

**Date:** YYYY-MM-DD
**Spike branch:** chore/issue-292-spike

## Field-by-field comparison

| Field | ChatMessageItem (source) | ChatMessageProps (target) | Adapter action |
|---|---|---|---|
| role | 'user' \| 'assistant' | 'user' \| 'assistant' | 1:1 pass-through |
| content | string | string | 1:1 |
| timestamp | string? | ? | ? |
| citations | Citation[]? from @/types | ? | ? |
| ... | ... | ... | ... |

## Gaps (fields in source not in target)

- [list fields that exist in ChatMessageItem but ChatMessage cannot render]

## Gaps (fields in target not in source)

- [list fields ChatMessage requires but source doesn't provide]

## Feedback slot analysis

Does ChatMessage render feedback buttons internally, or expect the parent to wrap?
- If internal: what props does it expose? (value, onChange, loading?)
- If external: the refactor must KEEP FeedbackButtons in the orchestrator (outside <ChatMessage>)

## MeepleAvatar slot analysis

Does ChatMessage render MeepleAvatar internally?
- If yes: what state props does it expose?
- If no: who renders the avatar — the orchestrator or ChatMessage's assistant-row layout?

## Refactor strategy decision

Based on findings, choose ONE:

- [ ] **Option α:** `ChatMessage` is compatible as-is. Proceed with adapter-only approach. Phase A.2 is pure composition.
- [ ] **Option β:** `ChatMessage` needs NEW PROPS (list them). Phase A.2 splits into: (a) extend ChatMessage, (b) compose ChatMessageList. Two commits, same PR.
- [ ] **Option γ:** `ChatMessage` is incompatible for feedback/avatar. Phase A.2 keeps feedback + avatar OUTSIDE `<ChatMessage>` in the orchestrator. The refactor still gains composition for content/citations/role rendering but doesn't fully delegate.

## Impact on target LOC

With chosen option, project the expected ChatMessageList.tsx LOC after refactor.
Current: 311 (post PR #307).
Target: ≤ 150 (v1 estimate).
Revised target: **<fill in after spike>**.
```

- [ ] **Step 0.8:** Commit spike report + open PR

```bash
git checkout -b chore/issue-292-spike
git config branch.chore/issue-292-spike.parent main-dev
git add docs/development/chat-message-api-compatibility.md
git commit -m "docs(chat): spike report — ChatMessage API compatibility (#292)

Verifies assumptions for the #292 refactor. Identifies which fields of
ChatMessageItem can be mapped 1:1 to ChatMessageProps, which require new
props on ChatMessage, and which must stay in the orchestrator.

Outputs chosen refactor strategy (α/β/γ) for Phase A.2.

Refs meepleAi-app/meepleai-monorepo#292"
git push -u origin chore/issue-292-spike
gh pr create --base main-dev --head chore/issue-292-spike \
  --title "docs(chat): #292 spike — ChatMessage API compatibility report" \
  --body "Spike output for #292. Read-only analysis. Unblocks Phase A.1-A.3 of the refactor."
```

- [ ] **Step 0.9:** Merge spike PR (fast path, no auto-merge needed)

```bash
gh pr merge <PR_NUMBER> --merge --delete-branch
```

**Exit criterion for Phase A.0:** spike report merged to main-dev. Chosen option (α/β/γ) determines what Phase A.2 looks like.

## Phase A.1 — Complete characterization test suite (9 NEW unique tests)

**Reference:** `docs/development/chat-message-list-behavior-baseline.md`

**Pre-condition:** Phase A.0 complete (spike report merged). **NOTE:** Phase A.1 can run in parallel with A.0 if the engineer is confident about mock strategy. The characterization tests are independent of the refactor strategy.

**Mock strategy decision (Crispin):** To avoid touching the existing global mocks (which would affect the 15 pre-existing tests), **split the test file**:
- Keep `ChatMessageList.test.tsx` (unchanged — global mocks stay simple)
- Create `ChatMessageList.characterization.test.tsx` (new — with observable mocks)

### A.1.1 — Create characterization test file with observable mocks

- [ ] **Step 1: Create new test file**

File: `apps/web/src/components/chat-unified/__tests__/ChatMessageList.characterization.test.tsx`

```tsx
/**
 * ChatMessageList Characterization Tests
 *
 * Separate from ChatMessageList.test.tsx to avoid coupling with the
 * global mocks used by windowed slice tests.
 *
 * These tests use OBSERVABLE mocks (with data-testid + props-as-attributes)
 * to verify prop wiring through ChatMessageList to child components.
 *
 * Added as safety net before #292 refactor (~311 → target from A.0 spike).
 */

import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  ChatMessageList,
  type ChatMessageItem,
  type StreamStateForMessages,
} from '../ChatMessageList';

// Observable mocks (different from ChatMessageList.test.tsx)
vi.mock('../TtsSpeakerButton', () => ({
  TtsSpeakerButton: ({ text }: { text: string }) => (
    <button type="button" data-testid="tts-speaker" data-text={text}>
      speak
    </button>
  ),
}));

vi.mock('../RuleSourceCard', () => ({
  RuleSourceCard: ({ citations }: { citations: Array<{ id: string }> }) => (
    <div data-testid="rule-source-card" data-count={citations.length}>
      {citations.map(c => (
        <span key={c.id}>{c.id}</span>
      ))}
    </div>
  ),
}));

vi.mock('../ResponseMetaBadge', () => ({
  ResponseMetaBadge: ({ strategyTier }: { strategyTier: string | null }) => (
    <span data-testid="strategy-badge" data-tier={strategyTier ?? 'null'} />
  ),
}));

vi.mock('../TechnicalDetailsPanel', () => ({
  TechnicalDetailsPanel: ({
    debugSteps,
    isAdmin,
  }: {
    debugSteps: unknown[];
    isAdmin: boolean;
  }) => (
    <div
      data-testid="tech-details"
      data-steps={debugSteps.length}
      data-admin={String(isAdmin)}
    />
  ),
}));

const baseStream: StreamStateForMessages = {
  isStreaming: false,
  currentAnswer: '',
  statusMessage: null,
  strategyTier: null,
  executionId: null,
  debugSteps: [],
  modelDowngrade: null,
};

const defaultProps = {
  streamState: baseStream,
  isEditor: false,
  isAdmin: false,
  isTtsSupported: false,
  ttsEnabled: false,
  isSpeaking: false,
  onSpeak: vi.fn(),
  onStopSpeaking: vi.fn(),
  messagesEndRef: { current: null } as React.RefObject<HTMLDivElement | null>,
};

// Tests will be added in subsequent steps
```

- [ ] **Step 2: Commit scaffolding**

```bash
git checkout -b feature/issue-292-chatmessagelist-refactor
git config branch.feature/issue-292-chatmessagelist-refactor.parent main-dev
git add apps/web/src/components/chat-unified/__tests__/ChatMessageList.characterization.test.tsx
git commit -m "test(chat): scaffold characterization test file with observable mocks (#292)"
```

- [ ] **Step 3: Verify existing 15 tests still pass**

```bash
pnpm -C apps/web test ChatMessageList --run
```

**Expected:** 15 pre-existing tests still pass. New file has 0 tests so far (compiles).

### A.1.2 — HIGH priority tests (6 new, 1 skip as duplicate)

- [ ] **Step 4: Test `model_downgrade_local_fallback_banner`**

⚠️ **Pre-check:** Before writing this test, grep the source to find the actual testid:

```bash
grep -n 'data-testid' apps/web/src/components/chat-unified/ChatMessageList.tsx | grep -i downgrade
```

If no `model-downgrade-banner` testid exists, **add one to the source** as part of this commit (it's a characterization improvement).

Test:

```tsx
it('renders model downgrade banner when stream has local fallback', () => {
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

- [ ] **Step 5: Run and verify GREEN**

```bash
pnpm -C apps/web test ChatMessageList --run
# Expected: 15 existing + 1 new = 16 passing
```

- [ ] **Step 6: Test `model_downgrade_upgrade_message`**

⚠️ **Pre-check:** verify the `role="link"` and href for the upgrade link. If the source uses a different aria-label, adjust the selector.

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
  const link = screen.getByRole('link', { name: /premium|pricing|upgrade/i });
  expect(link).toHaveAttribute('href', '/pricing');
});
```

- [ ] **Step 7: Test `tts_speaker_button_conditional_render`**

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

- [ ] **Step 8: Test `citations_multiple_per_message`**

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
  expect(card).toHaveTextContent('cit-1');
  expect(card).toHaveTextContent('cit-2');
  expect(card).toHaveTextContent('cit-3');
});
```

- [ ] **Step 9: Test `last_assistant_message_strategy_tier_badge`**

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
  expect(badges).toHaveLength(1);
  expect(badges[0]).toHaveAttribute('data-tier', 'Balanced');
});

it('does NOT render ResponseMetaBadge during streaming', () => {
  const messages: ChatMessageItem[] = [
    { id: 'a1', role: 'assistant', content: 'Response' },
  ];
  render(
    <ChatMessageList
      {...defaultProps}
      messages={messages}
      streamState={{ ...baseStream, strategyTier: 'Balanced', isStreaming: true }}
    />
  );
  expect(screen.queryByTestId('strategy-badge')).toBeNull();
});
```

- [ ] **Step 10: Test `window_slide_exact_boundary_51_messages`**

```tsx
function makeMessages(count: number): ChatMessageItem[] {
  return Array.from({ length: count }, (_, i) => ({
    id: `msg-${i}`,
    role: (i % 2 === 0 ? 'user' : 'assistant') as 'user' | 'assistant',
    content: `Messaggio numero ${i}`,
  }));
}

it('handles exact WINDOW_SIZE+1 boundary (51 messages)', () => {
  const messages = makeMessages(51);
  render(<ChatMessageList {...defaultProps} messages={messages} />);
  expect(screen.getAllByTestId(/^message-/).length).toBe(50);
  expect(screen.getByText('Messaggio numero 50')).toBeInTheDocument();
  expect(screen.queryByText('Messaggio numero 0')).toBeNull();
  expect(
    screen.getByRole('button', { name: /1 messaggi? precedenti/ })
  ).toBeInTheDocument();
});
```

- [ ] **Step 11: Note — `feedback_buttons_only_on_assistant` is a duplicate**

Already covered by PR #307's `does NOT render feedback buttons on user messages`. **Skip.**

- [ ] **Step 12: Run all tests — expect GREEN**

```bash
pnpm -C apps/web test ChatMessageList --run
# Expected: 15 existing + 6 new = 21 passing
```

- [ ] **Step 13: Commit HIGH priority tests**

```bash
git add apps/web/src/components/chat-unified/__tests__/ChatMessageList.characterization.test.tsx
git commit -m "test(chat): add HIGH priority characterization tests (#292)

Adds 6 HIGH priority characterization tests from G0.1 audit in a
separate characterization test file to avoid mock coupling with
the existing ChatMessageList.test.tsx windowed slice tests.

Tests added:
- model_downgrade_local_fallback_banner
- model_downgrade_upgrade_message
- tts_speaker_button_conditional_render
- citations_multiple_per_message
- last_assistant_message_strategy_tier_badge
- does_not_render_strategy_badge_during_streaming
- window_slide_exact_boundary_51_messages

Skipped as duplicate of PR #307:
- feedback_buttons_only_on_assistant

Refs meepleAi-app/meepleai-monorepo#292"
```

### A.1.3 — MEDIUM priority tests (3 new, 2 skip as duplicates)

- [ ] **Step 14: Test `technical_details_panel_visibility`**

```tsx
it('renders TechnicalDetailsPanel only when editor + isLastAssistant + debugSteps.length > 0', () => {
  const msg: ChatMessageItem = { id: 'a', role: 'assistant', content: 'Response' };

  // Case 1: not editor → hidden
  const { rerender } = render(
    <ChatMessageList
      {...defaultProps}
      messages={[msg]}
      isEditor={false}
      streamState={{ ...baseStream, debugSteps: [{ step: 'mock' } as never] }}
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
      streamState={{ ...baseStream, debugSteps: [{ step: 'mock' } as never] }}
    />
  );
  const panel = screen.getByTestId('tech-details');
  expect(panel).toHaveAttribute('data-steps', '1');
  expect(panel).toHaveAttribute('data-admin', 'true');
});
```

- [ ] **Step 15: Test `empty_citations_list_no_render`**

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

it('does NOT render RuleSourceCard when citations is undefined', () => {
  const msg: ChatMessageItem = {
    id: 'a',
    role: 'assistant',
    content: 'No sources',
  };
  render(<ChatMessageList {...defaultProps} messages={[msg]} />);
  expect(screen.queryByTestId('rule-source-card')).toBeNull();
});
```

- [ ] **Step 16: Test `message_content_whitespace_preserved` (behavior-based, not className-based)**

**Fixed per Crispin feedback:** Don't rely on Tailwind class name.

⚠️ **jsdom limitation:** `window.getComputedStyle()` in jsdom **often returns empty strings** for Tailwind-applied styles because jsdom doesn't process the CSS layer. Use a defensive test with 2 strategies:

```tsx
it('preserves whitespace in message content', () => {
  const msg: ChatMessageItem = {
    id: 'a',
    role: 'assistant',
    content: 'Line 1\n\nLine 3',
  };
  render(<ChatMessageList {...defaultProps} messages={[msg]} />);
  const element = screen.getByText(/Line 1/);

  // Strategy 1: check computed style (works in real browsers + some jsdom setups)
  const computedStyle = window.getComputedStyle(element);
  const whiteSpaceProp = computedStyle.whiteSpace;

  if (whiteSpaceProp && whiteSpaceProp !== '') {
    // Strategy 1 worked — assert directly
    expect(['pre-wrap', 'pre', 'break-spaces']).toContain(whiteSpaceProp);
  } else {
    // Strategy 1 failed (jsdom limitation) — fall back to DOM content check
    // The text content should still contain the newlines
    expect(element.textContent).toContain('\n');
    // And the element's class attribute should contain a whitespace-* utility
    // (this is fragile but only runs as fallback)
    const classAttr = element.getAttribute('class') ?? '';
    expect(classAttr).toMatch(/whitespace-(pre|pre-wrap|break-spaces)/);
  }
});
```

**Pre-flight check:** Before committing this test, verify which strategy actually runs in the project's vitest config:

```bash
pnpm -C apps/web test -t 'preserves whitespace in message content' --run 2>&1 | grep -i 'strategy'
```

If both strategies fail consistently, consider removing this test entirely — the behavior can be verified visually in the dev server smoke test (Step 27) instead.

- [ ] **Step 17: Run all tests**

```bash
pnpm -C apps/web test ChatMessageList --run
# Expected: 15 + 6 (HIGH) + 4 (MEDIUM) = 25 passing (the MEDIUM set has 3 unique + 1 extra undefined-citations edge case)
```

- [ ] **Step 18: Commit MEDIUM priority tests**

```bash
git commit -am "test(chat): add MEDIUM priority characterization tests (#292)

Adds 3 MEDIUM priority characterization tests:
- technical_details_panel_visibility (3 conditional branches)
- empty_citations_list_no_render (empty array)
- empty_citations_list_no_render (undefined)
- message_content_whitespace_preserved (behavior-based check)

Skipped as duplicates of PR #307:
- streaming_status_message_display
- feedback_gameId_null_hides_buttons

The whitespace test uses getComputedStyle instead of className check
to avoid coupling to Tailwind class names (Crispin/review feedback).

Refs meepleAi-app/meepleai-monorepo#292"
```

## Phase A.2 — Refactor ChatMessageList

**Pre-condition:** Phase A.0 spike report merged. Chosen refactor option (α/β/γ) drives this phase.

**The steps below assume Option α (ChatMessage compatible as-is).** If Option β or γ was chosen, these steps are modified per the spike report — Phase A.2 is a **pointer to the spike, not a standalone prescription**.

### A.2.1 — Shared helper: isLastAssistantMessage

**Gap fix:** v1 referenced `isLastAssistantMessage` without defining it. Define it here.

- [ ] **Step 19: Create helper file**

File: `apps/web/src/components/chat-unified/utils/isLastAssistantMessage.ts`

```ts
import type { ChatMessageItem } from '../ChatMessageList';

/**
 * Returns true if the message at `index` is the last assistant message in the list.
 *
 * Used by ChatMessageList to decide which message should render the strategy
 * tier badge and technical details panel.
 */
export function isLastAssistantMessage(
  messages: ChatMessageItem[],
  index: number
): boolean {
  const msg = messages[index];
  if (!msg || msg.role !== 'assistant') return false;
  // Find the last message with role='assistant' and compare its index
  for (let i = messages.length - 1; i >= 0; i--) {
    if (messages[i].role === 'assistant') {
      return i === index;
    }
  }
  return false;
}
```

- [ ] **Step 20: Write test**

File: `apps/web/src/components/chat-unified/utils/__tests__/isLastAssistantMessage.test.ts`

```ts
import { describe, it, expect } from 'vitest';

import { isLastAssistantMessage } from '../isLastAssistantMessage';

import type { ChatMessageItem } from '../../ChatMessageList';

describe('isLastAssistantMessage', () => {
  const u = (id: string): ChatMessageItem => ({ id, role: 'user', content: 'q' });
  const a = (id: string): ChatMessageItem => ({ id, role: 'assistant', content: 'a' });

  it('returns false for user messages', () => {
    const msgs = [u('u1'), a('a1')];
    expect(isLastAssistantMessage(msgs, 0)).toBe(false);
  });

  it('returns true for the last assistant message', () => {
    const msgs = [u('u1'), a('a1'), u('u2'), a('a2')];
    expect(isLastAssistantMessage(msgs, 3)).toBe(true);
  });

  it('returns false for non-last assistant messages', () => {
    const msgs = [u('u1'), a('a1'), u('u2'), a('a2')];
    expect(isLastAssistantMessage(msgs, 1)).toBe(false);
  });

  it('returns true when last message is assistant and index points to it', () => {
    const msgs = [u('u1'), a('a1')];
    expect(isLastAssistantMessage(msgs, 1)).toBe(true);
  });

  it('returns false for out-of-range index', () => {
    const msgs = [u('u1'), a('a1')];
    expect(isLastAssistantMessage(msgs, 5)).toBe(false);
  });

  it('returns false for empty list', () => {
    expect(isLastAssistantMessage([], 0)).toBe(false);
  });

  it('returns false when no assistant messages exist', () => {
    const msgs = [u('u1'), u('u2')];
    expect(isLastAssistantMessage(msgs, 1)).toBe(false);
  });
});
```

- [ ] **Step 21: Run + commit**

```bash
pnpm -C apps/web test isLastAssistantMessage --run
git add apps/web/src/components/chat-unified/utils/
git commit -m "refactor(chat): extract isLastAssistantMessage helper (#292)

Needed by the Phase A.2 composition refactor to decide where to render
the strategy tier badge and technical details panel. Has 7 unit tests
covering edge cases.

Refs meepleAi-app/meepleai-monorepo#292"
```

### A.2.2 — Type adapter (toChatMessageProps)

**IMPORTANT:** The adapter's return type MUST be based on the **actual** `ChatMessageProps` from the spike report (A.0). The sketch below assumes Option α. Adjust per spike findings.

- [ ] **Step 22: Create adapter (per spike findings)**

File: `apps/web/src/components/chat-unified/utils/toChatMessageProps.ts`

```ts
/**
 * Adapter: ChatMessageItem → ChatMessageProps.
 *
 * See docs/development/chat-message-api-compatibility.md for the field-by-field
 * mapping rationale (spike report from Phase A.0).
 */

import type { ChatMessageItem } from '../ChatMessageList';
import type { ChatMessageProps } from '@/components/ui/meeple/chat-message';

// ⚠️ This interface DEPENDS ON THE SPIKE OUTPUT.
// The fields below are a placeholder — replace with actual ChatMessageProps
// structure from chat-message.tsx after running Phase A.0.
interface AdapterContext {
  gameTitle?: string;
  // Only include context fields that map to existing ChatMessageProps slots.
  // Do NOT invent fields (e.g., feedbackValue) unless A.0 confirmed they exist.
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars -- intentional #292-SPIKE-STUB; replace per A.0 spike before committing A.2.3
export function toChatMessageProps(
  item: ChatMessageItem,
  _ctx: AdapterContext
): ChatMessageProps {
  // #292-SPIKE-STUB: Actual implementation depends on the spike outcome.
  // Replace with mapping derived from docs/development/chat-message-api-compatibility.md
  // before moving past Step 24 (adapter commit).
  throw new Error(
    '[#292] Adapter stub — replace with spike-derived implementation before committing A.2.3'
  );
}
```

**After A.0:** Replace the stub body with the mapping derived from the spike report, then write tests. Remove the `#292-SPIKE-STUB` comment + eslint-disable.

**CI guard (Fowler amendment):** Add to Phase A.3 (Step 35) a grep check:

```bash
# In A.3.2 final verification, before typecheck:
if grep -rn "#292-SPIKE-STUB" apps/web/src --include="*.ts" --include="*.tsx"; then
  echo "ERROR: spike stubs not replaced — abort commit"
  exit 1
fi
```

This prevents accidentally shipping the `throw new Error` stub to production.

- [ ] **Step 23: Write adapter tests (per spike outputs)**

Tests mirror whatever the spike report documented. Include edge cases:
- `undefined` citations vs empty `[]`
- `timestamp` propagation
- Role pass-through
- Malformed citations (missing `label` or `page`) → defensive handling

- [ ] **Step 24: Run + commit**

```bash
pnpm -C apps/web test toChatMessageProps --run
git add apps/web/src/components/chat-unified/utils/toChatMessageProps.ts \
        apps/web/src/components/chat-unified/utils/__tests__/toChatMessageProps.test.ts
git commit -m "refactor(chat): add ChatMessageItem → ChatMessage adapter (#292)

Implements the mapping per docs/development/chat-message-api-compatibility.md
(spike report). Includes defensive handling for undefined/malformed citations.

Refs meepleAi-app/meepleai-monorepo#292"
```

### A.2.3 — Replace inline message block with `<ChatMessage>`

**Pre-condition:** Steps 19-24 complete. Refactor strategy chosen (α/β/γ).

- [ ] **Step 25: Modify ChatMessageList.tsx to use composition**

Pseudo-diff (actual code depends on chosen option):

```tsx
// BEFORE (simplified):
{windowedMessages.map((msg, localIdx) => {
  const absoluteIdx = windowStart + localIdx;
  const isLast = isLastAssistantMessageOld(messages, absoluteIdx); // inline logic
  return (
    <div key={msg.id} className={cn(...)}>
      {/* 70+ lines of inline JSX */}
    </div>
  );
})}

// AFTER (Option α):
{windowedMessages.map((msg, localIdx) => {
  const absoluteIdx = windowStart + localIdx;
  const isLast = isLastAssistantMessage(messages, absoluteIdx);
  return (
    <React.Fragment key={msg.id}>
      <ChatMessage {...toChatMessageProps(msg, { gameTitle })} />

      {/* Context-dependent children kept OUTSIDE <ChatMessage> */}
      {msg.role === 'assistant' && !!gameId && !!threadId && (
        <FeedbackButtons
          value={feedbackMap.get(msg.id) ?? null}
          onFeedbackChange={(v, c) => handleFeedback(msg.id, v, c)}
          isLoading={feedbackLoadingMap.get(msg.id) ?? false}
          showCommentOnNegative
          size="sm"
        />
      )}
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
    </React.Fragment>
  );
})}
```

**Note:** Feedback buttons are kept in the orchestrator (not inside `<ChatMessage>`) because `ChatMessage` atom does not own the feedback state (per spike A.0, most likely finding).

- [ ] **Step 26: Run all tests — expect GREEN (safety net check)**

```bash
pnpm -C apps/web test ChatMessageList --run
```

**If any test fails: STOP.** The refactor introduced a regression. Fix before continuing.

- [ ] **Step 27: Manual smoke test on dev server**

Scripted checklist:
1. Navigate to chat thread, send message → receive streaming response ✅
2. Verify citations render from RuleSourceCard ✅
3. Click helpful feedback → see loading state → verify API call (Network tab) ✅
4. Click not-helpful → enter comment → verify API call with `outcome: 'not_helpful'` ✅
5. Trigger model downgrade (test fixture or rate limit) → see banner ✅
6. Switch TTS on (settings) → verify speak button appears ✅
7. Navigate between 3 threads → feedback state isolated ✅
8. Load thread with 60+ messages → window slide works ✅

- [ ] **Step 28: Commit Phase A.2.3**

```bash
git commit -am "refactor(chat): compose ChatMessageList with ChatMessage atom (#292)

Replaces ~70 lines of per-message inline JSX with composition:
  <ChatMessage {...toChatMessageProps(msg, ctx)} />

Keeps orchestration logic in ChatMessageList:
- Windowing (WINDOW_SIZE=50 slice)
- Per-message feedback state (feedbackMap, feedbackLoadingMap)
- handleFeedback API round-trip
- isLastAssistant derivation (via extracted helper)
- FeedbackButtons (outside <ChatMessage> — atom doesn't own feedback)
- TechnicalDetailsPanel (context-dependent on isLast + debugSteps)
- ResponseMetaBadge (context-dependent on isLast + strategyTier)

All 25 characterization tests still pass.

Refs meepleAi-app/meepleai-monorepo#292"
```

### A.2.4 — [OPTIONAL per spike] MeepleAvatar state wiring

**Skip if:** Phase A.0 spike determined that `ChatMessage` does NOT own avatar rendering (most likely case per feedback structure). In that case, leave avatar handling as-is in the orchestrator.

**Execute if:** Phase A.0 spike determined that `ChatMessage` exposes an avatar state slot.

Steps omitted — reference v1 plan Steps 28-33 for the `deriveMeepleAvatarState` helper if needed.

### A.2.5 — Cleanup orphan annotations

- [ ] **Step 29: Remove `@status ORPHAN` from chat-message.tsx**

- [ ] **Step 30: Remove `@status ORPHAN` from meeple-avatar.tsx** (only if actually consumed — per spike; otherwise leave orphan annotation)

- [ ] **Step 31: Remove `[ORPHAN]` from showcase metadata for chat-message** and maybe meeple-avatar

File: `apps/web/src/components/showcase/stories/metadata.ts`

- [ ] **Step 32: Commit cleanup**

```bash
git commit -am "chore(showcase): promote ChatMessage from orphan status (#292)"
```

## Phase A.3 — Final verification + PR

### A.3.1 — Metrics verification (Wiegers)

- [ ] **Step 33: Verify LOC target**

```bash
wc -l apps/web/src/components/chat-unified/ChatMessageList.tsx
```

**Pass criteria:** ≤ revised target from A.0 spike (may be 150, may be higher if Option β/γ was chosen).

- [ ] **Step 34: Verify test coverage**

```bash
pnpm -C apps/web test:coverage --run \
  --coverage.include='apps/web/src/components/chat-unified/ChatMessageList.tsx' \
  --coverage.include='apps/web/src/components/chat-unified/utils/*.ts'
```

**Pass criteria:**
- `ChatMessageList.tsx`: ≥ 85% line coverage, ≥ 80% branch coverage
- `toChatMessageProps.ts`: ≥ 90% line + branch (pure function)
- `isLastAssistantMessage.ts`: 100% line + branch (covered by 7 unit tests)

### A.3.2 — Full suite + typecheck + lint

- [ ] **Step 35: Run full frontend test suite**

```bash
pnpm -C apps/web test --run
```

All tests must pass. No new failures, no newly-skipped tests.

- [ ] **Step 36: Typecheck + lint**

```bash
pnpm -C apps/web typecheck
pnpm -C apps/web lint
```

Both must be clean.

### A.3.3 — PR + review + merge

- [ ] **Step 37: Push branch**

```bash
git push -u origin feature/issue-292-chatmessagelist-refactor
```

- [ ] **Step 38: Open PR against main-dev**

```bash
gh pr create --repo meepleAi-app/meepleai-monorepo \
  --base main-dev \
  --head feature/issue-292-chatmessagelist-refactor \
  --title "refactor(chat): compose ChatMessageList with ChatMessage atom" \
  --body "<see pr-body-292.md template below>"
```

PR body must include:
- Link to spike report (A.0)
- LOC before/after
- Coverage delta
- Link to all 25 characterization tests
- Chosen option (α/β/γ) from spike
- Post-merge smoke test checklist

- [ ] **Step 39: Request architect code review (REQUIRED)**

```bash
gh pr edit <PR_NUMBER> --add-reviewer <architect-handle>
```

**Do NOT auto-merge.** This refactor is HIGH risk.

- [ ] **Step 40: Address review feedback, iterate until approved**

- [ ] **Step 41: Post-merge structured smoke test (Nygard)**

After merge, deploy to staging. Execute this checklist (do NOT skip):

**Smoke test — staging only:**

1. [ ] **E2E (Playwright):** Send message → streaming response with ≥1 citation → feedback helpful → verify `helpful` in Network tab → pass
2. [ ] **Manual:** Submit not-helpful with comment → verify `not_helpful` + comment in payload
3. [ ] **Manual:** Force model downgrade (use test API key or rate limit) → verify banner appears with `llama3` or `gpt-3.5` text
4. [ ] **Manual:** Navigate between 5 threads → feedback state isolated (clicked feedback on thread A does NOT affect thread B)
5. [ ] **Manual:** Mobile viewport (375px) → feedback buttons remain usable, no layout break
6. [ ] **Manual:** Browser: Chrome latest + Safari latest (macOS)

**Timeout:** 30 min. **Any failure → revert PR.**

- [ ] **Step 42: Merge PR (human approved)**

```bash
gh pr merge <PR_NUMBER> --merge --delete-branch
```

- [ ] **Step 43: Remove temporary CODEOWNERS lock**

```bash
git checkout main-dev
git pull --ff-only origin main-dev
# Edit .github/CODEOWNERS to remove the chat-unified temp entry
git add .github/CODEOWNERS
git commit -m "chore(codeowners): remove temporary chat-unified lock (#292 merged)"
git push origin main-dev
```

- [ ] **Step 44: Close lock tracking issue**

```bash
gh issue close <LOCK_ISSUE_NUMBER> --repo meepleAi-app/meepleai-monorepo \
  --comment "Unlocked — #292 refactor merged. Normal development resumes."
```

### DoD for Track A

- [ ] Phase A.0 spike report merged
- [ ] 6 HIGH + 3 MEDIUM + 2 extra characterization tests added (total ~25 in characterization file)
- [ ] All tests passing (full frontend suite)
- [ ] `ChatMessageList.tsx` LOC ≤ revised target from spike
- [ ] `ChatMessageList.tsx` coverage ≥ 85% line, ≥ 80% branch
- [ ] Adapter + helper unit tests at ≥ 90% coverage
- [ ] Architect code review approved
- [ ] Post-merge staging smoke test checklist complete
- [ ] Temporary CODEOWNERS lock removed
- [ ] Lock tracking issue closed
- [ ] Issue #292 closed by merge

---

# Track B — #294 AgentStatsDisplay Unblock

**Issue:** meepleAi-app/meepleai-monorepo#294 (currently CLOSED as blocked)
**Status:** ⏸ Requires human decision
**🗓 Decision deadline:** 14 days from this plan's acceptance (otherwise auto-execute Path C)

## The problem (recap)

`AgentStatsDisplay` consumes `AgentMetadata` type which requires `invocationCount`, `capabilities`, `avgResponseTime`. `AgentDefinitionDto` (actual backend schema) does not expose these.

## User story validation (Cockburn — mandatory before choosing a path)

**Fill in before choosing:**

```
As a _____________,
I want _____________,
So that _____________.

Acceptance criteria:
- _____________
- _____________

Evidence that this story is real (pick one):
[ ] User research interview
[ ] Support ticket / complaint
[ ] Analytics showing users attempting this workflow
[ ] Stakeholder request (document from whom)
[ ] Roadmap commitment (link)
```

**If none of the above can be filled in → Path C (delete) is correct.**

## Decision tree

### Path A — Extend backend with telemetry endpoint

**Effort:** M (backend + frontend, ~10-14h)
**Risk:** MEDIUM
**Value:** HIGHEST (enables future telemetry features)
**Prerequisite:** User story validated + architect sign-off

**Architecture (Fowler — explicit BC decision required):**

Available BCs from CLAUDE.md: Administration, AgentMemory, Authentication, BusinessSimulations, DatabaseSync, DocumentProcessing, EntityRelationships, Gamification, **GameManagement**, GameToolbox, GameToolkit, KnowledgeBase, SessionTracking, SharedGameCatalog, SystemConfiguration, UserLibrary, UserNotifications, WorkflowIntegration.

**No dedicated "AgentMetrics" BC exists.** Place the metrics query in:
- **Administration BC** — fits "audit, analytics" responsibility (CLAUDE.md line on Administration)
- OR create a read-model projection that lives outside any BC (CQRS query side)

**Recommended:** `Administration/Application/Queries/GetAgentMetricsQuery.cs` — it's audit-flavored data.

**Data source:**
- Option A1: new `agent_invocations` table populated by existing request middleware (add migration + instrumentation)
- Option A2: aggregate from existing request logs (if already structured — check `docs/operations/operations-manual.md`)
- Option A3: aggregate from Sentry/APM (skip database entirely)

**Steps:**

1. Validate user story (above)
2. Choose data source (A1/A2/A3) — document in ADR
3. **Backend:**
   - Create `Administration/Application/Queries/GetAgentMetricsQuery.cs`
   - Create handler + repository method
   - Add endpoint `GET /api/admin/agent-definitions/{id}/metrics`
   - Add integration test using Testcontainers (CLAUDE.md test pattern)
4. **Frontend:**
   - Add API client method in `agent-definitions.api.ts`
   - Create mapper `mapAgentMetricsToMetadata` (uses actual `AgentMetrics` DTO + derives capabilities from `tools[]`)
   - Create capabilities mapping table: `{'RAG': ['kb-search', 'vector-query'], 'Vision': ['image-analysis'], ...}`
   - Update `BuilderTable` with new stats column
5. Remove `@status ORPHAN` annotations
6. **Open 2 PRs:** backend first, frontend depends on backend merge
7. Reopen #294 → close via frontend PR

**DoD for Path A:**
- [ ] User story documented and signed off
- [ ] ADR for data source choice
- [ ] Backend endpoint + integration test (≥ 1 happy path)
- [ ] Frontend mapper unit test
- [ ] Capabilities derivation table tested
- [ ] BuilderTable column visible on `/admin/agents/definitions`
- [ ] No regression in existing admin agent list tests
- [ ] Typecheck + lint clean

### Path B — Refactor component to accept AgentDefinitionDto

**Effort:** S (~4h)
**Risk:** LOW
**Value:** MEDIUM
**Warning:** Creates a degraded version of an already-degraded component. **Spec-panel recommends AVOIDING this path.**

Steps unchanged from v1. If chosen, read v1 plan.

### Path C — Delete component entirely (recommended default)

**Effort:** S (~2h)
**Risk:** NONE
**Value:** code hygiene

**Steps:**

- [ ] **Step 1: Delete files**

```bash
rm apps/web/src/components/ui/agent/AgentStatsDisplay.tsx
rm -f apps/web/src/components/ui/agent/__tests__/AgentStatsDisplay.test.tsx
rm apps/web/src/components/showcase/stories/agent-stats-display.story.tsx
```

- [ ] **Step 2: Remove from showcase registry**

Edit `apps/web/src/components/showcase/stories/index.ts`:
- Remove `import { agentStatsDisplayStory } from './agent-stats-display.story';`
- Remove `agentStatsDisplayStory,` from `ALL_STORIES`

Edit `apps/web/src/components/showcase/stories/metadata.ts`:
- Remove the `agent-stats-display` entry

- [ ] **Step 3: Remove from admin/ui-library registries**

```bash
grep -n "AgentStatsDisplay" apps/web/src/components/admin/ui-library/component-map.ts
grep -n "agent-stats-display" apps/web/src/config/component-registry.ts
```

Remove matching import + entries.

- [ ] **Step 4: Check if AgentMetadata type is still used**

```bash
grep -rl "AgentMetadata" apps/web/src --include="*.tsx" --include="*.ts" | grep -v __tests__
```

If `AgentMetadata` is only used by the (now deleted) AgentStatsDisplay, **also delete the type from `types/agent.ts`**.

- [ ] **Step 5: Typecheck + lint**

```bash
pnpm -C apps/web typecheck
pnpm -C apps/web lint
```

- [ ] **Step 6: Commit + PR + merge**

```bash
git checkout -b feature/issue-294-agent-stats-display-delete
git config branch.feature/issue-294-agent-stats-display-delete.parent main-dev
git add -A
git commit -m "chore(agent): delete unused AgentStatsDisplay orphan (#294)

Phase B Path C of follow-up plan. User story validation failed —
no forcing function for agent telemetry display. YAGNI applied.

AgentMetadata type backend schema mismatch documented in issue #294
comment thread. Component restorable from git history if telemetry
endpoint is later built.

Refs meepleAi-app/meepleai-monorepo#294
Refs docs/development/orphan-components-follow-up-plan-v2.md Track B Path C"

git push -u origin feature/issue-294-agent-stats-display-delete
gh pr create --base main-dev --head feature/issue-294-agent-stats-display-delete \
  --title "chore(agent): delete unused AgentStatsDisplay orphan" \
  --body "..."
```

**DoD for Path C:**
- [ ] 3 files deleted
- [ ] 5 registry entries removed
- [ ] `AgentMetadata` type reviewed (deleted if unused)
- [ ] Typecheck + lint clean
- [ ] PR merged to main-dev
- [ ] #294 comment references the delete PR

## Decision required (deadline: +14 days)

- [ ] Fill in user story template above
- [ ] Choose Path A / B / C
- [ ] If Path A: architect sign-off on BC placement + data source
- [ ] If Path B: open sub-ticket explaining trade-off acceptance
- [ ] If Path C: execute immediately

**Auto-execution clause:** If no decision is signed within 14 days of this plan being accepted, **Path C auto-executes**. This prevents the component from remaining orphan indefinitely.

**Signed:** ________________
**Date:** ________________

---

# Track C — #293 PageTransition Post-merge Validation

**Issue:** meepleAi-app/meepleai-monorepo#293 (already closed via PR #304)
**Effort:** S (6-8h) + 24h observation + 7d production gate
**Owner requirement:** engineer with staging SSH access + UX sign-off authority
**Branch (if changes needed):** `chore/page-transition-staging-validation`

## 🚧 Prerequisite: Named UX motion owner (Wiegers amendment)

**This track requires a named UX motion owner** to sign off on the visual feel (Phase C.5 gate). As of this plan's writing, no such owner is documented in `docs/` or `CLAUDE.md`.

- [ ] **Step 0: Identify or establish UX motion ownership**

Check in priority order:
1. `docs/frontend/` for an OWNERS or design-system-owner file
2. GitHub repo `CODEOWNERS` for `apps/web/src/components/ui/animations/`
3. Team roster / org chart

**If no owner exists:** file a meta-ticket before proceeding:

```bash
gh issue create --repo meepleAi-app/meepleai-monorepo \
  --title "meta: establish UX motion owner for PageTransition rollout (#293)" \
  --body "Track C of orphan-components-follow-up-plan-v2.md requires a named UX owner to sign off on the visual feel of page transitions. No such role is documented today. Please assign before Track C can proceed." \
  --label "coordination,ux"
```

**STOP** Track C until this meta-ticket is resolved. Tracks A and B are unaffected.

**Why:** PR #304 merged with `NEXT_PUBLIC_ENABLE_PAGE_TRANSITIONS=false`. Real validation requires staging deploy with flag ON.

## Success criteria (Wiegers — explicit gates)

**Production rollout requires ALL of these to pass:**

1. **Manual QA (5 test cases):** 5/5 PASS (see C.2 below)
2. **Lighthouse delta:**
   - Performance score: ≤ 5 point drop
   - LCP: ≤ 100ms increase
   - CLS: ≤ 0.05 increase
3. **Hydration check:** zero warnings in browser console over 20 consecutive navigations
4. **Error budget:** Sentry error rate for `/chat` routes ≤ baseline + 5% over 24h of staging exposure
5. **UX sign-off:** motion UX owner approves the visual feel

## Failure taxonomy (Nygard)

When a gate fails, classify the severity:

| Severity | Definition | Action |
|---|---|---|
| **BLOCKER** | Hydration mismatch, layout shift > 0.25, any console error from transition code, regression in E2E suite | **Rollback immediately.** Flag OFF, rebuild, investigate. |
| **DEGRADATION** | Lighthouse drop 5-10%, minor visual glitches, motion nausea feedback | Document + fix before production. Re-run tests after fix. |
| **PAPERCUT** | Motion feels "off", edge cases in back nav (e.g., nested modals) | Ship anyway, file follow-up issue with label `polish`. |

## Phase C.1 — Staging deploy with flag enabled

**Prerequisite:** SSH access to staging server (meepleai.app)

- [ ] **Step 1: Sync current staging secrets**

```bash
cd /d/Repositories/meepleai-monorepo-backend/infra
make secrets-sync
cat secrets/web.secret | grep -i NEXT_PUBLIC || echo "No NEXT_PUBLIC_* in web.secret yet"
```

- [ ] **Step 2: SSH to staging and enable flag**

```bash
ssh meepleai.app
cd ~/meepleai/infra
# Backup current secret
cp secrets/web.secret secrets/web.secret.bak-$(date +%Y%m%d)
# Add or update flag
sed -i '/^NEXT_PUBLIC_ENABLE_PAGE_TRANSITIONS=/d' secrets/web.secret
echo "NEXT_PUBLIC_ENABLE_PAGE_TRANSITIONS=true" >> secrets/web.secret
```

- [ ] **Step 3: Rebuild web image (build-time flag)**

```bash
# Still on staging server
cd ~/meepleai/infra
make staging-rebuild-web 2>/dev/null || docker compose -f docker-compose.staging.yml up -d --build web
```

**Note:** If `make staging-rebuild-web` doesn't exist (verify with `make help` on staging), use the docker compose fallback. If even the fallback doesn't work, **STOP** and investigate the staging Makefile before continuing.

- [ ] **Step 4: Verify the flag is active in the deployed bundle**

```bash
# On local machine
curl -s https://staging.meepleai.app/chat 2>/dev/null | grep -c "ENABLE_PAGE_TRANSITIONS" || echo "not visible in HTML (expected — it's in JS bundle)"
# Alternative: open https://staging.meepleai.app/chat in Chrome DevTools
# Sources → find chunk containing 'ENABLE_PAGE_TRANSITIONS' → verify literal 'true'
```

## Phase C.2 — Manual QA (5 test cases)

**Browser matrix:** Chrome latest + Safari latest (macOS) — minimum
**Viewport:** desktop (1920×1080) + mobile (375×667)

- [ ] **Step 5: Test case 1 — Empty → thread**

1. Open https://staging.meepleai.app/chat (no active thread)
2. Click "New thread"
3. Observe: fade-in animation on the new thread view
4. **Pass criteria:** smooth fade (~300ms), no flicker, no missing content flash

Result: ⬜ PASS / ⬜ FAIL — Severity: ___ — Notes: ___

- [ ] **Step 6: Test case 2 — Thread A → Thread B switch**

1. Have ≥ 2 threads in sidebar
2. Click Thread A (wait for load)
3. Click Thread B
4. **Pass criteria:** visible fade-out of A + fade-in of B (~300ms total), no layout shift, scroll position reset

Result: ⬜ PASS / ⬜ FAIL — Severity: ___ — Notes: ___

- [ ] **Step 7: Test case 3 — Back navigation**

1. Navigate to Thread A → Thread B (via sidebar click)
2. Browser back button
3. **Pass criteria:** smooth transition, correct history stack, Thread A content restored

Result: ⬜ PASS / ⬜ FAIL — Severity: ___ — Notes: ___

- [ ] **Step 8: Test case 4 — Streaming during navigation**

1. Open Thread A, send a message
2. While streaming (SSE in progress), click Thread B
3. **Pass criteria:** no UI corruption, streaming aborts cleanly (no stuck indicator), Thread B renders normally

Result: ⬜ PASS / ⬜ FAIL — Severity: ___ — Notes: ___

- [ ] **Step 9: Test case 5 — Hydration check (20 navigations)**

1. Open browser DevTools Console
2. Clear console
3. Navigate between 5 threads, 4 times each (20 total)
4. **Pass criteria:** Zero console warnings containing "Hydration failed", "did not match", or "flushSync"

Result: ⬜ PASS / ⬜ FAIL — Severity: ___ — Notes: ___

- [ ] **Step 10: Mobile viewport check**

Repeat tests 1-5 on 375×667 (iPhone SE equivalent, via DevTools emulation or real device if possible).

Result: ⬜ PASS / ⬜ FAIL — Severity: ___ — Notes: ___

- [ ] **Step 11: Document QA results**

Append to `docs/development/page-transition-decision.md`:

```markdown
## Staging validation results (YYYY-MM-DD)

| Test case | Desktop | Mobile | Severity | Notes |
|---|---|---|---|---|
| 1. Empty → thread | ✅/❌ | ✅/❌ | — | |
| 2. Thread switch | ✅/❌ | ✅/❌ | — | |
| 3. Back nav | ✅/❌ | ✅/❌ | — | |
| 4. Streaming interruption | ✅/❌ | ✅/❌ | — | |
| 5. Hydration (20 nav) | ✅/❌ | ✅/❌ | — | |

**Overall:** GO / NO-GO / CONDITIONAL
**Signed:** _____________________
**Date:** _____________________
```

## Phase C.3 — Lighthouse delta check

**Approach fix:** Can't easily A/B the same staging URL without redeploy. Use **local builds** for baseline/flag-on comparison.

- [ ] **Step 12: Local baseline build (flag OFF)**

```bash
cd /d/Repositories/meepleai-monorepo-backend/apps/web
NEXT_PUBLIC_ENABLE_PAGE_TRANSITIONS=false pnpm build
pnpm start &  # local server on :3000
sleep 10
npx lighthouse http://localhost:3000/chat \
  --only-categories=performance \
  --output=json \
  --output-path=./lighthouse-baseline.json \
  --chrome-flags="--headless"
kill %1  # stop local server
```

- [ ] **Step 13: Local flag-on build**

```bash
NEXT_PUBLIC_ENABLE_PAGE_TRANSITIONS=true pnpm build
pnpm start &
sleep 10
npx lighthouse http://localhost:3000/chat \
  --only-categories=performance \
  --output=json \
  --output-path=./lighthouse-flag-on.json \
  --chrome-flags="--headless"
kill %1
```

- [ ] **Step 14: Compare metrics**

```bash
node -e "
const baseline = require('./lighthouse-baseline.json');
const flagOn = require('./lighthouse-flag-on.json');
const metrics = ['performance', 'largest-contentful-paint', 'first-contentful-paint', 'cumulative-layout-shift'];
for (const audit of metrics) {
  const bv = baseline.categories[audit]?.score ?? baseline.audits[audit]?.numericValue;
  const fv = flagOn.categories[audit]?.score ?? flagOn.audits[audit]?.numericValue;
  console.log(audit, '→ baseline:', bv, 'flag on:', fv, 'delta:', fv - bv);
}
"
```

**Pass criteria:**
- Performance score delta: ≥ -0.05 (≤ 5 point drop)
- LCP delta: ≤ +100ms
- FCP delta: ≤ +50ms
- CLS delta: ≤ +0.05

- [ ] **Step 15: Document performance findings**

Append to `page-transition-decision.md`:

```markdown
## Lighthouse delta (YYYY-MM-DD)

| Metric | Baseline (flag OFF) | Flag ON | Delta | Gate | Verdict |
|---|---|---|---|---|---|
| Performance score | X | Y | Z | ≥ -0.05 | PASS/FAIL |
| LCP (ms) | X | Y | Z | ≤ +100 | PASS/FAIL |
| FCP (ms) | X | Y | Z | ≤ +50 | PASS/FAIL |
| CLS | X | Y | Z | ≤ +0.05 | PASS/FAIL |

**Overall:** GO / NO-GO
```

## Phase C.4 — 24h staging error budget observation

- [ ] **Step 16: Record baseline Sentry error rate**

Before enabling flag (Phase C.1), capture 24h of `/chat` route error rate from Sentry dashboard.
Store: `<baseline_rate_per_hour>`.

- [ ] **Step 17: Start 24h observation window**

**Nygard amendment — enforced timestamp gate:**

Append to `docs/development/page-transition-decision.md`:

```markdown
## 24h staging observation (Track C Phase C.4)

- **Flag enabled at:** YYYY-MM-DD HH:MM UTC (record actual timestamp)
- **Earliest Phase C.5 eligibility:** YYYY-MM-DD HH:MM UTC + 24 hours
- **Baseline Sentry error rate (pre-flag 24h):** X errors / hour
- **Observation start Sentry snapshot:** [dashboard link with timestamp]
```

Let the staging environment run with flag ON for 24 hours. Monitor Sentry dashboard.

**Pass criteria:** `error_rate_with_flag ≤ baseline_rate + 5%`

- [ ] **Step 17.5: Verify 24h elapsed before advancing**

**Do NOT proceed to Phase C.5 unless:**

```bash
# On local machine
NOW_UTC=$(date -u +%s)
FLAG_START_UTC=$(date -u -d "<flag enabled timestamp>" +%s)  # fill from Step 17
ELAPSED=$((NOW_UTC - FLAG_START_UTC))

if [ $ELAPSED -lt 86400 ]; then
  REMAINING=$((86400 - ELAPSED))
  echo "❌ Only $ELAPSED seconds elapsed. Wait $REMAINING more seconds before Phase C.5."
  exit 1
else
  echo "✅ 24h elapsed. Proceeding to Phase C.5."
fi
```

**Alternative automation (optional):** configure a GitHub Action to post a comment on #293 exactly 24h after the "flag enabled" timestamp with a "24h observation window complete" confirmation. This creates an audit trail.

**Record end snapshot in decision doc:**

```markdown
- **Observation end at:** YYYY-MM-DD HH:MM UTC
- **End snapshot Sentry rate:** X errors / hour
- **Delta:** +/- X% vs baseline
- **Gate:** PASS / FAIL
```

## Phase C.5 — Production rollout decision

- [ ] **Step 18: Aggregate results**

Fill in this gate checklist:

```
Production rollout gates:
- [ ] Manual QA:           5/5 PASS (no BLOCKER or DEGRADATION)
- [ ] Lighthouse:          all 4 metrics within tolerance
- [ ] Hydration:           0 console warnings over 20 navigations
- [ ] Error budget:        Sentry rate within +5% over 24h
- [ ] UX sign-off:         <owner name> approved visual feel

All gates passed → PROCEED to Step 19 (production rollout)
Any gate failed   → STOP, classify per failure taxonomy, either:
                    - BLOCKER → rollback, investigate
                    - DEGRADATION → fix in follow-up, re-test
                    - PAPERCUT → file follow-up, ship anyway
```

- [ ] **Step 19: Enable flag in production (if all gates pass)**

```bash
# Open PR to enable in production secrets
# OR edit production secret directly via deploy procedure
# Depends on the team's production deploy model (see docs/operations/)
```

- [ ] **Step 20: Post-rollout monitoring (7 days)**

Monitor Sentry + user feedback for 7 consecutive days.

**Success gate for flag removal:**
- [ ] 7+ consecutive days with flag ON in production
- [ ] Sentry error rate for `/chat` route ≤ baseline + 5%
- [ ] Lighthouse performance delta ≤ -5%
- [ ] Zero user-reported bugs tagged `page-transition` or `chat-navigation`
- [ ] Sign-off from: UX owner + SRE on-call

- [ ] **Step 21: Remove feature flag gate (if all success gates met)**

Open PR to always wrap with `<PageTransition>` (remove the env var check):

```tsx
// apps/web/src/app/(chat)/layout.tsx — simplified
export default function ChatLayout({ children }: { children: ReactNode }) {
  return (
    <UserShell>
      <PageTransition variant="fade">{children}</PageTransition>
    </UserShell>
  );
}
```

- [ ] **Step 22: Comment on Issue #293**

```
Post-merge validation complete (Track C of follow-up plan v2).

Staging deploy:  [link to deploy log]
Manual QA:       [5/5 PASS / details]
Lighthouse:      [metric table]
Error budget:    [24h Sentry delta]
Production:      [enabled YYYY-MM-DD / rolled back]
Flag removal:    [YYYY-MM-DD + PR link] / [pending 7-day gate]

Full results: docs/development/page-transition-decision.md
```

## DoD for Track C

- [ ] Staging deploy with flag ON completed
- [ ] Manual QA: 5/5 test cases PASS on desktop + mobile
- [ ] Lighthouse: all 4 metrics within tolerance
- [ ] 24h Sentry error budget: within +5% of baseline
- [ ] UX sign-off documented
- [ ] Production rollout executed (or NO-GO documented)
- [ ] (After 7 days) Feature flag gate removed
- [ ] Issue #293 commented with final results

---

## Execution Matrix

| Track | Blockers | Effort | Parallel with | Agent-delegable? |
|---|---|---|---|---|
| A | A.0 spike (4-6h) | 20-28h total | B, C | Mostly yes; architect review required |
| B | User story validation + decision | 2-14h (path-dep) | A, C | Path C yes; Path A/B human |
| C | Staging access | 6-8h + 24h observation | A, B | Partial |

## Dependencies

```
Phase A.0 (spike) ──────────┐
                            │
Phase A.1 (chars tests) ────┼──── Phase A.2 (refactor) ──── Phase A.3 (PR)
                            │
Phase A.1 and A.0 parallel-ok (independent)

Phase B: decision → path execution (independent of A and C)

Phase C.1 (deploy) → C.2 (QA) → C.3 (Lighthouse) → C.4 (24h obs) → C.5 (rollout)
                    (C.1-C.3 same day, C.4 +24h, C.5 +1-7d)
```

## Risk Register (expanded from v1)

| Risk | Severity | Mitigation |
|---|---|---|
| Phase A.0 spike reveals ChatMessage API incompatibility requiring scope expansion | HIGH | Spike outputs chosen option (α/β/γ); plan adapts. If Option β/γ, split A.2 into sub-PRs |
| Phase A.2 refactor breaks production chat path | CRITICAL | 25 characterization tests + architect review + staging smoke test + CODEOWNERS lock |
| Characterization tests modify shared mocks → break 15 existing tests | HIGH | **SPLIT test file** into `ChatMessageList.test.tsx` + `ChatMessageList.characterization.test.tsx` |
| Parallel PR modifies chat-unified during refactor | MEDIUM | Exclusive lock protocol: pinned issue + CODEOWNERS + PR review checklist |
| Track B decision slips indefinitely | MEDIUM | 14-day auto-execution clause (defaults to Path C) |
| Track C PageTransition hydration mismatch in production | HIGH | Feature flag allows instant rollback; 24h staging observation; Lighthouse gates |
| Track C staging rebuild fails (Makefile drift) | MEDIUM | Document docker compose fallback; verify on staging before starting |
| `whitespace-pre-wrap` test breaks on Tailwind class rename | LOW | Use `getComputedStyle` instead of `className.includes` |
| Lighthouse comparison on same staging URL is impossible | MEDIUM | Use local builds for A/B comparison |

## Self-Review (writing-plans skill)

- [x] **Spec coverage:** 3 tracks + Phase A.0 spike + lock protocol + failure taxonomy
- [x] **No placeholders:**
  - `isLastAssistantMessage` → DEFINED in Step 19 with full implementation + test
  - Test case success criteria → explicit gate values (not "good enough")
  - `model-downgrade-banner` testid → explicit pre-check in Step 4
  - Mock strategy → split file (no shared-mock coupling)
  - Track B deadline → 14 days with auto-execution
  - Track C rollback window → classified by severity
- [x] **Type consistency:** `ChatMessageItem`, `ChatMessageProps`, `AgentMetadata`, `AgentDefinitionDto`, `MeepleAvatarState` all referenced to files where they live
- [x] **Exit criteria:** each phase has measurable DoD with numeric thresholds where applicable
- [x] **Risk register:** 9 risks with severity + mitigation (up from 4 in v1)
- [x] **Architect review gate:** Track A Step 39 is a hard stop
- [x] **Rollback plan:** Track C has BLOCKER/DEGRADATION/PAPERCUT classification
- [x] **Stakeholder clarity:** Track B requires user story validation before path execution

## Changes from v1

| Area | v1 | v2 |
|---|---|---|
| **Track A.0 spike** | Missing | Added — 4-6h verification phase |
| **Mock strategy** | Modified shared mocks (risky) | **Split test file** (safe) |
| **`isLastAssistantMessage`** | Referenced, undefined | **Defined with 7 unit tests** |
| **whitespace test** | className-based (fragile) | **getComputedStyle** (behavior) |
| **Lock enforcement** | Verbal statement | **Concrete protocol**: issue + CODEOWNERS + PR checklist |
| **Track A smoke test** | "5 manual chats" | **Structured 6-point checklist** with E2E |
| **Track A LOC target** | Hardcoded 150 | **Derived from spike** |
| **Track A coverage** | Not specified | **≥ 85% line, ≥ 80% branch** |
| **Track B decision deadline** | None | **14 days → auto Path C** |
| **Track B Path A BC** | Vague "AgentMetrics" | **Administration BC** (evidence-based) |
| **Track B user story** | Missing | **Mandatory before Path A/B** |
| **Track C success gates** | "no issues, 1 week" | **5 explicit gates** with numbers |
| **Track C failure taxonomy** | Binary GO/NO-GO | **3-tier: BLOCKER/DEGRADATION/PAPERCUT** |
| **Track C Lighthouse A/B** | Staging URL (impossible) | **Local builds** |
| **Track C rollback** | Vague | **Per-severity actions** |
| **Agent delegation** | Not addressed | **Matrix: which phases are agent-delegable** |

## Execution Handoff

**Do NOT execute autonomously as a single block.** This plan contains:

1. **Track A.0 (spike)** — can be agent-executed; outputs a report that unblocks A.1-A.3
2. **Track A.1 (characterization tests)** — can be agent-executed; mechanical TDD
3. **Track A.2 (refactor)** — **inline execution with architect review gate**
4. **Track A.3 (PR)** — agent prepares PR, **human approves + merges**
5. **Track B decision** — human only; 14-day deadline
6. **Track B Path C** — can be agent-executed if decision is C
7. **Track C.1 (deploy)** — human only (SSH, secrets)
8. **Track C.2-C.3 (QA + Lighthouse)** — partial: agent runs Lighthouse, human does manual QA
9. **Track C.4-C.5 (rollout)** — human only (production gate)

**Recommended execution session structure:**

### Solo engineer path (sequential — ~3-4 weeks wall-clock)

One engineer working ~20 focus hours/week:

- **Session 1 (1-2h):** Phase A.0 spike → determine option α/β/γ
- **Session 2 (4-6h):** Phase A.1 characterization tests
- **Session 3 (2h):** Track B decision workshop (with product/tech lead)
- **Session 4 (6-10h):** Phase A.2 refactor + A.3 PR prep
- **Session 5:** Architect review + merge (async, 1-3 days)
- **Session 6 (6-8h):** Track C.1-C.3 (staging deploy + QA + Lighthouse)
- **Session 7 (async, 24h+7d):** Track C.4-C.5 (observation + rollout)

All sessions are **sequential** in this path because one person can only do one thing at a time.

### Team path (parallel — ~1-2 weeks wall-clock)

Two or three engineers working in parallel. Track independence allows overlapping:

**Week 1:**

| Day | Engineer 1 (Track A) | Engineer 2 (Track C) | Engineer 3 (Track B) |
|---|---|---|---|
| Mon | Phase A.0 spike (1-2h) + start A.1 (3-4h) | Track C prereq (UX owner) + C.1 staging deploy | Track B: user story workshop with product |
| Tue | Finish A.1 characterization tests | C.2 manual QA + C.3 Lighthouse | Track B: finalize decision (Path A/B/C) |
| Wed | Start A.2 refactor (requires A.0 output) | C.4 24h observation (passive — monitor Sentry) | Execute chosen path (most likely C: delete AgentStatsDisplay) |
| Thu | A.2 refactor continues | C.4 observation (continues) | Path C PR → merge |
| Fri | A.3 PR prep + architect review request | C.5 rollout decision (if obs passed) | — |

**Week 2:**

- Architect reviews Track A PR (async 1-3 days)
- Track A merges + smoke test
- Track C flag removal (after 7d in production)

**Dependencies that force sequencing within a track:**
- A.0 must finish before A.2 refactor (A.1 can run in parallel with A.0)
- C.4 24h observation is a clock-time gate (cannot parallelize)
- C.5 7-day production observation is a clock-time gate

**Total wall-clock:**
- **Solo engineer path:** ~3-4 weeks (bottleneck: engineer bandwidth)
- **Team path:** ~1-2 weeks (bottleneck: C.4 24h + C.5 7d clock gates)
- **Minimum impossible:** < 8 days (due to 7d production observation gate)
