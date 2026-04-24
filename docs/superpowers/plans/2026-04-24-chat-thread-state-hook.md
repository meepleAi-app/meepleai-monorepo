# Chat Thread State Hook — dedicated refactor for `useThreadMessages`

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract the thread-message state machine currently embedded in `components/chat-unified/ChatThreadView.tsx` into a single, encapsulated hook `useThreadMessages` that both the monolithic view and the new `chat/panel/*` slide-over can share. Unlike Phase 0 primitives, this is NOT a trivial copy-paste — it is a **targeted refactor** that closes escape hatches while preserving every observable behavior of the current SSE + QA-stream + REST-fallback + voice + continuation flows.

**Why this is a dedicated plan (and was deferred from Phase 0):** The Phase 0 rule was "copy-paste first, refactor never". Faithfully extracting the current code would expose a `setMessages` dispatcher to the caller (20+ call sites across 6 flows), defeating the encapsulation goal — the hook would be a facade with a leaking back door. Doing a proper encapsulation inside Phase 0 would have broken the "zero behavioral change" commit discipline that kept Phase 0 reviewable in one sitting. We therefore separated concerns: Phase 0 landed the cheap, safe primitives (`types`, `messages.ts`, `useChatScroll`); this plan handles the hard, testable, risky one in isolation.

**Non-Goals (explicitly out of scope):**
- Do NOT merge or replace `chat-unified/ChatThreadView` — only swap its `useState<ChatMessageItem[]>` for the hook
- Do NOT rewrite `useAgentChatStream` or the `qaStream` generator
- Do NOT change the SSE event-type wire format or continuation-token semantics
- Do NOT touch `/chat/[threadId]/page.tsx` routing
- Do NOT alter TTS / voice-input hook signatures
- Do NOT refactor error UX copy or toast/alert behavior
- Do NOT introduce a reducer, state-machine library, or TanStack Query mutation for the stream — current imperative flow is preserved
- Do NOT migrate panel-side consumers in the same PR — that is the follow-up

**Architecture:** Facade hook over `useReducer` (internal) exposing an **encapsulated action API**. Characterization tests pin 6 invariants before the extraction. No direct `setMessages` escape hatch. Stream aborts and voice-flag lifecycle are managed inside the hook.

**Tech Stack:** React 19, TypeScript strict, Vitest + React Testing Library `renderHook`, Playwright (regression smoke only).

**Commit discipline:** Split into characterization-first commits, then extraction, then wiring. Every commit is independently revertible. If the full extraction breaks an SSE invariant the reducer-only commit can land first and the wiring commit can revert without losing the reducer baseline.

---

## Codebase Baseline (verified 2026-04-24)

### `setMessages` call sites in `ChatThreadView.tsx` (15 total, 6 flows)

| # | Line | Flow | Operation | Notes |
|---|------|------|-----------|-------|
| 1 | 82 | init | `useState<ChatMessageItem[]>([])` | Replaced by hook |
| 2 | 158 | `useAgentChatStream.onComplete` | `append(assistant)` | SSE-agent path (currently unused route) |
| 3 | 255 | `useEffect(loadThread)` | `replace([welcomeMessage])` | New empty thread + agent → inject welcome |
| 4 | 257 | `useEffect(loadThread)` | `replace(mappedMessages)` | Hydration from backend |
| 5 | 293 | `handleContinue` per token | `patch(lastAssistant, { content, continuationToken:undefined })` | Coalesces streamed tokens |
| 6 | 329 | `handleSendMessage` | `append(user)` | Optimistic user echo |
| 7 | 343 | `handleSendMessage` QA path | `append(empty assistant)` | Placeholder before stream |
| 8 | 372 | `handleSendMessage` QA/InlineCitation | `patch(assistantMsgId, { inlineCitations })` | |
| 9 | 383 | `handleSendMessage` QA/ContinuationAvailable | `patch(assistantMsgId, { continuationToken })` | |
| 10 | 404 | `handleSendMessage` QA/Citations | `patch(assistantMsgId, { snippets })` | |
| 11 | 421 | `handleSendMessage` QA/Token | `patch(assistantMsgId, { content: accumulated })` | Per-token content update |
| 12 | 454 | `handleSendMessage` QA post-loop | `patch(assistantMsgId, { content, citations, followUpQuestions })` | Final commit |
| 13 | 484 | `handleSendMessage` QA catch | `remove(assistantMsgId)` | On non-abort error |
| 14 | 511 | `handleSendMessage` REST-fallback success | `replace(response.messages)` | Only when no gameId & no agentId |
| 15 | 525 | `handleSendMessage` REST-fallback catch | `remove(userMessage.id)` | On error |

### Current external coupling of the state

Dependencies that cross the state boundary today:
- `qaAbortRef.current` (line 275, 345) — stream abort controller lives as a ref in the component
- `lastMessageWasVoiceRef.current` (line 160, 476) — consumed by `onComplete` / post-stream TTS gate
- `voicePrefs.ttsEnabled` + `speak()` — called from inside the state-mutation callbacks (lines 161, 476–478)
- `setError(...)` — called adjacent to state mutations (lines 443, 482, 523)
- `setIsSending(true/false)` — book-keeping around stream lifecycle (lines 164, 168, 273, 304, 318, 487, 527)
- `api.chat.addMessage` — persistence call interleaved with state mutation (lines 353, 469, 505)

These make the "state" boundary leaky today. The hook will absorb abort-ref + voice-flag + isSending; it will NOT absorb `speak()`, `api.chat.*`, or `setError` (those stay caller-side via typed events).

### Existing tests to consult
- `apps/web/src/components/chat-unified/__tests__/ChatThreadView.test.tsx` — full-component tests (current behavioral contract)
- `apps/web/src/components/chat-unified/__tests__/ChatThreadView.*.test.tsx` — focused slices (agent-switch, continuation, voice)
- `apps/web/src/hooks/__tests__/useAgentChatStream.test.tsx` — SSE hook contract (DO NOT modify)

### Reference files to study before starting
- `apps/web/src/components/chat-unified/ChatThreadView.tsx:75–546` — source of extraction
- `apps/web/src/hooks/useAgentChatStream.ts` — existing SSE hook (untouched)
- `apps/web/src/lib/api/chat.ts` (via `api.chat.*`) — persistence surface
- `apps/web/src/components/chat/shared/types.ts` — `ChatMessageItem`, `StreamStateForMessages` (landed in Phase 0)

---

## Success Criteria (Acceptance)

- [ ] **AC-1 — Encapsulation:** `ChatThreadView.tsx` contains zero direct `setMessages` calls after migration. Verified by `grep -n "setMessages" apps/web/src/components/chat-unified/ChatThreadView.tsx` → empty.
- [ ] **AC-2 — Zero behavioral change:** All existing `chat-unified/__tests__/*` tests pass **without modifying assertions**. If a test must change, that is a bug in this plan — stop and re-scope.
- [ ] **AC-3 — Characterization coverage (6 invariants pinned before extraction):**
  1. Optimistic append: `sendMessage(content)` immediately yields a `{role:'user',content}` entry before any network call resolves.
  2. Stream abort on re-send: calling `sendMessage` while `streamStatus === 'streaming'` aborts the prior controller before starting the new request.
  3. Voice flag propagation: `sendMessage(content, { fromVoice: true })` surfaces `lastMessageWasVoice === true` in the returned state; consumer can observe it to drive `speak()`.
  4. Error preserves user message: a non-abort error during QA stream removes only the assistant placeholder (not the user message), matching today's line 484 behavior.
  5. `replaceMessages` aborts current stream: hydration (thread load, deletion → new thread) must cancel any in-flight stream before replacing.
  6. Continuation flow: `continueStream(token)` appends tokens to the LAST assistant message identified at call time, not a stale closure.
- [ ] **AC-4 — Bundle neutrality:** `/chat/[threadId]` First Load JS ≤ Phase 0 post-merge baseline + **1 KB gzipped**. Recorded in PR body.
- [ ] **AC-5 — Typecheck + lint clean:** `pnpm --dir apps/web typecheck && pnpm --dir apps/web lint` pass.
- [ ] **AC-6 — E2E regression:** The Phase 0 Playwright smoke (send → stream → citation) still passes. No new E2E required unless a Phase 0 gap is uncovered.
- [ ] **AC-7 — Import boundary:** `useThreadMessages` lands in `components/chat/shared/`. ESLint rule from Phase 0 Task 5 continues to enforce no back-imports.
- [ ] **AC-8 — Documentation:** `docs/frontend/chat-shared-primitives.md` (Phase 0 artifact) updated with hook signature + invariants 1–6 from AC-3 + consumer list.

---

## Proposed Hook Signature

```ts
// apps/web/src/components/chat/shared/useThreadMessages.ts

import type { ChatMessageItem } from './types';

export interface SendOptions {
  /** When true, downstream can decide to auto-speak the final answer. */
  fromVoice?: boolean;
}

export interface ThreadSendContext {
  /** Required for QA-stream path (gameId drives RAG). */
  gameId?: string;
  /** Thread id for REST persistence. */
  threadId: string;
  /** Agent id for SSE-agent path (fallback, no gameId). */
  agentId?: string;
  /** Response style passed to qaStream. */
  responseStyle?: 'concise' | 'detailed';
}

export type StreamStatus = 'idle' | 'streaming' | 'error';

export interface UseThreadMessagesResult {
  messages: ReadonlyArray<ChatMessageItem>;
  streamStatus: StreamStatus;
  /** Mirror of the current in-progress assistant content for UI token rendering. */
  currentAnswer: string;
  /** True iff the MOST RECENT sendMessage call used fromVoice:true AND no later send has fired. */
  lastMessageWasVoice: boolean;

  /** Optimistic append + full QA/SSE/REST lifecycle. Safe to call while streaming — aborts prior stream. */
  sendMessage: (content: string, ctx: ThreadSendContext, options?: SendOptions) => Promise<void>;
  /** Append streamed continuation to the last assistant message. Aborts any non-continuation stream first. */
  continueStream: (token: string, ctx: Pick<ThreadSendContext, 'gameId'>) => Promise<void>;
  /** Abort any in-flight stream. Idempotent. */
  abortCurrent: () => void;
  /** Replace the entire message list (thread load, welcome injection, deletion reset). Aborts current stream. */
  replaceMessages: (next: ReadonlyArray<ChatMessageItem>) => void;
}

export function useThreadMessages(): UseThreadMessagesResult;
```

### Error surfacing (typed events, not shared setError)

To avoid re-introducing a leaky `setError` escape hatch, the hook exposes errors via an **optional callback** passed at construction time rather than internal global state:

```ts
export function useThreadMessages(options?: {
  onError?: (err: { kind: 'stream' | 'persist' | 'qa'; message: string }) => void;
  onPersist?: (msg: ChatMessageItem) => Promise<void>; // injected api.chat.addMessage wrapper
  onStreamComplete?: (answer: string) => void; // caller wires speak() here if voice
}): UseThreadMessagesResult;
```

This keeps the hook **pure of I/O side-effects it cannot mock** while letting the caller retain control of TTS, toasts, and persistence. The existing `api.chat.addMessage` call remains in `ChatThreadView` via `onPersist` injection — no API behavior change.

---

## Migration Strategy — one flow at a time (NOT monolithic)

Each sub-flow migrates in its own commit, guarded by tests that already exist. Order is chosen to move simplest flows first so reverts stay small.

| Order | Flow | Complexity | Test anchor |
|-------|------|------------|-------------|
| 1 | `replaceMessages` (hydration + welcome injection, lines 255/257) | low | existing `loadThread` tests |
| 2 | `sendMessage` REST-fallback path (lines 329/511/525) | low | existing fallback tests |
| 3 | `continueStream` (lines 273–307) | medium | existing continuation tests |
| 4 | `sendMessage` QA-stream path (lines 329–489) | **high** | existing QA-stream tests + new invariant tests |
| 5 | `sendMessage` SSE-agent path (lines 149–170) | low (dead code today) | contract-only test |
| 6 | Remove `useState<ChatMessageItem[]>` from component | mechanical | — |

If any step fails AC-2, it is reverted in isolation; the preceding steps remain intact.

---

## Task 1: Characterization tests (PIN current behavior)

**Rationale:** Before moving code, we pin the 6 invariants against the **current** `ChatThreadView`. If any invariant is not already guaranteed, Task 1 surfaces it as a FAIL — we stop, file a regression issue, and re-scope.

**Files:**
- Create: `apps/web/src/components/chat-unified/__tests__/ChatThreadView.invariants.test.tsx`

**Steps:**
- [ ] 1.1 — Test invariant 1 (optimistic append):
  ```
  render <ChatThreadView>, intercept api.chat.addMessage with a 100ms delay,
  fire send, assert user message visible at t=0.
  ```
- [ ] 1.2 — Test invariant 2 (stream abort on re-send): mock `qaStream` as a slow generator, fire two sends back-to-back, assert `AbortController.abort` called on the first controller before the second stream starts.
- [ ] 1.3 — Test invariant 3 (voice flag propagation): drive `onTranscript` path, let stream complete, assert `speak()` called with the final answer; immediately fire a NON-voice send, assert `speak()` NOT called on its completion.
- [ ] 1.4 — Test invariant 4 (error preserves user message): mock `qaStream` to yield Error event, assert assistant placeholder removed AND user message retained.
- [ ] 1.5 — Test invariant 5 (hydration aborts stream): start a send, before it completes trigger a thread-load (re-render with new `threadId`), assert prior controller aborted.
- [ ] 1.6 — Test invariant 6 (continuation targets last assistant at call time): inject two assistant messages, call continueStream, assert only the latter updates.
- [ ] 1.7 — Run `pnpm --dir apps/web test ChatThreadView.invariants`; all 6 must pass against **unmodified code**.

**Verification:**
- [ ] All 6 tests green → proceed to Task 2.
- [ ] Any FAIL → STOP, file issue, re-scope plan.

**Commit:** `test(chat): pin thread-message invariants for useThreadMessages extraction`

---

## Task 2: Scaffold `useThreadMessages` (reducer only, no wiring)

**Rationale:** Land the reducer and pure helpers behind the hook API with zero call sites wired in. A bug here cannot regress production because the hook is not imported yet.

**Files:**
- Create: `apps/web/src/components/chat/shared/useThreadMessages.ts`
- Create: `apps/web/src/components/chat/shared/__tests__/useThreadMessages.test.ts`
- Modify: `apps/web/src/components/chat/shared/index.ts` (barrel export)

**Steps:**
- [ ] 2.1 — Define internal `ThreadMessagesState` and `Action` union (`APPEND`, `PATCH_BY_ID`, `REMOVE_BY_ID`, `REPLACE_ALL`, `SET_STREAM_STATUS`, `SET_CURRENT_ANSWER`, `SET_VOICE_FLAG`).
- [ ] 2.2 — Write `threadMessagesReducer(state, action)` as a pure function. Unit-test it with 12+ action scenarios (empty → append, patch-nonexistent-id no-op, replace-while-streaming flag reset, etc.).
- [ ] 2.3 — Wrap the reducer in the `useThreadMessages()` hook. `sendMessage`, `continueStream`, `abortCurrent`, `replaceMessages` start as stubs that dispatch actions but do NOT yet call `qaStream` / `api.chat.*`.
- [ ] 2.4 — Add `renderHook` tests asserting `replaceMessages` dispatches `REPLACE_ALL` and aborts the controller ref.
- [ ] 2.5 — Export from `components/chat/shared/index.ts`.

**Verification:**
- [ ] `pnpm --dir apps/web test useThreadMessages` green.
- [ ] `pnpm --dir apps/web typecheck` clean.
- [ ] No import of the hook from `chat-unified/*` yet.

**Commit:** `feat(chat/shared): scaffold useThreadMessages reducer + stubbed API`

---

## Task 3: Wire `replaceMessages` (flow 1)

**Rationale:** Start with the simplest flow. Swap only the hydration + welcome-injection `setMessages` calls (lines 255, 257) to use the hook. All other `setMessages` call sites keep working via a temporary `injectedState` bridge.

**Files:**
- Modify: `apps/web/src/components/chat-unified/ChatThreadView.tsx`

**Steps:**
- [ ] 3.1 — Instantiate `useThreadMessages()` at top of the component, read `messages` from the hook, alias `legacyMessages = hook.messages` so other call sites keep pattern-matching on `messages`.
- [ ] 3.2 — Replace lines 255/257 with `replaceMessages([welcomeMessage])` / `replaceMessages(mappedMessages)`.
- [ ] 3.3 — Keep `const [messagesLegacy, setMessages] = useState<ChatMessageItem[]>([])` TEMPORARILY alongside; do NOT read from it yet — this is a scaffolding safety net.
- [ ] 3.4 — Run full `chat-unified/__tests__` suite. All tests must pass.

**Verification:**
- [ ] `pnpm --dir apps/web test chat-unified` green.
- [ ] Manual smoke: load a thread in dev, observe messages render identically.

**Commit:** `refactor(chat): migrate thread hydration to useThreadMessages.replaceMessages`

---

## Task 4: Wire REST-fallback `sendMessage` (flow 2)

**Files:**
- Modify: `apps/web/src/components/chat/shared/useThreadMessages.ts` (implement REST path)
- Modify: `apps/web/src/components/chat-unified/ChatThreadView.tsx`

**Steps:**
- [ ] 4.1 — Implement `sendMessage` REST branch inside the hook: optimistic append, call `options.onPersist(userMsg)`, on success dispatch `REPLACE_ALL`, on error dispatch `REMOVE_BY_ID`.
- [ ] 4.2 — In `ChatThreadView`, route the no-gameId+no-agentId branch (lines 503–528) through `hook.sendMessage`. Inject `onPersist: (msg) => api.chat.addMessage(threadId, msg)` and `onError: (e) => setError(e.message)`.
- [ ] 4.3 — Add hook unit tests covering REST success + REST 500 + REST network abort.

**Verification:**
- [ ] REST-fallback tests green; existing tests untouched.
- [ ] Manual: send message in a threadless/gameless dev setup, verify identical UX.

**Commit:** `refactor(chat): migrate REST-fallback send to useThreadMessages`

---

## Task 5: Wire `continueStream` (flow 3)

**Files:**
- Modify: `apps/web/src/components/chat/shared/useThreadMessages.ts`
- Modify: `apps/web/src/components/chat-unified/ChatThreadView.tsx`

**Steps:**
- [ ] 5.1 — Implement `continueStream` inside the hook: capture `lastAssistant` from current state at call time (not from a stale closure), set stream status to `streaming`, loop `qaStream`, dispatch `PATCH_BY_ID` per token, reset status on finally.
- [ ] 5.2 — Replace `handleContinue` body (lines 270–310) with `hook.continueStream(continuationToken, { gameId: thread.gameId })`.
- [ ] 5.3 — Abort-controller management moves fully inside the hook.

**Verification:**
- [ ] Continuation characterization test (invariant 6) still green — note it must now point at the HOOK'S abort path, not the component's qaAbortRef.
- [ ] Manual: trigger a continuation in dev, verify tokens append to the correct message.

**Commit:** `refactor(chat): migrate continuation flow to useThreadMessages.continueStream`

---

## Task 6: Wire QA-stream `sendMessage` (flow 4 — HIGH RISK)

**Rationale:** This is the largest, most side-effect-heavy flow. The whole of lines 331–490 moves into the hook. Tests from Task 1 are the safety net.

**Files:**
- Modify: `apps/web/src/components/chat/shared/useThreadMessages.ts`
- Modify: `apps/web/src/components/chat-unified/ChatThreadView.tsx`

**Steps:**
- [ ] 6.1 — Implement QA branch inside the hook. Event switch (InlineCitation / ContinuationAvailable / Citations / Token / Complete / Error) translates directly into `PATCH_BY_ID` / `SET_CURRENT_ANSWER` / error propagation via `options.onError`.
- [ ] 6.2 — `onStreamComplete(finalAnswer)` callback fired after successful loop; caller uses it to gate `speak()` via `lastMessageWasVoice`.
- [ ] 6.3 — Remove lines 331–490 from `handleSendMessage` in `ChatThreadView`; that callback becomes ~20 LOC that routes via `hook.sendMessage` + `hook.currentAnswer` consumption.
- [ ] 6.4 — Double-check invariant 2 (abort-on-re-send) — Task 1 test must still pass against new code.

**Verification:**
- [ ] All 6 Task-1 invariants still green against migrated code.
- [ ] Full `chat-unified/__tests__` suite green.
- [ ] Manual: send 3 messages rapid-fire in dev, assert only the last assistant response renders (aborts working).
- [ ] Bundle check: measure `/chat/[threadId]` First Load JS, compare to Phase 0 baseline → ≤ +1 KB gzipped.

**Commit:** `refactor(chat): migrate QA-stream send to useThreadMessages`

---

## Task 7: Wire SSE-agent fallback (flow 5) + cleanup (flow 6)

**Files:**
- Modify: `apps/web/src/components/chat/shared/useThreadMessages.ts`
- Modify: `apps/web/src/components/chat-unified/ChatThreadView.tsx`

**Steps:**
- [ ] 7.1 — Route `useAgentChatStream.onComplete` through `hook` internally: when the hook detects `ctx.agentId && !ctx.gameId`, it delegates to the caller-injected `streamAdapter`. (This keeps `useAgentChatStream` untouched per non-goal.)
- [ ] 7.2 — Delete the now-orphan `useState<ChatMessageItem[]>` + `setMessages` alias from `ChatThreadView`.
- [ ] 7.3 — Delete `qaAbortRef` from the component; all abort handling is now inside the hook.
- [ ] 7.4 — Run `grep -n "setMessages" apps/web/src/components/chat-unified/ChatThreadView.tsx` → must return 0 matches.

**Verification:**
- [ ] AC-1 satisfied (zero `setMessages` in component).
- [ ] All chat-unified tests still green.
- [ ] Typecheck + lint clean.

**Commit:** `refactor(chat): remove direct setMessages escape hatch from ChatThreadView`

---

## Task 8: Documentation + PR

**Files:**
- Modify: `docs/frontend/chat-shared-primitives.md` (Phase 0 artifact)

**Steps:**
- [ ] 8.1 — Add a `useThreadMessages` section: signature, 6 invariants, consumer list (`ChatThreadView` today, `chat/panel/ChatSlideOverPanel` queued for follow-up).
- [ ] 8.2 — Note the intentional `onError` / `onPersist` / `onStreamComplete` injection pattern and the rationale ("hook stays pure of I/O it cannot mock").
- [ ] 8.3 — Cross-link this plan file from the primitives doc.
- [ ] 8.4 — Open PR against `feature/chat-shared-primitives-phase-0` (or its successor branch), record baseline + post-change bundle numbers.

**Commit:** `docs(chat): document useThreadMessages hook and invariants`

---

## Verification gates (end-to-end)

Before merge:
1. All 6 characterization tests from Task 1 pass against final code.
2. Full `chat-unified/__tests__/` suite passes with **no assertion changes**.
3. `pnpm --dir apps/web typecheck && pnpm --dir apps/web lint && pnpm --dir apps/web test` all green.
4. Playwright smoke `/chat/[threadId]` send → stream → citation passes.
5. Bundle delta ≤ +1 KB gzipped vs Phase 0 baseline.
6. `grep -n "setMessages" apps/web/src/components/chat-unified/ChatThreadView.tsx` returns empty.
7. Manual exploratory test: rapid double-send, mid-stream thread switch, voice send, continuation — all behave identically to pre-change.

---

## Relationship to Phase 0 and downstream work

- **Phase 0 (`2026-04-24-chat-shared-primitives-phase-0.md`):** Landed `types`, `messages.ts` helpers, `useChatScroll`. This plan builds on that module without modifying its surface.
- **This plan:** Closes the `setMessages` escape hatch — the last blocker that was preventing `chat/panel/*` from sharing the same state machine.
- **Follow-up (`chat-unified-full-page-refactor`, memory-tracked):** Once `useThreadMessages` is in place, `chat/panel/ChatSlideOverPanel` can migrate to the same hook, unlocking the full rewrite of `/chat/[threadId]/page.tsx` against `chat/panel/*` components. That is a separate plan — do NOT conflate.

---

## Risks and mitigations

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| QA-stream event order changes under the new dispatcher | low | Task 1 invariant 6 + existing QA tests pin order |
| Hook re-renders more than the current `useState` | medium | `useReducer` is cheaper than N scattered setStates in practice; measure with React DevTools profiler before Task 6 → Task 7 transition |
| `onPersist` injection leaks `api` into test harness | low | Default `onPersist = async () => {}`; tests inject a mock; production wires `api.chat.addMessage` |
| Voice-flag lifecycle regression (flag stays `true` across unrelated sends) | medium | Invariant 3 test + reducer clears flag on any non-voice send |
| Bundle growth from reducer + action constants | low | Target +1 KB max; plain union + switch, no libraries |
| Abort-controller lifecycle leak on unmount | medium | Hook's `useEffect` cleanup calls `abortCurrent()`; add test in Task 2 |

---

## Out-of-plan work that MUST NOT happen in this PR

- Migrating `chat/panel/*` consumers to the new hook
- Changing the SSE wire protocol or `qaStream` generator
- Adding new message actions beyond APPEND/PATCH/REMOVE/REPLACE
- Rewriting `handleSendMessage`'s branching logic (gameId vs agentId vs fallback) — preserved verbatim, only its state mutations are rerouted
- Converting `useAgentChatStream` to the new hook pattern
- Deleting any `chat-unified/` file

If any of these becomes necessary to make this plan work, STOP and re-scope. The plan is wrong.
