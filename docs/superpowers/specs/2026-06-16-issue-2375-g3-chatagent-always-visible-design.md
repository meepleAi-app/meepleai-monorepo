# Issue #2375 G3 — ChatAgent always-visible primitive (design)

**Date**: 2026-06-16
**Parent epic**: [#2354 Session live shell](https://github.com/meepleAi-app/meepleai-monorepo/issues/2354)
**Sub-issue**: [#2375](https://github.com/meepleAi-app/meepleai-monorepo/issues/2375)
**Scope**: C (combined verify-and-close + accordion FSM wire)
**Effort estimate**: ~2-3gg

## 1. Context

G1 (`#2374`, PR `#2393`, sess.46r) shipped the 2-col 60/40 desktop layout + mobile
bottom-sheet for `/sessions/[id]/live`. The `ChatAgentPanel` primitive landed
with a frozen §5 contract that supports `collapsed`/`onHeaderClick` props for
G3, but the parent `SessionLiveView` does **not** wire the accordion FSM yet.
The LEFT 60% column currently stacks `ChatAgentPanel` (always-expanded) on top
of `ActionLogTimeline`, matching the canonical mockup
`admin-mockups/design_files/sp4-session-skeleton-live.{html,jsx}`.

This spec covers two combined deliverables:

- **(A) Verify-and-close**: audit the current G1 implementation for the
  remaining `#2375` acceptance criteria (SSE scroll preservation, axe AA,
  Playwright E2E for typing + send + receive) and ship the missing tests +
  fixes.
- **(B) Accordion FSM wire**: parent `SessionLiveView` consumes
  `collapsed`/`onHeaderClick` props, exposes URL SSOT for the collapse state,
  preserves the chat draft across collapse cycles, and gracefully manages
  scroll position when SSE delivers new messages.

The §5 frozen contract on `ChatAgentPanel` is **preserved verbatim**: no prop
rename, no body-mount semantics change, no SSE state moved inside the primitive.

## 2. Decisions locked (brainstorming session)

| ID | Decision | Rationale |
|---|---|---|
| DEC-1 | URL SSOT separati `?chat` (desktop) + `?mchat` (mobile) | Mirror del pattern esistente `?tab` / `?mtab`. Permette state divergente cross-device. |
| DEC-2 | Smart auto-scroll via `IntersectionObserver` + "N nuovi messaggi" toast quando utente è scrolled up | UX-friendly per chat live; pattern industry-standard. |
| DEC-3 | Draft persistence in `sessionStorage` con key `meepleai.chat-draft.${sessionId}` | Zero data loss + zero friction. Restored on re-expand. |
| DEC-4 | Default state expanded (no URL param) on first render | Match mockup canonical "sempre visibile". |

## 3. FSM + URL contract

```
URL params (NEW G3 additions, no rename of existing):
  ?chat=collapsed      desktop accordion state
  ?mchat=collapsed     mobile accordion state

Default = param omitted → expanded (DEC-4).
Any value other than the literal string `collapsed` → expanded (defensive
parser).

Parsers (SessionLiveView):
  parseChatCollapsed(raw)        → raw === 'collapsed'
  parseMobileChatCollapsed(raw)  → raw === 'collapsed'

Handlers (SessionLiveView):
  handleChatCollapsedChange(c)       → router.replace(buildQuery({ chat:  c ? 'collapsed' : null }))
  handleMobileChatCollapsedChange(c) → router.replace(buildQuery({ mchat: c ? 'collapsed' : null }))
```

§5 contract preserved on `ChatAgentPanel`:

- `onHeaderClick` provided → header rendered as `<button aria-expanded={!collapsed}>`.
- `collapsed=true` → body unmounts (no hidden focus traps).
- `data-collapsed="true"` attribute reflects current state (E2E selector).

Back-compat:

- Existing bookmarks without `?chat`/`?mchat` continue to render the chat
  expanded (current G1 behavior).
- No legacy aliases needed — these are net-new params.

## 4. Components + hooks API

### 4.1 `useScrollAnchor` (new)

`apps/web/src/lib/session-live/use-scroll-anchor.ts`

```ts
interface UseScrollAnchorOptions {
  containerRef: RefObject<HTMLElement>;
  bottomRef: RefObject<HTMLElement>;
  trigger: unknown; // e.g. messages.length
}

interface UseScrollAnchorReturn {
  isAtBottom: boolean;
  scrollToBottom: () => void;
}

export function useScrollAnchor(opts: UseScrollAnchorOptions): UseScrollAnchorReturn;
```

Behavior:

- Mounts an `IntersectionObserver` on `bottomRef` with `root = containerRef`.
- `isAtBottom` flips to `true` when the bottom sentinel intersects the viewport
  (last message visible).
- `scrollToBottom()` calls `bottomRef.current?.scrollIntoView({ behavior: 'smooth' })`.
- Fallback: when `IntersectionObserver` is undefined, `isAtBottom` stays `true`
  (naive auto-scroll on every new message — degraded UX for ≤1% of browsers).

### 4.2 `useChatDraft` (new)

`apps/web/src/lib/session-live/use-chat-draft.ts`

```ts
interface UseChatDraftOptions {
  sessionId: string | null;
}

interface UseChatDraftReturn {
  draft: string;
  setDraft: (next: string) => void;
  clearDraft: () => void;
}

export function useChatDraft(opts: UseChatDraftOptions): UseChatDraftReturn;
```

Behavior:

- Key: `meepleai.chat-draft.${sessionId}`.
- Mount → read `sessionStorage[key]` (SSR-safe: returns `""` when
  `typeof window === 'undefined'`).
- `setDraft` writes synchronously to `sessionStorage`.
- `clearDraft` removes the key.
- `sessionId=null` → all operations are no-ops (`draft=""`).
- Quota exceeded / sessionStorage unavailable → `console.warn` + swallow
  (component still functional, just no persistence).

### 4.3 `LiveAgentChat` modifications

The existing `LiveAgentChat` component is extended (not rewritten):

- Mount `containerRef` on the inner messages-list `<div>` carrying
  `overflow-y: auto` (the actual scroll container, not the outer panel).
- Mount `bottomRef` as an `aria-hidden` empty `<div>` rendered immediately
  after the last message item.
- Wire `useChatDraft({ sessionId })`: the input is now controlled by `draft`,
  `setDraft` on change, `clearDraft` after a successful send.
- Wire `useScrollAnchor({ containerRef, bottomRef, trigger: messages.length })`.
- On `messages.length` increase:
  - `isAtBottom=true` → call `scrollToBottom()` (auto-scroll).
  - `isAtBottom=false` → increment local `newMessageCount`, render a "N nuovi
    messaggi" button that on click calls `scrollToBottom()` and resets the
    counter.

### 4.4 `SessionLiveView` wiring delta

```tsx
const chatCollapsed       = parseChatCollapsed(searchParams.get('chat'));
const mobileChatCollapsed = parseMobileChatCollapsed(searchParams.get('mchat'));

const handleChatCollapsedChange = useCallback((c: boolean) => {
  router.replace(`${pathname}${buildQuery({ chat: c ? 'collapsed' : null })}`,
    { scroll: false });
}, [router, pathname, buildQuery]);
// (mirror for mobile)

// desktopMainColumn:
<ChatAgentPanel
  …
  collapsed={chatCollapsed}
  onHeaderClick={() => handleChatCollapsedChange(!chatCollapsed)}
  labels={chatAgentLabels}
/>

// mobileMainContent (mirror):
<ChatAgentPanel
  …
  collapsed={mobileChatCollapsed}
  onHeaderClick={() => handleMobileChatCollapsedChange(!mobileChatCollapsed)}
  labels={chatAgentLabels}
  compact
/>
```

## 5. Error handling + edge cases

| Scenario | Behavior |
|---|---|
| `sessionStorage` unavailable (Safari private, quota exceeded) | `useChatDraft` swallows error in `console.warn`; user can still send, no persistence. |
| `IntersectionObserver` unsupported | `useScrollAnchor.isAtBottom=true` always → naive auto-scroll fallback. |
| `sessionId=null` (pre-hydration race) | `useChatDraft` no-ops; collapse handlers still work (URL is independent). Chat is already locked out by existing send guard. |
| Collapse during inflight SSE event | Body unmounts but parent owns the stream (§5 contract); on re-expand the full `messages` array is replayed into a fresh `LiveAgentChat`. |
| Concurrent desktop+mobile state divergence | `?chat` and `?mchat` are independent — desired by DEC-1. URL can encode both. |
| Mobile `?msheet=open` + `?mchat=collapsed` | Independent params. Drawer covers the main column; chat header remains visible underneath when sheet closes. |
| Pre-G3 bookmarks (e.g., `?tab=score`) | No `?chat`/`?mchat` → expanded by default. Bookmark continues to work. |

## 6. Accessibility (axe AA)

- Header `<button>` carries `aria-expanded={!collapsed}` (already in §5 contract).
- Body unmount when collapsed → no hidden focus traps.
- "N nuovi messaggi" button: `aria-live="polite"` on its parent region so AT
  announces the count change without spamming on every message.
- Auto-scroll path: no `aria-live` (avoid AT spam when user is at bottom).
- New i18n keys (Gate A — resolved in `SessionLiveView` via `useTranslation`,
  passed to `ChatAgentPanel`/`LiveAgentChat` pre-formatted; no ICU templates in
  child components):

```
pages.sessionLive.chatAgent.collapsedAriaLabel  "ChatAgent collassato — clic per espandere"
pages.sessionLive.chatAgent.expandedAriaLabel   "ChatAgent espanso — clic per collassare"
pages.sessionLive.chat.newMessagesToast         "{count, plural, one {# nuovo messaggio} other {# nuovi messaggi}}"
```

## 7. Testing strategy

### 7.1 Unit (Vitest)

`use-scroll-anchor.test.ts`:

- `isAtBottom=true` when bottom sentinel intersects (mock `IntersectionObserver`).
- `isAtBottom=false` when scrolled up.
- `scrollToBottom()` calls `scrollIntoView({ behavior: 'smooth' })`.
- Fallback `isAtBottom=true` when `IntersectionObserver` is `undefined`.

`use-chat-draft.test.ts`:

- Draft loaded from `sessionStorage` on mount.
- `setDraft` writes to `sessionStorage`.
- `clearDraft` removes the key.
- `sessionId=null` → no `sessionStorage` access.
- Quota exceeded → warn + swallow.
- Distinct `sessionId` values → distinct keys.

### 7.2 Integration (Vitest + Testing Library)

`SessionLiveView.test.tsx` (extend):

- `?chat=collapsed` → `ChatAgentPanel` rendered with `collapsed=true`,
  `data-collapsed="true"`.
- Click header → URL becomes `?chat=collapsed`.
- Click header again → `?chat` param removed.
- Mirror behavior for `?mchat`.
- No `?chat` → expanded default (DEC-4).
- Combined `?chat=collapsed&mchat=collapsed` → both sides collapsed.

`LiveAgentChat.test.tsx` (extend):

- Draft loaded from `sessionStorage` on mount.
- Send → `clearDraft` called.
- Input change → `setDraft` called.
- New message when `isAtBottom=false` → "N nuovi messaggi" button visible.
- Click toast → `scrollToBottom` called, counter reset.
- New message when `isAtBottom=true` → auto-scroll, no toast.

### 7.3 E2E (Playwright)

`apps/web/e2e/session-live-chat-agent-g3.spec.ts` (NEW):

1. Open `/sessions/{id}/live` → `ChatAgentPanel` expanded (no `?chat`).
2. Click header → URL becomes `?chat=collapsed`, body hidden.
3. Reload → still collapsed (URL is SSOT).
4. Click header again → `?chat` removed, body visible.
5. Type message in input → reload → input retains draft.
6. Send → input cleared, sessionStorage cleared.
7. Scroll up → new SSE message arrives → "N nuovi messaggi" toast.
8. Click toast → auto-scroll, toast gone.

### 7.4 Accessibility (axe-core)

`apps/web/__tests__/session-live-chat-agent-g3-axe.test.tsx` (NEW):

- Default expanded → 0 violations.
- Collapsed state → 0 violations.
- `aria-expanded` matches `!collapsed`.
- "N nuovi messaggi" button → `aria-live="polite"` on parent.
- Header button → discernible label.

## 8. Acceptance criteria (issue body)

- [x] **ChatAgent visible LEFT 60%** — shipped in G1, preserved.
- [x] **No tab switching** — chat is no longer a tab (G1).
- [ ] **SSE updates non disturbano scroll** — implemented via `useScrollAnchor`
  (smart auto-scroll + toast).
- [ ] **axe AA 0 violations** — new axe test suite (§7.4).
- [ ] **E2E typing + send + receive** — new Playwright spec (§7.3).

## 9. Out of scope (for #2375)

- G5 polymorphic renderers (#2373 / #2376 / #2378) — separate sub-issues.
- `useLiveSessionStore.scoringType` / `displayName` wiring (#2389) — separate
  follow-up.
- Per-game extensions (#2377 G6 umbrella).
- Refactoring `LiveAgentChat` SSE consumption (parent still owns the stream).

## 10. Refs

- Epic parent: [#2354](https://github.com/meepleAi-app/meepleai-monorepo/issues/2354).
- G1 shipped: PR [#2393](https://github.com/meepleAi-app/meepleai-monorepo/pull/2393)
  (`5ad149ea0`, sess.46r).
- §5 frozen contract: `apps/web/src/components/features/session-live/ChatAgentPanel.tsx:12-17`.
- Mockup canonical: `admin-mockups/design_files/sp4-session-skeleton-live.{html,jsx}`.
- Sibling G5 specs: not yet specced — `#2373`, `#2378`, `#2376` to be brainstormed
  separately.

🤖 Brainstormed via `superpowers:brainstorming` skill — 4 DEC user-locked.
