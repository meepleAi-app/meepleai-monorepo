# Chat Shared Primitives (`components/chat/shared/`)

> **Phase 0 Strangler Fig** — foundational extraction for the eventual `chat-unified → chat/panel` unification. Zero behavioral change.
> **Phase 1** ✅ — `useThreadMessages` owns the message list + `AbortController` lifecycle for a single thread (plan: [`docs/superpowers/plans/2026-04-24-chat-thread-state-hook.md`](../superpowers/plans/2026-04-24-chat-thread-state-hook.md)).
> Both view layers (monolithic `chat-unified/ChatThreadView` and slide-over `chat/panel/*`) consume from this module.

## 1. Purpose

`components/chat/shared/` is the **single source of truth** for chat primitive types and pure derivation logic that the two chat view layers must agree on. Before this module existed, each layer duplicated its own `Citation` interface, its own `ChatMessageRole`, and inlined helpers like `messages.flatMap(m => m.citations ?? [])` inside large `useMemo` blocks. That duplication meant any semantic drift (a field added in one place but not the other) would silently desync the two layers.

**What lives here:**
- Canonical domain types (`ChatMessageItem`, `ChatMessageRole`, `StreamStateForMessages`, …)
- Pure transformation helpers over message collections
- View-layer hooks that compose data-layer hooks (`useAgentChatStream`) with local rendering state

**What does NOT live here:**
- DOM-bound logic (stays in view layer — `chat-unified/*` or `chat/panel/*`)
- Data-layer hooks (remain in `src/hooks/*` — e.g. `useAgentChatStream`)
- UI projections / view-models (e.g. `ChatCitation` in `chat/panel/ChatCitationCard.tsx` is intentionally NOT migrated — see types.ts reconciliation notes)

## 2. Inventory

| Primitive | Kind | File | Consumers | Since |
|-----------|------|------|-----------|-------|
| `Citation` | Type (re-export of `@/types`) | `types.ts` | `chat-unified/ChatMessageList`, `chat-unified/CitationSheet`, `chat/panel/*` | 9bc5c9b31 |
| `CitationData` | Type (deprecated alias of `Citation`) | `types.ts` | `chat-unified/CitationSheet` (legacy) | 9bc5c9b31 |
| `ChatMessageRole` | Type | `types.ts` | `chat/panel/ChatMainArea`, `chat/panel/ChatMessageBubble` | 9bc5c9b31 |
| `ChatMessageItem` | Type | `types.ts` | `chat-unified/ChatMessageList`, `chat-unified/ChatThreadView` | 9bc5c9b31 |
| `StreamStateForMessages` | Type (`Pick<AgentChatStreamState, …>`) | `types.ts` | `chat-unified/ChatMessageList` | 9bc5c9b31 |
| `collectCitations` | Pure fn (`ChatMessageItem[] → Citation[]`) | `messages.ts` | `chat-unified/ChatThreadView` | 01f0cd383 |
| `getSuggestedQuestions` | Pure fn (`ChatMessageItem[] → string[]`) | `messages.ts` | `chat-unified/ChatThreadView` | 01f0cd383 |
| `useChatScroll` | Hook (auto-scroll on message change) | `useChatScroll.ts` | `chat-unified/ChatThreadView` | cb52416be |
| `UseChatScrollResult` | Type | `useChatScroll.ts` | internal | cb52416be |
| `useThreadMessages` | Hook (message list + `AbortController` lifecycle via `useReducer`) | `useThreadMessages.ts` | `chat-unified/ChatThreadView` | 3fe57f694 … e4b28a333 |
| `ThreadMessagesState`, `ThreadMessagesAction` | Reducer types (`APPEND`, `PATCH_BY_ID`, `REMOVE_BY_ID`, `REPLACE_ALL`, `SET_STREAM_STATUS`, `SET_CURRENT_ANSWER`, `SET_VOICE_FLAG`) | `useThreadMessages.ts` | internal / tests | 3fe57f694 |
| `threadMessagesReducer`, `initialThreadMessagesState` | Pure reducer + initial state | `useThreadMessages.ts` | tests | 3fe57f694 |
| `UseThreadMessagesOptions`, `UseThreadMessagesResult`, `ThreadSendContext`, `SendOptions`, `StreamStatus`, `ThreadStreamError` | Public hook types | `useThreadMessages.ts` | `chat-unified/ChatThreadView` | 3fe57f694 |

Pinned invariants that drove the `useThreadMessages` extraction live at [`apps/web/src/components/chat-unified/__tests__/ChatThreadView.invariants.test.tsx`](../../apps/web/src/components/chat-unified/__tests__/ChatThreadView.invariants.test.tsx) (6 characterization tests, commit `7cbe36829`). Invariant 5 (hydration-abort) was flipped from `.toBe(false)` to `.toBe(true)` in Task 7 (commit `e4b28a333`), closing the cross-`threadId` controller leak.

### 2.1 `useThreadMessages` API

The hook owns **all** in-flight state for a single thread: the message array, the active `AbortController`, and the `SSE`-streaming bookkeeping. It is the only sanctioned way to mutate chat messages from the view layer — direct `useState<ChatMessageItem[]>` is forbidden in `chat-unified/**`.

| Method | Signature | Purpose |
|--------|-----------|---------|
| `messages` | `ReadonlyArray<ChatMessageItem>` | Snapshot of the current thread. Never mutate directly. |
| `replaceMessages(next)` | `(ReadonlyArray<ChatMessageItem>) => void` | Hydration or full reset (REST fallback). Dispatches `REPLACE_ALL`. |
| `appendMessage(msg)` | `(ChatMessageItem) => void` | Add optimistic user bubble or assistant placeholder. Dispatches `APPEND`. |
| `patchMessageById(id, patch)` | `(string, Partial<ChatMessageItem>) => void` | Stream tokens, citations, continuation tokens. Dispatches `PATCH_BY_ID` (no-op if id not found). |
| `removeMessageById(id)` | `(string) => void` | Rollback on stream/persist error. Dispatches `REMOVE_BY_ID`. |
| `abortCurrent()` | `() => void` | Abort the current in-flight controller (if any) and clear the ref. Idempotent. |
| `beginAbort()` | `() => AbortController` | Abort any previous controller, create a fresh one, store it as the hook's current ref, and return it. Canonical single-stream gate. |

**Why granular dispatch methods and not a `setMessages` escape hatch?**

`patchMessageById` / `appendMessage` / `removeMessageById` reach the reducer via `useReducer`'s stable `dispatch` — they always operate on the latest state snapshot. This is essential for the streaming `for await` loops that live in `ChatThreadView.handleSendMessage`: those callbacks run for the full duration of an SSE stream, and a closure-captured `messages` array would grow stale after the first token. Dispatch-based APIs eliminate this trap by design.

**Why does `beginAbort()` return the controller?**

`ChatThreadView` still owns the streaming call sites (the hook's `sendMessage` is a stub — Tasks 4–6 of the deferred full-hook extraction). Callers need the `AbortSignal` to pass to `qaStream` / `fetch`. `beginAbort()` gives them the signal while guaranteeing the hook's `abortRef` is authoritative, so that `abortCurrent()` (invoked by the hydration effect on `threadId` change, or by the unmount cleanup) always cancels the right stream.

## 3. Citation type hierarchy

Per the reconciliation note in [`types.ts`](../../apps/web/src/components/chat/shared/types.ts):

- `Citation` (canonical) — re-exported from `@/types` (src/types/domain.ts). Use this type for every RAG citation anywhere in the chat scope.
- `CitationData` — **deprecated alias** of `Citation`. Retained only so that existing `chat-unified/CitationSheet` imports keep compiling. New code MUST NOT introduce `CitationData`; prefer `Citation`.
- `CitationWithPdf` — deferred. Originally planned as an extended type for PDF-modal-specific fields, but the audit showed the existing `Citation` shape is already sufficient for the current consumer set. Will be introduced only if a consumer adds fields exclusive to the PDF modal.
- `ChatCitation` (view-model in `chat/panel/ChatCitationCard.tsx`) — **NOT migrated**. It is a pure UI projection (`{ documentName, pages, excerpt, openUrl? }`) that is assembled at render time from domain data. UI projections stay co-located with their rendering component.

## 4. Rules of engagement

When adding new chat-related code:

| Adding… | Goes in | Reason |
|---------|---------|--------|
| A new chat-related **domain type** | `chat/shared/types.ts` | Single source of truth; prevents duplicate-drift. |
| A new **pure helper** over `ChatMessageItem[]` or `Citation[]` | `chat/shared/{feature}.ts` | Pure fns have no view coupling; shared by both view layers. |
| A new **view-layer hook** composing `useAgentChatStream` + local state | `chat/shared/use{Feature}.ts` | Coordinated state between stream + view; identical semantics in both layers. |
| DOM-bound logic (focus management, scroll anchoring, keyboard shortcuts) | Stays in view layer | DOM semantics differ between full-page and slide-over contexts. |
| A new **UI projection / view-model** | View layer (co-located with component) | Not a domain concept; only the rendering component needs it. |

### Module boundary (enforced)

`components/chat/shared/**` MUST NOT import from:
- `components/chat-unified/**`
- `components/chat/panel/**`

Enforced by ESLint `no-restricted-imports` rule in [`apps/web/eslint.config.mjs`](../../apps/web/eslint.config.mjs). CI fails on violation. The shared module is a **leaf** in the dependency graph: view layers depend on it, never the reverse.

## 5. Roadmap

- **Phase 0** ✅ (this module) — foundational extraction, zero behavioral change, dual-consumer via `chat/panel` migration.
- **Phase 0.5** — unify `Citation` across `ui/meeple/chat-message.tsx`, `ui/data-display/citation-link.tsx`, `chat-unified/CitationBadge.tsx` (10 scattered definitions). Extend ESLint boundary to enforce.
- **Phase 1** ✅ — `useThreadMessages` extracted and wired into `ChatThreadView`. Message list + `AbortController` lifecycle now live in `chat/shared/`. All 6 pinned invariants green (invariant 5 flipped from leaky to correct). See plan: [`2026-04-24-chat-thread-state-hook.md`](../superpowers/plans/2026-04-24-chat-thread-state-hook.md).
  - **Deferred (Phase 1.5)**: `sendMessage` / `continueStream` inside the hook are still stubs. `ChatThreadView` owns the QA-stream / REST-fallback call sites and uses `beginAbort()` + the granular dispatchers. Full in-hook streaming is deferred until the slide-over panel (`chat/panel/*`) is ready to reuse the same hook — doing it now would fossilize `chat-unified`-specific quirks into the shared layer.
- **Phases 2–6** — [chat-unified-full-page-refactor](../superpowers/plans/) — rewrite `/chat/[threadId]`, `AgentCharacterSheet`, `AgentExtraMeepleCard` to consume `chat/panel/*` components, then delete the `chat-unified/` module (~35 files). Requires first porting citations/feedback/TTS/debug-panel parity from `ChatThreadView` to `chat/panel`.

## References

- Plan (Phase 0): [`docs/superpowers/plans/2026-04-24-chat-shared-primitives-phase-0.md`](../superpowers/plans/2026-04-24-chat-shared-primitives-phase-0.md)
- Plan (Phase 1): [`docs/superpowers/plans/2026-04-24-chat-thread-state-hook.md`](../superpowers/plans/2026-04-24-chat-thread-state-hook.md)
- Source: [`apps/web/src/components/chat/shared/`](../../apps/web/src/components/chat/shared/)
- Hook unit tests: [`apps/web/src/components/chat/shared/__tests__/useThreadMessages.test.ts`](../../apps/web/src/components/chat/shared/__tests__/useThreadMessages.test.ts) (30 tests)
- Invariant tests: [`apps/web/src/components/chat-unified/__tests__/ChatThreadView.invariants.test.tsx`](../../apps/web/src/components/chat-unified/__tests__/ChatThreadView.invariants.test.tsx) (6 characterization tests)
- ESLint boundary rule: [`apps/web/eslint.config.mjs`](../../apps/web/eslint.config.mjs) (search `chat/shared`)
- E2E smoke: [`apps/web/e2e/chat/thread-view-smoke.spec.ts`](../../apps/web/e2e/chat/thread-view-smoke.spec.ts)
