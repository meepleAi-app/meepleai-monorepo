# Chat Shared Primitives — Phase 0 (Preparation for chat-unified → chat/panel unification)

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract the reusable primitives (types, hooks, pure functions) currently locked inside `components/chat-unified/*` into a new `components/chat/shared/*` module, with zero behavioral change, so that both `chat-unified/ChatThreadView` and `chat/panel/*` can consume them. This is the **foundation** for the eventual `chat-unified-full-page-refactor` (Phase 4b of V2 consolidation). Phase 0 is mergeable on its own and does NOT touch routing, UI composition, or delete any file.

**Non-Goals (explicitly out of scope):**
- Do NOT rewrite `/chat/[threadId]/page.tsx`
- Do NOT rewrite `AgentCharacterSheet` / `AgentExtraMeepleCard`
- Do NOT delete any file from `chat-unified/`
- Do NOT introduce new UI features
- Do NOT change the SSE streaming contract
- Do NOT change `useAgentChatStream` / `useVoiceInput` / `useVoiceOutput` signatures (only re-export for discoverability if needed)
- Do NOT unify `Citation` across `ui/meeple/` / `ui/data-display/` — deferred to Phase 0.5

**Architecture:** Strangler Fig Fase 0. Pure extraction with characterization tests guarding invariance. Two view layers (`chat-unified/ChatThreadView` monolithic + `chat/panel/*` slide-over) will BOTH import the shared primitives by the end of this phase, but neither is visually altered.

**Tech Stack:** React 19, TypeScript strict, Vitest + React Testing Library, Tailwind 4, TanStack Query (indirect via existing hooks), Playwright (smoke E2E).

**Commit discipline:** Every task produces a commit that is **independently revertible**. If Task N fails AC-4 (bundle), `git revert <N>` restores a working state without rolling back Task 1..N-1. No cross-task refactors.

---

## Codebase Baseline (verified 2026-04-24)

| Area | Current state | Size |
|------|---------------|------|
| `components/chat-unified/ChatThreadView.tsx` | Monolithic, 918 LOC, 15+ `useState`, 10+ `useCallback`, 4+ `useEffect` | 918 LOC |
| `components/chat-unified/` | 41 files | ~3000+ LOC |
| `components/chat/panel/` | 9 files | 1238 LOC |
| `components/chat/shared/` | **does not exist** | 0 LOC |
| Duplicate citation types | 10 `interface Citation*` across `chat-unified`, `chat/panel`, `ui/meeple`, `ui/data-display`, wizard | 10 definitions |
| `ChatMessageItem` type | `chat-unified/ChatMessageList.tsx:37` (exported, reused by `ChatThreadView`) | 1 canonical ref |
| Hooks | `useAgentChatStream`, `useVoiceInput`, `useVoiceOutput` already in `apps/web/src/hooks/` (already shared) | 3 hooks |
| Existing tests | 10 test files in `chat-unified/__tests__/`, 8 in `chat/panel/__tests__/` | baseline coverage |

**Key insight:** The data-layer hooks (`useAgentChatStream` et al.) are already shared. What is NOT shared is:
- Domain types (`ChatMessageItem`, `CitationData`, `DebugStep`, etc.)
- View-layer hooks that wire the stream into message-list state
- Pure transformation helpers (message coalescing, citation parsing, welcome-message builders)

**Reference files to study before starting:**
- `apps/web/src/components/chat-unified/ChatThreadView.tsx` — source of extractions
- `apps/web/src/components/chat-unified/ChatMessageList.tsx` — canonical `ChatMessageItem`
- `apps/web/src/components/chat-unified/CitationSheet.tsx` — canonical `CitationData`
- `apps/web/src/components/chat-unified/DebugStepCard.tsx` — `DebugStep` shape
- `apps/web/src/hooks/useAgentChatStream.ts` — existing stream hook (do NOT modify)
- `apps/web/src/components/chat/panel/ChatMessageBubble.tsx` — current panel-side type (to be migrated to shared)

---

## Success Criteria (Acceptance)

- [ ] **AC-1 — Zero behavioral change:** All existing tests in `chat-unified/__tests__/` and `chat/panel/__tests__/` continue to pass with ZERO modifications to their assertions.
- [ ] **AC-2 — No duplicate domain types within chat scope:** `grep -rn "^\\(export \\)\\?interface Citation\\b" apps/web/src/components/chat-unified apps/web/src/components/chat/panel` returns zero domain-type definitions (component *Props* types excluded). Enforced long-term by ESLint rule (Task 6).
- [ ] **AC-3 — Characterization coverage:** Each extracted pure function and hook has ≥1 unit test pinning current behavior. Critical hook invariants (SSE abort, voice-flag tracking, error propagation) have explicit test cases.
- [ ] **AC-4 — Bundle neutrality (absolute budget):** `/chat/[threadId]` route **First Load JS** ≤ baseline + **3 KB gzipped**. Measured via `pnpm --dir apps/web build` and parsing the `.next/build-manifest.json` + route chunk output. Both baseline and post-change numbers recorded in the PR body.
- [ ] **AC-5 — Typecheck + lint clean:** `pnpm --dir apps/web typecheck && pnpm --dir apps/web lint` pass. No new ESLint warnings on touched files.
- [ ] **AC-6 — Documentation:** `docs/frontend/chat-shared-primitives.md` created listing each extracted primitive with its signature and consumer list.
- [ ] **AC-7 — E2E smoke:** One Playwright test (new or extended) exercises `/chat/[threadId]`: send a message → receive streamed response → at least one citation rendered. MUST pass against dev server with mocked agent endpoint.
- [ ] **AC-8 — Import boundary enforced:** New ESLint rule `no-restricted-imports` prevents `components/chat/shared/**` from importing `components/chat-unified/**` or `components/chat/panel/**`. CI fails if violated.

---

## Task 1: Scaffold module + extract canonical domain types (merged)

**Rationale:** Empty scaffolding as its own commit is pure churn. We bundle the module skeleton with the first real content (types). A failed extraction can still be reverted atomically.

**Files:**
- Create: `apps/web/src/components/chat/shared/index.ts`
- Create: `apps/web/src/components/chat/shared/types.ts`
- Create: `apps/web/src/components/chat/shared/__tests__/types.test-d.ts`

### Target types (canonical definitions to migrate)

| New shared name | Source location (current) | Notes |
|-----------------|---------------------------|-------|
| `ChatMessageItem` | `chat-unified/ChatMessageList.tsx:37` | **Canonical.** Re-export from old path for compat. |
| `Citation` (minimal) | intersection of `chat/panel` + `chat-unified` usage | **Only fields used by BOTH sides.** See "Split strategy" below. |
| `CitationWithPdf extends Citation` | from `chat-unified/CitationSheet:CitationData` | PDF-modal-specific fields (pdfId, pageNumber, etc.). |
| `CitationData` | — | **Deprecated alias** → `CitationWithPdf` (keeps `chat-unified/CitationSheet` compat). |
| `DebugStep` | inferred from `chat-unified/DebugStepCard.tsx` props | Read file first. |
| `DebugSummary` | inferred from `chat-unified/DebugSummaryBar.tsx` props | Read file first. |
| `ChatMessageRole` | `chat/panel/ChatMessageBubble.tsx:9` | Already exported there. |
| `StreamStateForMessages` | `chat-unified/ChatMessageList.tsx:49` | Move as-is (`Pick<AgentChatStreamState, ...>`). |

### Citation split strategy (fix for "superset forces widening on chat/panel")

**Do NOT** define a single superset `Citation`. Instead:

```typescript
// Minimal: fields genuinely shared by both view layers
export interface Citation {
  id: string;
  snippet: string;
  score?: number;
  chunkId?: string;
}

// Extended: PDF-modal-specific (only chat-unified/CitationSheet uses these today)
export interface CitationWithPdf extends Citation {
  pdfId: string;
  pageNumber: number;
  pdfUrl?: string;
}

/** @deprecated Use `CitationWithPdf` directly. */
export type CitationData = CitationWithPdf;
```

Rationale: `chat/panel/ChatCitationCard` today renders snippet + score only; forcing it to accept `pdfId`/`pageNumber` would be type widening without behavioral justification. When Phase 1 ports the PDF modal into `chat/panel`, `ChatCitationCard` will graduate its prop type to `CitationWithPdf` — but that's a Phase 1 change, not Phase 0.

- [ ] **Step 1: Read current type shapes**

Read these files in full:
- `components/chat-unified/ChatMessageList.tsx` (entire `ChatMessageItem`)
- `components/chat-unified/CitationSheet.tsx` (entire `CitationData`)
- `components/chat-unified/DebugStepCard.tsx` (props)
- `components/chat-unified/DebugSummaryBar.tsx` (props)
- `components/chat/panel/ChatMessageBubble.tsx` (role type + citation prop shape)
- `components/chat/panel/ChatCitationCard.tsx` (citation prop shape — to decide minimal vs extended)

Copy field-by-field. When unsure if a field belongs to minimal or extended, **prefer extended** (less chance of over-widening panel-side consumers).

- [ ] **Step 2: Write type-only test (robust against Vitest type widening)**

`apps/web/src/components/chat/shared/__tests__/types.test-d.ts`:

```typescript
// Type-only assertions. Run via `pnpm tsc --noEmit --project tsconfig.json`.
// Uses @ts-expect-error for negative assertions (Vitest's expectTypeOf can silently pass on widened types).
import type {
  ChatMessageItem,
  Citation,
  CitationWithPdf,
  ChatMessageRole,
  DebugStep,
  DebugSummary,
} from '../types';

// Positive: CitationWithPdf IS-A Citation
const _extendsOk: Citation = {} as CitationWithPdf;

// Negative: plain Citation is NOT assignable to CitationWithPdf
// @ts-expect-error Citation missing pdfId/pageNumber
const _shouldFail: CitationWithPdf = {} as Citation;

// Pin: role is the string union we expect
const _role1: ChatMessageRole = 'user';
const _role2: ChatMessageRole = 'assistant';
// @ts-expect-error 'system' is not a valid role
const _role3: ChatMessageRole = 'system';

// Pin: ChatMessageItem requires id + role + content
const _msg: ChatMessageItem = {
  id: 'x',
  role: 'user',
  content: '',
};

// Dead-code references to prevent unused-import errors under strict lint
void (_extendsOk, _shouldFail, _role1, _role2, _role3, _msg);
export {};
```

**Critical:** Add a CI step or npm script that runs `tsc --noEmit` on this file specifically — `@ts-expect-error` only triggers during a type-check, not during Vitest runtime.

Add to `apps/web/package.json` scripts:
```json
"typecheck:types-test": "tsc --noEmit --project tsconfig.test-types.json"
```

Create `apps/web/tsconfig.test-types.json`:
```json
{
  "extends": "./tsconfig.json",
  "include": ["src/components/chat/shared/__tests__/**/*.test-d.ts"],
  "compilerOptions": { "noEmit": true }
}
```

- [ ] **Step 3: Populate `types.ts` + barrel**

```typescript
// apps/web/src/components/chat/shared/types.ts
export type ChatMessageRole = 'user' | 'assistant';

export interface Citation { /* minimal — filled from Step 1 */ }
export interface CitationWithPdf extends Citation { /* PDF fields */ }
/** @deprecated Use `CitationWithPdf` instead. */
export type CitationData = CitationWithPdf;

export interface DebugStep { /* from DebugStepCard props */ }
export interface DebugSummary { /* from DebugSummaryBar props */ }

export interface ChatMessageItem {
  id: string;
  role: ChatMessageRole;
  content: string;
  citations?: CitationWithPdf[]; // chat-unified today stores full Citation; minimal subset is sufficient for panel consumers
  // ... complete from ChatMessageList.ChatMessageItem
}

export type StreamStateForMessages = /* Pick<...> as in ChatMessageList:49 */;
```

```typescript
// apps/web/src/components/chat/shared/index.ts
export * from './types';
```

- [ ] **Step 4: Re-export from legacy paths for compat**

In `chat-unified/ChatMessageList.tsx`:
```typescript
export type { ChatMessageItem, StreamStateForMessages } from '@/components/chat/shared/types';
```

In `chat-unified/CitationSheet.tsx`:
```typescript
import type { CitationWithPdf } from '@/components/chat/shared/types';
export type CitationData = CitationWithPdf; // deprecated alias preserved
```

In `chat/panel/ChatMessageBubble.tsx`:
```typescript
export type { ChatMessageRole } from '@/components/chat/shared/types';
```

- [ ] **Step 5: Run full verification**

```bash
pnpm --dir apps/web typecheck
pnpm --dir apps/web typecheck:types-test    # <-- new script from Step 2
pnpm --dir apps/web test chat-unified
pnpm --dir apps/web test chat/panel
```

All must pass. Expected: AC-1 holds, AC-5 holds.

- [ ] **Step 6: Audit no duplicate definitions within chat scope**

```bash
grep -rnE "^(export )?interface Citation\b" apps/web/src/components/chat-unified apps/web/src/components/chat/panel
```

Expected: only *Props* types (`CitationExpanderProps`, etc.). No `interface Citation` / `interface CitationData` / `interface CitationItem`.

- [ ] **Step 7: Commit (atomic, revertible)**

Message: `refactor(chat): scaffold chat/shared module + extract canonical domain types (Phase 0, Task 1)`

---

## Task 2: Extract pure helper functions

**Files:**
- Create: `apps/web/src/components/chat/shared/messages.ts`
- Create: `apps/web/src/components/chat/shared/welcome.ts`
- Create: `apps/web/src/components/chat/shared/__tests__/messages.test.ts`
- Create: `apps/web/src/components/chat/shared/__tests__/welcome.test.ts`

### Target helpers

Identify by scanning `ChatThreadView.tsx`:
- `allCitations` memo (line ~173) → `collectCitations(messages: ChatMessageItem[]): CitationWithPdf[]`
- `suggestedQuestions` memo (line ~176) → `getSuggestedQuestions(thread, agent, messages): string[]`
- `scrollToBottom` (line ~182) — **leave in view** (DOM-bound, not pure)
- `buildWelcomeMessage` / `getWelcomeFollowUpQuestions` — already in `@/config/agent-welcome`; verify and add targeted re-export only if cross-module reach is actually needed (don't speculatively barrel).

- [ ] **Step 1: TDD — `collectCitations`**

```typescript
// messages.test.ts
import { describe, it, expect } from 'vitest';
import { collectCitations } from '../messages';
import type { ChatMessageItem, CitationWithPdf } from '../types';

describe('collectCitations', () => {
  it('returns empty array for empty messages', () => {
    expect(collectCitations([])).toEqual([]);
  });

  it('flattens citations from all messages preserving order', () => {
    const c1 = { id: '1', pdfId: 'a', pageNumber: 1, snippet: '' } as CitationWithPdf;
    const c2 = { id: '2', pdfId: 'a', pageNumber: 2, snippet: '' } as CitationWithPdf;
    const messages: ChatMessageItem[] = [
      { id: 'm1', role: 'user', content: 'q', citations: [] },
      { id: 'm2', role: 'assistant', content: 'a1', citations: [c1] },
      { id: 'm3', role: 'assistant', content: 'a2', citations: [c2] },
    ];
    expect(collectCitations(messages)).toEqual([c1, c2]);
  });

  it('handles messages with undefined citations', () => {
    const messages: ChatMessageItem[] = [
      { id: 'm1', role: 'assistant', content: 'x' },
    ];
    expect(collectCitations(messages)).toEqual([]);
  });
});
```

Run: FAIL. Implement:

```typescript
// apps/web/src/components/chat/shared/messages.ts
import type { ChatMessageItem, CitationWithPdf } from './types';

export function collectCitations(messages: ChatMessageItem[]): CitationWithPdf[] {
  return messages.flatMap(m => m.citations ?? []);
}
```

Run test: PASS.

- [ ] **Step 2: Replace inline usage in `ChatThreadView.tsx`**

```typescript
// Before:
const allCitations = useMemo(() => messages.flatMap(m => m.citations ?? []), [messages]);
// After:
const allCitations = useMemo(() => collectCitations(messages), [messages]);
```

Run `pnpm test chat-unified/ChatThreadView`. Expect PASS unchanged.

- [ ] **Step 3: TDD — `getSuggestedQuestions`**

Read `suggestedQuestions` memo in `ChatThreadView.tsx` line ~176 to identify actual inputs. Write ≥3 tests covering:
- First-turn (no prior assistant messages)
- Mid-conversation (messages.length > threshold)
- Agent-type switching (different suggestion set per agent)

Implement, replace inline, verify.

- [ ] **Step 4: Re-export from barrel**

```typescript
// apps/web/src/components/chat/shared/index.ts
export * from './types';
export * from './messages';
export * from './welcome';
```

- [ ] **Step 5: Full verification**

```bash
pnpm --dir apps/web test
pnpm --dir apps/web typecheck
```

- [ ] **Step 6: Commit**

Message: `refactor(chat): extract pure message helpers to chat/shared (Phase 0, Task 2)`

---

## Task 3: Extract view-layer hooks (state composition around streaming)

**Files:**
- Create: `apps/web/src/components/chat/shared/useThreadMessages.ts`
- Create: `apps/web/src/components/chat/shared/useChatScroll.ts`
- Create: `apps/web/src/components/chat/shared/__tests__/useThreadMessages.test.tsx`
- Create: `apps/web/src/components/chat/shared/__tests__/useChatScroll.test.tsx`

### `useThreadMessages` — encapsulated, no escape-hatch setter

**Fix for previous "leaky setMessages":** we do NOT expose `setMessages`. Instead we expose explicit, contract-bound methods.

**Signature:**

```typescript
export interface UseThreadMessagesOptions {
  threadId: string;
  agentType: AgentType;
  gameContext?: ProxyGameContext;
  /** Initial messages from server-side load. Locked in on first render. */
  initialMessages?: ChatMessageItem[];
}

export interface UseThreadMessagesResult {
  /** Current message list (read-only view). */
  messages: ReadonlyArray<ChatMessageItem>;
  isSending: boolean;
  error: string | null;
  streamState: AgentChatStreamState;

  /** Send a new message. Aborts any in-flight stream. */
  sendMessage: (content: string, opts?: { isVoice?: boolean }) => Promise<void>;

  /** Abort current stream without sending a new one. */
  abortCurrent: () => void;

  /**
   * Replace the entire message list. Use for server-side hydration on thread switch.
   * Aborts any in-flight stream first.
   */
  replaceMessages: (messages: ChatMessageItem[]) => void;

  /** Was the most recent user message sent via voice? Read-only for TTS decisions. */
  lastMessageWasVoice: boolean;
}

export function useThreadMessages(opts: UseThreadMessagesOptions): UseThreadMessagesResult;
```

**Critical invariants to preserve (characterization tests MUST pin these):**
- `qaAbortRef`: sending during active stream aborts the previous stream before starting new one
- `lastMessageWasVoiceRef` → exposed as `lastMessageWasVoice` read-only return value
- `handleSendRef` bridge: internal; external voice handlers call `sendMessage` directly with `{ isVoice: true }`
- Optimistic user-message append BEFORE stream starts
- On stream error, `error` is set AND `isSending` returns to false AND the optimistic user message is NOT removed

- [ ] **Step 1: Write characterization tests BEFORE extracting**

```typescript
// useThreadMessages.test.tsx
import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/hooks/useAgentChatStream', () => ({
  useAgentChatStream: vi.fn(),
}));

describe('useThreadMessages — invariants', () => {
  beforeEach(() => vi.clearAllMocks());

  it('appends user message optimistically before stream resolves', async () => {
    // arrange stream mock to hang
    // act: sendMessage("hello")
    // assert: messages[last] === { role: 'user', content: 'hello' } synchronously
  });

  it('aborts previous stream when sendMessage called during active stream', async () => {
    const abortSpy = vi.fn();
    // arrange: stream returns a controller with abortSpy
    // act: sendMessage twice rapidly
    // assert: abortSpy called once before second send
  });

  it('sets lastMessageWasVoice=true when isVoice option passed', async () => {
    // act: sendMessage("hi", { isVoice: true })
    // assert: result.current.lastMessageWasVoice === true
  });

  it('sets lastMessageWasVoice=false for text-origin messages', async () => {
    // act: sendMessage("hi")
    // assert: result.current.lastMessageWasVoice === false
  });

  it('surfaces stream error without clearing optimistic user message', async () => {
    // arrange: stream rejects with "boom"
    // act: sendMessage("hi")
    // assert: error === "boom", isSending === false, messages still contains "hi"
  });

  it('replaceMessages aborts active stream and swaps list', async () => {
    // arrange: start a stream
    // act: replaceMessages([...])
    // assert: abortSpy called, messages === new list, isSending === false
  });
});
```

Run: FAIL (hook doesn't exist).

- [ ] **Step 2: Extract — copy-paste first, refactor never**

Strategy: open `ChatThreadView.tsx`, identify the state block (useState for messages/isSending/error, useRef for qaAbortRef/lastMessageWasVoiceRef/handleSendRef, handleSendMessage body ~line 319). Copy verbatim into `useThreadMessages.ts`. Expose via the return shape above. Replace the corresponding region in `ChatThreadView` with `const threadMessages = useThreadMessages({ ... })` and destructure.

**Do NOT refactor during the move.** Any improvement (removal of `handleSendRef` bridge, consolidation of refs) is a FOLLOW-UP plan, not Phase 0.

- [ ] **Step 3: Verify ChatThreadView tests unchanged**

```bash
pnpm --dir apps/web test chat-unified/ChatThreadView
```

Tests must pass WITHOUT modification. If a test breaks, the extraction changed behavior → revert and re-extract.

- [ ] **Step 4: Repeat for `useChatScroll`**

Extract `messagesEndRef` + `scrollToBottom` + auto-scroll `useEffect` (line ~186-197).

Signature:
```typescript
export function useChatScroll<T extends HTMLElement = HTMLDivElement>(
  messages: ReadonlyArray<ChatMessageItem>
): { anchorRef: React.RefObject<T>; scrollToBottom: () => void };
```

Test via `renderHook` mocking `HTMLElement.prototype.scrollIntoView`.

- [ ] **Step 5: Full regression**

```bash
pnpm --dir apps/web test
pnpm --dir apps/web typecheck
```

- [ ] **Step 6: Commit**

Message: `refactor(chat): extract useThreadMessages + useChatScroll to chat/shared (Phase 0, Task 3)`

---

## Task 4: Consumer migration + bundle measurement (absolute budget)

**Files:**
- Modify: `apps/web/src/components/chat/panel/ChatMainArea.tsx`
- Modify: `apps/web/src/components/chat/panel/ChatMessageBubble.tsx` (verify)
- Modify: `apps/web/src/components/chat/panel/ChatCitationCard.tsx` (use minimal `Citation`, not `CitationWithPdf`)

- [ ] **Step 1: Baseline bundle size**

```bash
cd apps/web && pnpm build > /tmp/build-baseline.log 2>&1
# Extract First Load JS for /chat/[threadId] route from Next.js build output.
# Also record from .next/build-manifest.json for deterministic parsing.
node -e "const m=require('./.next/build-manifest.json'); console.log(JSON.stringify(m.pages['/chat/[threadId]']||m.pages['/chat/[threadId]/page'], null, 2))" > /tmp/chunks-baseline.json
```

Record: First Load JS (KB, gzipped) for `/chat/[threadId]`. Save numbers in PR body.

- [ ] **Step 2: Migrate panel consumers**

- `ChatMainArea.tsx`: replace any local `Message`-like prop types with `ChatMessageItem` from `@/components/chat/shared`.
- `ChatCitationCard.tsx`: consume minimal `Citation` (NOT `CitationWithPdf`) — the panel today doesn't have PDF modal UI. If strict TS requires `pdfId`, it means panel already uses it → promote to `CitationWithPdf` and document that decision.

Only type imports change. Zero behavior change.

- [ ] **Step 3: Run panel tests**

```bash
pnpm --dir apps/web test chat/panel
```

- [ ] **Step 4: Rebuild + compare (absolute budget ≤ 3 KB gzipped delta)**

```bash
cd apps/web && pnpm build > /tmp/build-after.log 2>&1
node -e "const m=require('./.next/build-manifest.json'); console.log(JSON.stringify(m.pages['/chat/[threadId]']||m.pages['/chat/[threadId]/page'], null, 2))" > /tmp/chunks-after.json
diff /tmp/chunks-baseline.json /tmp/chunks-after.json
```

Extract First Load JS from both builds. Assert `after - baseline ≤ 3 KB gzipped`. If over budget, investigate circular re-exports and barrel tree-shaking; consider direct-file imports instead of `@/components/chat/shared`.

- [ ] **Step 5: Commit**

Message: `refactor(chat): migrate chat/panel consumers to shared types (Phase 0, Task 4)`

---

## Task 5: Enforce import boundary via ESLint + add E2E smoke

**Files:**
- Modify: `apps/web/.eslintrc.*` or `apps/web/eslint.config.*`
- Create or extend: `apps/web/e2e/chat/thread-view-smoke.spec.ts`

- [ ] **Step 1: Add `no-restricted-imports` rule**

Add to ESLint config, scoped to `apps/web/src/components/chat/shared/**`:

```javascript
// eslint.config.mjs (or equivalent)
{
  files: ['apps/web/src/components/chat/shared/**/*.{ts,tsx}'],
  rules: {
    'no-restricted-imports': ['error', {
      patterns: [
        {
          group: ['**/components/chat-unified/**', '**/components/chat/panel/**'],
          message: 'chat/shared/* MUST NOT depend on chat-unified/* or chat/panel/*. Shared primitives are leaf modules.',
        },
      ],
    }],
  },
},
```

- [ ] **Step 2: Verify rule fires**

Temporarily add `import '@/components/chat-unified/ChatMessageList';` to `chat/shared/index.ts`. Run `pnpm --dir apps/web lint`. Expect ERROR. Remove the probe import.

- [ ] **Step 3: Add Playwright smoke test**

Create `apps/web/e2e/chat/thread-view-smoke.spec.ts` (or extend an existing chat E2E file if present):

```typescript
import { test, expect } from '@playwright/test';

test.describe('Chat thread view — smoke', () => {
  test('send message, receive streamed response, render citation', async ({ page, context }) => {
    // Mock agent SSE endpoint with a scripted stream emitting a short response + 1 citation
    await context.route('**/api/v1/agents/*/stream**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'text/event-stream',
        body: [
          'data: {"type":"token","content":"Hello"}\n\n',
          'data: {"type":"citation","id":"c1","snippet":"rule 1","pdfId":"p1","pageNumber":1}\n\n',
          'data: {"type":"done"}\n\n',
        ].join(''),
      });
    });

    await page.goto('/chat/test-thread-id');
    await page.getByRole('textbox', { name: /message|messaggio/i }).fill('Quick rules?');
    await page.getByRole('button', { name: /send|invia/i }).click();

    await expect(page.getByText('Hello')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('rule 1')).toBeVisible();
  });
});
```

Notes:
- Use `context.route()` (NOT `page.route()`) per project convention (see MEMORY.md Playwright patterns).
- Exact selectors/endpoint path to be confirmed against current `/chat/[threadId]` implementation during Task 5 execution.
- If the project uses `PLAYWRIGHT_AUTH_BYPASS=true`, reuse it for this spec.

- [ ] **Step 4: Run E2E**

```bash
pnpm --dir apps/web test:e2e chat/thread-view-smoke
```

Must pass (AC-7).

- [ ] **Step 5: Commit**

Message: `chore(chat): enforce shared module boundary + add E2E smoke (Phase 0, Task 5)`

---

## Task 6: Documentation

**Files:**
- Create: `docs/frontend/chat-shared-primitives.md`

- [ ] **Step 1: Write the doc**

Sections:
1. **Purpose** of `components/chat/shared/`
2. **Inventory table** (name, kind, file, consumers, since-PR)
3. **Citation type hierarchy**: `Citation` (minimal) vs `CitationWithPdf` (PDF modal); when to use which
4. **Rules of engagement**:
   - NEW chat-related domain types → `chat/shared/types.ts`
   - NEW pure helpers → `chat/shared/{feature}.ts`
   - Hooks composing data-layer (`useAgentChatStream`) + view state → `chat/shared/use{Feature}.ts`
   - DO NOT put DOM-bound logic in shared (stays in view layer)
   - DO NOT import from `chat-unified/` or `chat/panel/` inside `chat/shared/` (enforced by ESLint)
5. **Roadmap**: link to future Phase 0.5 (ui/meeple Citation unification) and Phase 1-6 strangler plan.

- [ ] **Step 2: Commit**

Message: `docs(chat): document shared primitives module (Phase 0, Task 6)`

---

## Task 7: PR + review + merge

- [ ] **Step 1: Verify all acceptance criteria**

- [ ] AC-1: `pnpm --dir apps/web test` all green, no assertion modifications
- [ ] AC-2: `grep -rnE "^(export )?interface Citation\\b" apps/web/src/components/chat-unified apps/web/src/components/chat/panel` returns zero domain-type duplicates
- [ ] AC-3: Characterization tests exist for `collectCitations`, `getSuggestedQuestions`, `useThreadMessages` (all 6 invariants), `useChatScroll`
- [ ] AC-4: `/chat/[threadId]` First Load JS delta ≤ 3 KB gzipped (numbers in PR body)
- [ ] AC-5: `pnpm --dir apps/web typecheck && pnpm --dir apps/web typecheck:types-test && pnpm --dir apps/web lint` clean
- [ ] AC-6: `docs/frontend/chat-shared-primitives.md` present
- [ ] AC-7: E2E smoke test passes locally
- [ ] AC-8: ESLint rule blocks imports from `chat-unified/` / `chat/panel/` inside `chat/shared/`

- [ ] **Step 2: Detect parent branch**

```bash
git config branch.$(git branch --show-current).parent || echo "main-dev (default)"
```

Per CLAUDE.md: feature branches merge to parent, not main. Confirm `main-dev` with user if ambiguous.

- [ ] **Step 3: Push + open PR**

```bash
git push -u origin feature/chat-shared-primitives-phase-0
gh pr create --base main-dev --title "refactor(chat): extract shared primitives (Phase 0)" --body "$(cat <<'EOF'
## Summary
Phase 0 of chat-unified → chat/panel unification (Strangler Fig). Extracts canonical domain types, pure helpers, and view-layer hooks into `components/chat/shared/`. Zero behavioral change. No file deletions. No UI/routing changes.

## Non-Goals
- Rewriting /chat/[threadId] or AgentCharacterSheet (future phases)
- Deleting chat-unified/* (future phases)
- Unifying Citation across ui/meeple/* (Phase 0.5)

## Acceptance Criteria
- [x] AC-1: existing tests unmodified, all green
- [x] AC-2: no duplicate Citation domain types in chat scope
- [x] AC-3: characterization tests for extracted pure fns + hooks (6 invariants on useThreadMessages)
- [x] AC-4: /chat/[threadId] First Load JS delta: baseline=XXX KB → after=YYY KB (Δ = ZZZ KB, ≤ 3 KB budget)
- [x] AC-5: typecheck + lint clean (incl. new types-test-d tsc project)
- [x] AC-6: docs/frontend/chat-shared-primitives.md
- [x] AC-7: Playwright smoke: send → stream → citation
- [x] AC-8: ESLint no-restricted-imports enforces module boundary

## Rollback
`git revert <PR-sha>`. No schema/data changes. Each task is an independent commit.

## Follow-ups (separate plans)
- Phase 0.5: unify Citation across ui/meeple, ui/data-display (10 duplicates)
- Phase 1: port PDF modal into chat/shared/citation-ui/
- Phase 2-6: progressive chat-unified strangler

🤖 Generated with [Claude Code](https://claude.ai/code)
EOF
)"
```

- [ ] **Step 4: Run `/code-review:code-review <PR-URL>` skill**

- [ ] **Step 5: Merge to parent branch (NOT main)**

```bash
gh pr merge <N> --squash --delete-branch
```

- [ ] **Step 6: Update MEMORY.md**

Under `## Executed Plans`:
```
- `chat-shared-primitives-phase-0` ✅ PR#<N> (extract ChatMessageItem, Citation/CitationWithPdf split, DebugStep, useThreadMessages w/ 6 pinned invariants, useChatScroll to components/chat/shared/; ESLint boundary rule; Playwright smoke; zero behavioral change; prep for chat-unified-full-page-refactor Phase 1-6)
```

Under `## Pending Plans`: annotate `chat-unified-full-page-refactor`: "Prereq ✅ — Phase 0 merged in PR#<N>. Next: Phase 0.5 (ui/meeple Citation unification) OR Phase 1 (port PDF modal to chat/shared/citation-ui/)."

---

## Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Circular re-export (`chat-unified` → `chat/shared` → `chat-unified`) | Medium | Build fails | **Enforced** by ESLint rule (Task 5) — CI blocks regression. |
| `CitationWithPdf` over-widens panel consumers | Low | Typecheck fails | Split into minimal `Citation` + extended `CitationWithPdf`; panel uses minimal. |
| Extracted hook accidentally changes SSE semantics | High | Runtime chat breakage | Copy-paste extraction only (Task 3 Step 2, "refactor never"); 6 characterization tests pin abort/voice/error/hydration paths. |
| Bundle bloat from extra barrel hops | Medium | AC-4 violation (>3 KB) | Absolute KB budget, not %. Measured via `.next/build-manifest.json` for deterministic parsing. If violated: replace `@/components/chat/shared` imports with direct file paths (`@/components/chat/shared/types`). |
| `types.test-d.ts` silently passes due to Vitest not running type checks | Medium | Type regressions undetected | Dedicated `typecheck:types-test` script using `tsc --noEmit` via separate `tsconfig.test-types.json`. `@ts-expect-error` annotations provide negative assertions. |
| 11th `Citation` definition added in `ui/meeple` post-merge | High | Silent drift, AC-2 regression | AC-2 grep scope limited to chat components; long-term unification deferred to Phase 0.5 with its own plan. |
| `replaceMessages` misused by Phase 1+ consumer as a generic setter | Low | Invariant violation | Documented contract: "aborts active stream, resets error+isSending". Tested. TS type `ReadonlyArray` on read side prevents silent mutation. |
| E2E smoke brittle on agent endpoint path change | Medium | AC-7 false red | Mock path confirmed against current route at Task 5 execution; if route changes, update mock in same PR. |

---

## Out-of-scope follow-ups (each becomes its own plan)

- **Phase 0.5**: unify `Citation` across `ui/meeple/chat-message.tsx`, `ui/data-display/citation-link.tsx`, `chat-unified/CitationBadge.tsx` (10 definitions). Extend ESLint rule scope to enforce.
- **Phase 1**: port PDF modal + citation sheet into `chat/shared/citation-ui/` so both full-page and slide-over render from the same primitives. Promote `ChatCitationCard` from `Citation` to `CitationWithPdf` when PDF UI is available panel-side.
- **Phase 2-6**: progressive strangler — view-layer composition, AgentCharacterSheet rewrite, AgentExtraMeepleCard, then delete `chat-unified/`.

---

**Owner:** —
**Parent branch:** `main-dev` (confirm)
**Feature flag:** none required (pure refactor)
**Rollback:** per-task `git revert <commit-sha>` OR full `git revert <PR-sha>` — no data migrations, no schema changes.

---

## Changelog of this plan

- **2026-04-24 v2**: fixes from iteration review
  - AC-4: ±2% → absolute 3 KB gzipped budget with deterministic `.next/build-manifest.json` parsing
  - Task 2 Citation: split into minimal `Citation` + `CitationWithPdf` (avoids widening panel consumers)
  - Task 3 useThreadMessages: removed `setMessages` escape hatch; added explicit `replaceMessages` with documented contract; `lastMessageWasVoice` returned as read-only value
  - Task 2 types test: switched to `@ts-expect-error`-based assertions + dedicated `tsc --noEmit` project (Vitest `expectTypeOf` can silently pass on widened types)
  - Task 5 NEW: ESLint `no-restricted-imports` boundary rule + Playwright E2E smoke (AC-7, AC-8)
  - Task 1+2 (old): merged into single Task 1 (scaffolding-only commit was pure churn)
  - Commit discipline: each task independently revertible; explicit intermediate safe points
- **2026-04-24 v1**: initial plan
