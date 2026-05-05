# `/sessions/[id]/live` Hook Contract — Wave D.2 Phase 0.5

> **Phase 0.5 contract** per V2 migration spec sezione 5.
> **Tier**: L (real-time SSE + dialog focus trap + cartesian FSM connection × pause × endgame × tab × role).
> **Scope**: orchestrator `SessionLiveView` per route `/sessions/[id]/live`.
> **Issue**: TBD (child di umbrella #582). **Mockup**: `admin-mockups/design_files/sp4-session-live.{html,jsx}` + `sp4-session-live-parts.{html,jsx}`.
>
> Pattern blueprint da Wave 3 ([game-nights-hooks.md](game-nights-hooks.md)) e Wave C.2 ([agents-id-hooks.md](agents-id-hooks.md)) adattato per **real-time SSE multi-component live state + dialog focus trap + 3-role variant matrix**.
>
> **Audit gates applicati** (post-D.1 amendments PR #741): A (ICU plural) · B (schema reality) · C (MeepleCard API-fit) · D (bootstrap-then-merge). Vedi `docs/superpowers/specs/2026-05-05-wave-d-spec-panel-review.md` §10.

## 1. Route surface

| Aspect | Value |
|--------|-------|
| Route path | `/sessions/[id]/live` |
| Page component | `apps/web/src/app/(authenticated)/sessions/[id]/live/page.tsx` |
| Orchestrator | `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx` |
| Param source | `useParams<{ id: string }>()` |
| Param validation | `params?.id` può essere `undefined` (Next.js 16 app router pre-hydration) |
| **Critical**: never pass `undefined` literal o stringa `'undefined'` ai sub-hook | |
| **Migration note** | Page legacy esistente. Wave D.2 = brownfield migration. Subroute `/sessions/[id]/live` solo — `/sessions/[id]` (summary, D.3) e `/sessions/[id]/diary/*` UNTOUCHED. |
| **Theme default** | **Dark mode** (`data-theme="dark"`) — vs light default su altre route v2. Visual baselines 4 PNG (desktop+mobile × dark+light). |

### 1.1 URL state schema (single source of truth, no useState mirror)

```ts
type DialogKind = 'pause' | 'endgame' | null;
type LiveTab = 'tools' | 'chat' | 'notes';
type MobileTab = 'score' | 'log' | 'tools' | 'chat';

interface UrlState {
  dialog: DialogKind;          // ?dialog=pause|endgame, default null (no dialog open)
  tab: LiveTab;                 // ?tab=tools|chat|notes (desktop right column), default 'tools'
  mobileTab: MobileTab;         // ?mtab=score|log|tools|chat (mobile bottom-nav), default 'score'
}

// Anti-pattern: NO useState<DialogKind> + URL sync hooks.
// URL serializable for deep-link to "Sessione in pausa" confirmation.
// Pattern Wave 3 /game-nights drawer (?day=YYYY-MM-DD) sustained.
```

**`?state=` URL override** per visual fixture (gated by `STATE_OVERRIDE_ENABLED`):
- `?state=loading` → forza shell loading (Cell 2)
- `?state=connection-lost` → forza ConnectionLostBanner (Cell C)
- `?state=pause-dialog` → forza PauseOverlay aperto (visual baseline)
- `?state=endgame-dialog` → forza EndgameDialog aperto (visual baseline)
- `?state=error` → NO visual override (TanStack `isError` non riproducibile via URL deterministicamente, coperto unit test)

## 2. Hook dependency graph

```
useParams<{id}> ──validate──→ sessionId: string | null
                                  │
                                  │ (null se params?.id è undefined o "")
                                  │
                                  ├─→ useSession(sessionId) [PARENT]
                                  │       │
                                  │       └─→ STATE: idle | loading | error | success(session)
                                  │              │
                                  │              ├─ session.viewerRole — Spectator | Player | Host
                                  │              ├─ session.status — InProgress | Paused | Setup
                                  │              ├─ session.players — ReadonlyArray<SessionPlayerDto>
                                  │              │
                                  │              └─→ useSessionLiveStream({ sessionId, enabled: true }) [REAL-TIME]
                                  │                     │
                                  │                     └─→ STATE: connecting | connected | reconnecting |
                                  │                                degraded-polling | failed
                                  │                            │
                                  │                            ├─ events: ReadonlyArray<SessionEvent>
                                  │                            │   (replayed from Last-Event-ID + live tail)
                                  │                            │
                                  │                            └─→ Reducer composes session live state:
                                  │                                  - playerScores (live updates)
                                  │                                  - currentTurn (live updates)
                                  │                                  - actionLog (append-only)
                                  │                                  - chatMessages (append-only)
                                  │
                                  └─→ shells: loading / error / not-found / unauthorized (403 spectator illegal action)
```

**Key constraints**:
1. **`sessionId` è `string | null`, MAI `undefined` o `'undefined'`** (mirror Wave C.1 sez. 2.1)
2. **`useSession` è il parent** — `useSessionLiveStream` NON monta finché parent !== success E data !== null
3. **SSE NON refetcha session DTO** — session DTO è fetched once, SSE applies deltas via reducer
4. **Polling fallback** quando SSE retry exhausted (5 retries) → switch to `useSessionState(5s)` (already exists)
5. **Optimistic UI client-side** per Player+Host actions; server enforces role → handle 403 gracefully

### 2.1 SessionId resolution

Identico a Wave C.1 sez. 2.1:

```ts
// CORRETTO
const params = useParams<{ id: string }>();
const rawId = params?.id;
const sessionId = typeof rawId === 'string' && rawId.length > 0 ? rawId : null;

// SBAGLIATO (anti-pattern Wave C.1 PR #697)
const id = params?.id ?? '';
```

### 2.2 Sub-hook gating + reducer composition

```ts
// useSession (parent) — fetches session DTO once
const sessionQuery = useSession(sessionId);

// useSessionLiveStream — SSE subscription, mounts on parent success
const liveStream = useSessionLiveStream({
  sessionId,
  enabled: !!sessionId && sessionQuery.isSuccess && sessionQuery.data != null,
});

// Reducer composes events with parent DTO
const liveState = useMemo<SessionLiveState>(() => {
  if (!sessionQuery.data) return null;
  return composeSessionLiveState(sessionQuery.data, liveStream.events);
}, [sessionQuery.data, liveStream.events]);

// Polling fallback when SSE failed
const pollingQuery = useSessionState({
  sessionId,
  enabled: !!sessionId && liveStream.connectionState === 'degraded-polling',
  refetchInterval: 5_000,
});
```

**Anti-pattern dal PR #697 (Wave C.1) e D.1 PR #736 sustained**:
- ❌ `useSessionLiveStream(sessionId ?? 'undefined')` (literal string)
- ❌ Skip `sessionQuery.data != null` check (Cell 4 race)
- ❌ Mount SSE before parent success (wasted connection + race condition on viewerRole)

### 2.3 SSE backend endpoint (per §10.9 audit Gate B resolution)

**Endpoint**: `GET /api/v1/game-sessions/{sessionId}/stream/v2`

Verified production-ready in `apps/api/src/Api/Routing/SessionTracking/SessionQueryEndpoints.cs:321-418`. All Newman recommendations IMPLEMENTED server-side.

**Request shape**:
```http
GET /api/v1/game-sessions/{sessionId}/stream/v2
Cookie: <session-cookie>          // RequireAuthenticatedUser
Last-Event-ID: <last-id-on-reconnect>  // optional, browser auto-tracks
```

**Response shape**:
```http
HTTP/1.1 200 OK
Content-Type: text/event-stream
Cache-Control: no-cache
Connection: keep-alive

id: <event-id>
event: <typed-name>     // e.g., session:score, session:turn
data: <json-payload>

event: heartbeat
data: {"timestamp":"<ISO-8601>"}
```

**Error responses**:
- `401 Unauthorized` — no session cookie
- `403 Forbidden` — viewer has no access to this session
- `404 Not Found` — session does not exist
- `429 Too Many Requests` — connection pool full (20 max/session)

**Service layer**: `ISessionBroadcastService.SubscribeAsync(sessionId, userId, lastEventId, ct)` returns `IAsyncEnumerable<EventEnvelope { Id, EventType, Data }>`.

## 3. SSE event schema (typed events enum)

> **Audit Gate B applied**: schema reality v1 carryover audit. Event types verified in `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Events/` directory.

### 3.1 Event types enum

```ts
// apps/web/src/lib/session-live/sse-events.ts
export type SessionEventType =
  | 'session:score'           // ScoreUpdatedEvent
  | 'session:turn'            // TurnAdvancedEvent
  | 'session:player-join'     // ParticipantJoinedEvent
  | 'session:player-leave'    // ParticipantLeftEvent
  | 'session:role-change'     // ParticipantRoleChangedEvent
  | 'session:pause'           // SessionPausedEvent
  | 'session:resume'          // SessionResumedEvent
  | 'session:endgame'         // SessionEndedEvent
  | 'session:chat'            // ChatMessageEvent (private + shared)
  | 'session:tool-execution'  // ToolExecutedEvent (dice/timer/card)
  | 'session:diary'           // DiaryEntryEvent (note added)
  | 'heartbeat';              // server keep-alive (no payload action)

// Discriminated union per payload
export type SessionEvent =
  | { type: 'session:score'; sessionId: string; participantId: string; score: number; updatedBy: string; timestamp: string }
  | { type: 'session:turn'; sessionId: string; turnNumber: number; activePlayerId: string; timestamp: string }
  | { type: 'session:player-join'; sessionId: string; participantId: string; playerName: string; role: ParticipantRole; timestamp: string }
  | { type: 'session:player-leave'; sessionId: string; participantId: string; timestamp: string }
  | { type: 'session:role-change'; sessionId: string; participantId: string; oldRole: ParticipantRole; newRole: ParticipantRole; assignedBy: string; timestamp: string }
  | { type: 'session:pause'; sessionId: string; pausedBy: string; reason?: string; timestamp: string }
  | { type: 'session:resume'; sessionId: string; resumedBy: string; timestamp: string }
  | { type: 'session:endgame'; sessionId: string; finalScores: ReadonlyArray<{ participantId: string; score: number; winner: boolean }>; timestamp: string }
  | { type: 'session:chat'; sessionId: string; messageId: string; senderId: string; content: string; visibility: 'private' | 'shared'; timestamp: string }
  | { type: 'session:tool-execution'; sessionId: string; tool: 'dice' | 'timer' | 'card'; outcome: unknown; executedBy: string; timestamp: string }
  | { type: 'session:diary'; sessionId: string; entryId: string; authorId: string; content: string; timestamp: string }
  | { type: 'heartbeat'; timestamp: string };

export type ParticipantRole = 'Spectator' | 'Player' | 'Host';
```

**Schema reality v1 audit** (Gate B): event payload shapes MUST be verified against backend `*.cs` event records in `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Events/`. If backend payload differs (e.g., field name divergence), document v1 carryover similar to D.1 SessionPlayerDto pattern.

**Pre-implementation grep audit**:
```bash
grep -rn "INotification\|public sealed record.*Event" apps/api/src/Api/BoundedContexts/SessionTracking/Application/Events/
```

### 3.2 EventSource integration

```ts
// apps/web/src/hooks/queries/useSessionLiveStream.ts
export type SseConnectionState =
  | 'connecting'        // initial open in flight
  | 'connected'         // first event received
  | 'reconnecting'      // browser retrying after drop (within retry budget)
  | 'degraded-polling'  // retry budget exhausted, fall back to polling
  | 'failed';           // permanent failure (e.g., 403/404/429)

export function useSessionLiveStream(input: {
  sessionId: string | null;
  enabled: boolean;
}): {
  events: ReadonlyArray<SessionEvent>;
  connectionState: SseConnectionState;
  lastEventId: string | null;
  retryCount: number;
  retryAt: Date | null;          // next retry timestamp
} {
  // Uses native EventSource API (browser handles Last-Event-ID automatically)
  // url: `/api/v1/game-sessions/${sessionId}/stream/v2`
  // Reducer applies events to local store
  // Retry budget [1s, 2s, 4s, 8s, 16s] tracked client-side
  // After 5 retries → switch to 'degraded-polling' state
  // 429 response → connectionState = 'failed', show toast
  // ...
}
```

**Browser-native Last-Event-ID handling**: when `EventSource` reconnects after drop, it automatically sends `Last-Event-ID` header with the last `id:` value received. No client-side state tracking needed — browser handles it.

**Retry budget client-side** (Newman recommendation):
```ts
const RETRY_BUDGET_MS = [1_000, 2_000, 4_000, 8_000, 16_000];  // total ~31s
// On EventSource error event:
//   if (retryCount < 5) schedule next retry per budget[retryCount]
//   else → connectionState = 'degraded-polling', useSessionState(5_000) takes over
```

### 3.3 Polling fallback (Newman recommendation)

When SSE retry budget exhausted, fall back to existing `useSessionState`:

```ts
const pollingQuery = useSessionState({
  sessionId,
  enabled: !!sessionId && liveStream.connectionState === 'degraded-polling',
  refetchInterval: 5_000,  // 5s polling cadence
});
```

User-visible: `ConnectionLostBanner` shows "Connessione instabile, aggiornamenti ogni 5s" + auto-hides on SSE reconnect (manual user retry button).

### 3.4 Connection pool 429 handling

When server returns 429 (>20 connections/session):
- `connectionState = 'failed'`
- Show toast: "Sessione affollata — riprova tra qualche minuto"
- Disable retry button for 60s (prevent thundering herd)
- After 60s, allow manual retry

## 4. FSM cell matrix

Cartesian rilevante (subset di celle critical, larger than Wave 3 due to dialog × connection × role):

### 4.1 Index-level FSM (5-state shell)

| # | sessionId | useSession | UI Behavior |
|---|-----------|------------|-------------|
| 1 | `null` | `disabled` | **Shell `not-found`**: hero illustrato + CTA back to /sessions. Nessuna fetch. |
| 2 | valid | `loading` | **Shell `loading`**: skeleton 3-column desktop / single-column mobile. SSE NON montato. |
| 3 | valid | `error` | **Shell `error`**: error card + CTA retry. SSE NON montato. |
| 4 | valid | `success(null)` | **Shell `not-found`**: session non trovata (404 logical). |
| 5 | valid | `success(data)` | **Shell `default`**: 3-column desktop / mobile bottom-nav, SSE montato. |

### 4.2 SSE connection state FSM (cross-cutting con Cell 5)

| # | connectionState | UI Behavior |
|---|-----------------|-------------|
| C1 | `connecting` | Skeleton sub-content + small connection indicator (dot pulsing) |
| C2 | `connected` | Default render, no banner |
| C3 | `reconnecting` | Default render + ConnectionLostBanner "Connessione persa, riprovo... (tentativo {n}/5)" |
| C4 | `degraded-polling` | Default render + ConnectionLostBanner "Aggiornamenti ogni 5s — clicca per riprovare in tempo reale" |
| C5 | `failed` | Default render + toast "Sessione affollata o errore — riprova tra 60s" + retry button disabled |

### 4.3 Dialog state FSM (independent, URL `?dialog=`)

| # | dialog URL | UI Behavior |
|---|-----------|-------------|
| D1 | `null` | No dialog rendered |
| D2 | `pause` | **PauseOverlay** mounted, focus trap active, ESC closes (returns to default) |
| D3 | `endgame` | **EndgameDialog** mounted, focus trap active, **ESC disabled** (intentional — cannot accidentally dismiss endgame summary, must click acknowledgement CTA) |

### 4.4 Role variant FSM (cross-cutting con Cell 5, server-enforced)

| # | viewerRole | Server-enforced + UI affordances |
|---|------------|----------------------------------|
| R1 | `Spectator` | Read-only. Chat enabled (visibility=shared). Score/dice/timer/card buttons hidden OR disabled with tooltip "Solo i giocatori possono aggiornare". Action log visible. |
| R2 | `Player` | Score input enabled (own score only). Dice/timer/card enabled. Chat enabled. Pause/resume hidden. Kick hidden. |
| R3 | `Host` | All Player actions + advance turn + pause/resume + kick participants + modify toolkit. EndgameDialog "Termina sessione" CTA enabled. |

**Optimistic UI handling** (Crispin):
- Player+Host clicks score update → optimistic update local state immediately
- Server enforces role; if 403 → rollback optimistic update + toast "Permesso negato"
- Spectator UI hides write controls server-side validates anyway (defense-in-depth)

### 4.5 Critical assertion contracts

- ⚠️ **Cell 4 vs Cell 1**: stesso UI shell `not-found` ma origini diverse (mirror Wave C.1)
- ⚠️ **Cell C3 vs C4**: ConnectionLostBanner reuses component, only message text changes (i18n templates)
- ⚠️ **Cell D3 EndgameDialog ESC disabled**: intentional WCAG deviation per UX (endgame summary critical info, accidental dismiss = data loss). Document in component JSDoc.
- ⚠️ **Cell R1 Spectator**: write actions MUST be hidden server-side (UI affordance) AND server validates (defense-in-depth). Test 403 rollback path explicitly.
- ⚠️ **Reconnection event replay**: when SSE reconnects with `Last-Event-ID`, server replays missed events. Reducer must be idempotent (same event applied twice = same state — use event.id as dedup key).

### 4.6 State derivation function

```ts
// apps/web/src/lib/session-live/session-live-state.ts
export type SessionLiveUiState = 'loading' | 'error' | 'not-found' | 'default';
export type SessionLiveDialogState = 'none' | 'pause' | 'endgame';
export type SessionLiveConnectionState = SseConnectionState;

export function deriveSessionLiveUiState(input: {
  sessionId: string | null;
  isLoading: boolean;
  isError: boolean;
  hasData: boolean;
}): SessionLiveUiState {
  if (input.sessionId == null) return 'not-found';   // Cell 1
  if (input.isLoading) return 'loading';              // Cell 2
  if (input.isError) return 'error';                  // Cell 3
  if (!input.hasData) return 'not-found';             // Cell 4
  return 'default';                                    // Cell 5
}

export function deriveSessionLiveDialogState(searchParams: URLSearchParams): SessionLiveDialogState {
  const raw = searchParams.get('dialog');
  if (raw === 'pause') return 'pause';
  if (raw === 'endgame') return 'endgame';
  return 'none';
}
```

## 5. Component contracts

> **Audit Gate C applied** (MeepleCard API-fit): Wave D.2 components are LIVE/REAL-TIME UI — radically different from MeepleCard list/grid card primitives. Decision: ALL components DIVERGE from MeepleCard. None of these are "card" pattern — they are bespoke live UI elements.

### 5.1 LiveTopBar

```ts
interface LiveTopBarLabels {
  readonly sessionTitle: string;     // "Sessione live"
  readonly turnLabel: string;         // "Turno {turn}/{total}" (resolved by orchestrator via t())
  readonly pauseCta: string;          // "Pausa"
  readonly resumeCta: string;         // "Riprendi"
  readonly endgameCta: string;        // "Termina sessione"
  readonly exitAriaLabel: string;     // "Esci dalla sessione"
}

interface LiveTopBarProps {
  readonly sessionName: string;
  readonly status: 'InProgress' | 'Paused';
  readonly viewerRole: ParticipantRole;
  readonly onPause?: () => void;      // only Host (variant)
  readonly onResume?: () => void;     // only Host (variant)
  readonly onEndgame?: () => void;    // only Host (variant)
  readonly onExit: () => void;
  readonly labels: LiveTopBarLabels;
}
```

**Render rules**:
- Spectator + Player: title + turn label + exit (no Pause/Resume/Endgame CTAs)
- Host: all CTAs visible
- Status `Paused` + Host: only `Resume` shown (no `Pause`)

### 5.2 TurnIndicator

```ts
interface TurnIndicatorProps {
  readonly current: number;
  readonly total: number;
  readonly activePlayerName: string;
  readonly isMyTurn: boolean;        // viewerRole === Player && activePlayerId === viewerId
  readonly compact?: boolean;
  readonly labels: { readonly turnLabel: string; readonly currentTurnAriaLabel: string };
}
```

**A11y**: `role="progressbar" aria-valuenow={current} aria-valuemin={1} aria-valuemax={total}` — already present in mockup.

### 5.3 PlayerRosterLive

```ts
interface PlayerRosterLiveProps {
  readonly players: ReadonlyArray<LivePlayerEntry>;
  readonly viewerId: string;
  readonly viewerRole: ParticipantRole;
  readonly onKickParticipant?: (participantId: string) => void;  // Host only
  readonly compact?: boolean;
  readonly labels: PlayerRosterLiveLabels;
}

interface LivePlayerEntry {
  readonly id: string;
  readonly name: string;
  readonly role: ParticipantRole;
  readonly score: number;
  readonly isActive: boolean;        // current turn
  readonly isOnline: boolean;         // SSE-derived presence
}
```

**Variants**:
- Spectator/Player: name + score + active indicator + online status
- Host: name + score + active indicator + online status + kick button (own controls hidden)

### 5.4 LiveScoringPanel

```ts
interface LiveScoringPanelProps {
  readonly scores: ReadonlyArray<{ playerId: string; playerName: string; score: number; isWinner: boolean }>;
  readonly viewerRole: ParticipantRole;
  readonly viewerId: string;
  readonly onScoreUpdate?: (playerId: string, delta: number) => void;  // Player+Host (own score only for Player)
  readonly emptyTurn?: boolean;
  readonly compact?: boolean;
  readonly labels: LiveScoringPanelLabels;
}
```

**Server enforcement**: Player can only update own score; Host can update any. Server returns 403 if Player attempts another player's score → handle with toast + rollback.

### 5.5 ActionLogTimeline

```ts
interface ActionLogTimelineProps {
  readonly entries: ReadonlyArray<ActionLogEntry>;
  readonly compact?: boolean;
  readonly labels: ActionLogTimelineLabels;
}

interface ActionLogEntry {
  readonly id: string;
  readonly type: 'score' | 'tool' | 'agent' | 'chat' | 'photo' | 'event';
  readonly authorName: string;
  readonly content: string;
  readonly timestamp: string;
}
```

**Append-only render**: new events from SSE prepended (newest at top). Auto-scroll to top on new event UNLESS user has scrolled away (preserve reading position).

### 5.6 SessionToolsRail

```ts
interface SessionToolsRailProps {
  readonly tools: ReadonlyArray<{ id: string; name: string; icon: string; color: string }>;
  readonly viewerRole: ParticipantRole;
  readonly onToolExecute: (toolId: string) => void;   // hidden for Spectator
  readonly compact?: boolean;
  readonly labels: SessionToolsRailLabels;
}
```

**Spectator**: tool rail hidden entirely. Player+Host: full grid of tool cards.

### 5.7 LiveAgentChat

```ts
interface LiveAgentChatProps {
  readonly messages: ReadonlyArray<ChatMessage>;
  readonly viewerRole: ParticipantRole;
  readonly viewerId: string;
  readonly onSendMessage: (content: string, visibility: 'private' | 'shared') => void;
  readonly compact?: boolean;
  readonly labels: LiveAgentChatLabels;
}
```

**Spectator**: can send `visibility=shared` only (no private channel). Player+Host: both visibilities.

### 5.8 LiveSessionNotes

```ts
interface LiveSessionNotesProps {
  readonly notes: ReadonlyArray<NoteEntry>;
  readonly viewerRole: ParticipantRole;
  readonly viewerId: string;
  readonly onAddNote: (content: string, visibility: 'private' | 'shared') => void;  // hidden for Spectator
  readonly labels: LiveSessionNotesLabels;
}
```

### 5.9 RightColumnTabs (desktop only)

```ts
interface RightColumnTabsProps {
  readonly activeTab: LiveTab;       // 'tools' | 'chat' | 'notes'
  readonly onTabChange: (next: LiveTab) => void;
  readonly children: React.ReactNode;  // tab content
  readonly labels: { readonly tabsAriaLabel: string; readonly tools: string; readonly chat: string; readonly notes: string };
}
```

**A11y**: `role="tablist" aria-orientation="horizontal"` + roving tabindex via `useTablistKeyboardNav` hook (Wave A.6 PR #623 reuse).

### 5.10 PauseOverlay (DIALOG, focus trap critical)

```ts
interface PauseOverlayProps {
  readonly pausedBy: string;
  readonly pausedAt: string;
  readonly viewerRole: ParticipantRole;
  readonly onResume?: () => void;       // Host only
  readonly onClose: () => void;          // Spectator/Player → close, Host → close + URL drops ?dialog=
  readonly labels: PauseOverlayLabels;
}
```

**A11y critical** (Gate C lessons sustained, Wave A.4 invites pattern):
- `role="dialog" aria-modal="true" aria-labelledby={titleId} aria-describedby={descId}`
- Focus trap with Tab cycle constrained to dialog
- ESC closes (Spectator/Player → just closes overlay, Host → can also resume via CTA)
- Background `aria-hidden="true"` while dialog open
- Restore focus on close
- `prefers-reduced-motion`: snap open/close (no fade animation)

### 5.11 EndgameDialog (DIALOG, focus trap + ESC DISABLED)

```ts
interface EndgameDialogProps {
  readonly finalScores: ReadonlyArray<{ playerName: string; score: number; isWinner: boolean }>;
  readonly endedAt: string;
  readonly endedBy: string;
  readonly onAcknowledge: () => void;    // closes dialog, navigates to /sessions/[id] (D.3 summary)
  readonly labels: EndgameDialogLabels;
}
```

**A11y** (intentional WCAG deviation):
- `role="dialog" aria-modal="true"` + focus trap + Tab cycle
- **ESC DISABLED** — cannot accidentally dismiss endgame summary (data loss risk)
- Document deviation in JSDoc + i18n label "Premi 'Conferma' per continuare"
- Acknowledgement CTA is the ONLY exit path
- After acknowledge → orchestrator navigates to `/sessions/[id]` (D.3 summary)

### 5.12 ConnectionLostBanner

```ts
type ConnectionLostBannerKind = 'reconnecting' | 'degraded-polling' | 'failed';

interface ConnectionLostBannerProps {
  readonly kind: ConnectionLostBannerKind;
  readonly retryCount?: number;          // for kind='reconnecting'
  readonly retryAt?: Date | null;        // for kind='reconnecting'
  readonly onManualRetry?: () => void;   // for kind='degraded-polling' or 'failed'
  readonly labels: ConnectionLostBannerLabels;
}
```

**A11y**: `role="status" aria-live="polite"` (kind='reconnecting'), `role="alert"` (kind='failed').

### 5.13 ⚠️ A11y CTA contrast pre-emption (Wave C.1 lesson + dark mode audit)

- 700-shade discipline sustained for white-text CTAs
- **NEW**: dark mode contrast audit — text on dark backgrounds must use 200/300-shade (light text) for WCAG AA 4.5:1
- Audit pre-impl: `grep -E "bg-(emerald|amber|session|rose)-(600|700)" components/v2/session-live/`
- **Audit Gate B applied**: token contrast in dark theme = `--c-session-on-dark` token TBD post-impl

## 6. Test coverage plan

Per Tier L spec sez. 4.1 ratio: **50% unit / 35% integration / 15% e2e**.

### 6.1 Unit tests (~50% — ~80 tests)

| Target | Tests |
|--------|-------|
| `deriveSessionLiveUiState` | Cells 1-5 + edge cases — 8 tests |
| `deriveSessionLiveDialogState` | URL param parsing — 4 tests |
| `parseStateOverride` | Valid/invalid/disabled — 4 tests |
| `composeSessionLiveState` (reducer) | Each event type → state transition — ~30 tests |
| `useSessionLiveStream` hook | EventSource mount/unmount, retry budget, polling fallback, 429 handling — ~12 tests |
| 13 v2 components | Render shape per role variant (Spectator/Player/Host), discriminated unions — ~40 tests |

### 6.2 Integration tests (35% — ~30 tests)

Orchestrator-level via `renderHook` + MSW mocks:

| Target | Cell |
|--------|------|
| `sessionId === null` | Cell 1 |
| `useSession.loading` | Cell 2 |
| `useSession.error` | Cell 3 |
| `useSession.success(null)` | Cell 4 |
| `useSession.success(data)` + SSE `connecting` | Cell 5 + C1 |
| SSE event → reducer → UI update (score, turn, etc.) | Cell 5 + C2 |
| SSE drop → reconnecting banner | Cell 5 + C3 |
| SSE 5 retries exhausted → polling fallback | Cell 5 + C4 |
| 429 response → failed state + toast | Cell 5 + C5 |
| `?dialog=pause` URL → PauseOverlay mounts | Cell 5 + D2 |
| `?dialog=endgame` URL → EndgameDialog mounts, ESC ignored | Cell 5 + D3 |
| Spectator role → write actions hidden | Cell 5 + R1 |
| Player role → own score input enabled, others' disabled | Cell 5 + R2 |
| Host role → all admin actions visible | Cell 5 + R3 |
| Player attempts another player's score → 403 → rollback | (security) |
| **SSE-to-polling transition test** (Wave D.2 NEW per #741 §10) | (degraded mode) |

**Critical assertions in all integration tests**:
```ts
const sseHandler = (req) => {
  const url = new URL(req.url);
  const sessionId = url.pathname.match(/\/game-sessions\/([^/]+)\/stream\/v2/)?.[1];
  expect(sessionId).not.toBe('undefined');
  expect(sessionId).not.toBe('null');
  expect(sessionId).not.toBe('');
  expect(sessionId).toMatch(/^[a-f0-9-]{36}$/);
  // ...
};
```

### 6.3 E2E tests (15% — ~10 specs)

- `e2e/v2-states/session-live.spec.ts` — 5 base FSM × 2 viewports × 2 themes (light+dark) = 20 PNG (D2/D3 dialog states + C3/C4 banner states adds 4 more)
- `e2e/visual-migrated/sp4-session-live.spec.ts` — visual baseline 1280×720 + 375×812 × dark+light = 4 PNG
- `e2e/a11y/session-live.spec.ts` — axe-core WCAG 2.1 AA + reduced-motion + dialog focus trap (PauseOverlay + EndgameDialog) + keyboard nav RightColumnTabs
- `e2e/smoke-real-backend/session-live.smoke.spec.ts` — deterministic seed, real SSE event flow against staging (D.2 interactions sub-PR scope)

**Dialog focus trap test** (critical):
```ts
test('PauseOverlay focus trap: Tab cycles within dialog', async ({ page }) => {
  await page.goto('/sessions/<fixture-id>/live?dialog=pause');
  await page.waitForSelector('[data-slot="pause-overlay"]');
  // Focus first focusable in dialog
  await page.keyboard.press('Tab');
  // Tab through → should NOT escape dialog
  for (let i = 0; i < 10; i++) {
    await page.keyboard.press('Tab');
    const focused = await page.evaluate(() => document.activeElement?.closest('[role="dialog"]'));
    expect(focused).toBeTruthy();
  }
});
```

## 7. Bundle budget

Tier L per spec sez. 1.3: max +120 KB. Estimate post-D.1 actuals (Newman + Fowler):
- 13 v2 components: ~58 KB
- Orchestrator: ~25 KB (URL state + role variants + dialog management)
- SSE hook + reducer: ~12 KB
- i18n keys (it+en, ICU plural for turn/score templates): ~14 KB
- Visual fixture: ~6 KB

**Subtotal**: ~115 KB.

**Code-split** (per #741 §10.4): `PauseOverlay` + `EndgameDialog` via `React.lazy` — only loaded when triggered. Saves ~15 KB from initial bundle.

**Target initial**: ~100 KB.
**Target with dialogs lazy-loaded**: ~115 KB total (dialogs ~15 KB lazy chunk).

## 8. Sub-PR split definition

Per #582 spec + #741 §10.4 sequencing, D.2 splits 2 sub-PRs:

### 8.1 Foundation sub-PR (Days 2-4 per #741 timeline)

**Scope**: read-only UI with **static fixture** (no SSE wiring).

Components:
- LiveTopBar (without onPause/onResume/onEndgame handlers)
- TurnIndicator
- PlayerRosterLive (static data)
- LiveScoringPanel (read-only)
- ActionLogTimeline (static entries)
- DesktopBody / MobileBody layout shells

Foundation library:
- `lib/session-live/session-live-state.ts` (FSM derivation)
- `lib/session-live/sse-events.ts` (event types enum + payload schemas)
- `lib/session-live/visual-test-fixture.ts` (sentinel pattern, 1 active session fixture with deterministic event log)

Tests: ~100 tests (foundation + components, no integration with SSE yet).

Bundle target: ~60 KB.

### 8.2 Interactions sub-PR (Days 6-8 per #741 timeline)

**Scope**: real SSE wiring + dialogs + write actions.

Components:
- SessionToolsRail (write actions)
- LiveAgentChat (write actions)
- LiveSessionNotes (write actions)
- RightColumnTabs (orchestration)
- PauseOverlay (lazy)
- EndgameDialog (lazy)
- ConnectionLostBanner

Hook:
- `useSessionLiveStream` (real EventSource wiring)
- Reducer composition with `composeSessionLiveState`

Orchestrator integration:
- URL state SSOT (`?dialog=`, `?tab=`, `?mtab=`)
- 5-state FSM × connection × dialog × role variant matrix wiring
- Optimistic UI for Player+Host write actions + 403 rollback

Tests: ~50 additional tests (integration + E2E + smoke).

Bundle target: ~55 KB additional → total D.2 ~115 KB.

## 9. Coexistence flag

**Decisione**: NO flag (mirror Wave C.1/C.2/B.3/Wave 3/D.1 — app pre-prod, big-bang accettabile post-Phase 0.5).

Rollback path: `git revert` PR squash commit + `next.config.js` non ha redirect `/sessions/[id]/live` (verified — see audit checklist §10).

## 10. Pre-implementation audit checklist (4 audit gates A-D applied)

⚠️ **Verificare PRIMA del dispatch implementation subagent**:

### Gate A — ICU plural defensive pattern (#741 §10.2 Gate A)

- [ ] Audit i18n keys con `{count, plural, ...}` ICU formatter:
  - `pages.sessionLive.topBar.turnLabel` (turn count)
  - `pages.sessionLive.scoring.playerCount`
  - `pages.sessionLive.connectionLost.retryCount`
- [ ] Orchestrator usa `t(key, { count })` — MAI `intl.messages[key].replace(...)`
- [ ] Component riceve `string` resolto (NOT template + count)
- [ ] Pre-impl grep audit: `grep "{count, plural" apps/web/src/locales/*.json | grep sessionLive`

### Gate B — Schema reality v1 carryover (#741 §10.2 Gate B)

- [ ] **SSE event payload audit**: `grep -rn "INotification\|public sealed record.*Event" apps/api/src/Api/BoundedContexts/SessionTracking/Application/Events/`
- [ ] Map mockup field names to actual backend event records (e.g., `ScoreUpdatedEvent` shape)
- [ ] Document v1 carryover gaps in `lib/session-live/sse-events.ts` JSDoc
- [ ] **Session DTO audit**: verify `viewerRole`, `players[].role` fields exist on `GameSessionDto` schema
- [ ] **Note**: D.1 SessionPlayerDto missing score/winner already documented; verify D.2 if needed

### Gate C — MeepleCard API-fit audit (#741 §10.2 Gate C)

- [x] **Decision**: ALL 13 D.2 components DIVERGE from MeepleCard (live/real-time UI, not card pattern)
- [x] Justification documented per component (§5)
- [x] No MeepleCard wrap attempts in Foundation sub-PR

### Gate D — Bootstrap-then-merge discipline (#741 §10.2 Gate D)

- [ ] Foundation sub-PR opens → trigger `gh workflow run visual-regression-migrated.yml -f mode=bootstrap`
- [ ] Wait for PNG baselines committed to branch
- [ ] Verify visual regression passes against baselines
- [ ] THEN consider merge (--admin only if E2E DB flake confirmed)
- [ ] Same discipline for Interactions sub-PR

### Other prerequisites

- [ ] **Redirect cleanup**: `grep -n "/sessions/.*live" apps/web/next.config.js` — verificare nessun redirect attivo
- [ ] **Proxy.ts rewrite**: `grep -n "sessions.*live" apps/web/src/proxy.ts` — verificare nessun rewrite intercetta path
- [ ] **Subroute preservation**: `/sessions/[id]` (D.3 summary), `/sessions/[id]/diary/*`, layout.tsx UNTOUCHED
- [ ] **A11y CTA contrast pre-emption**: 700-shade per white text on light, 200/300-shade for light text on dark
- [ ] **Page boundary normalization**: page.tsx convertita a client component thin shell con `<Suspense>` (CRITICAL per `useSearchParams()`)
- [ ] **Triple auth helper** in E2E specs: `seedAuthSession + seedCookieConsent + mockAuthEndpoints`
- [ ] **`useTablistKeyboardNav` riuso** per RightColumnTabs (PR #623)
- [ ] **Default theme dark for /sessions/[id]/live** — explicit `data-theme="dark"`, visual baselines 4 PNG (×2 themes)
- [ ] **Dialog focus trap library check**: verify `focus-trap-react` or equivalent in deps; if missing, add to bundle (Wave A.4 invites pattern)
- [ ] **Code-split lazy dialogs**: PauseOverlay + EndgameDialog via `React.lazy` (per #582 DoD bundle budget)

## 11. Open questions (deferred to implementation)

- [ ] Endgame dialog → D.3 summary auto-redirect: should completing endgame trigger `router.push('/sessions/[id]')` automatically? Or manual user click?
  - Recommendation: explicit "Vedi riepilogo" CTA in EndgameDialog → navigate manually (avoid surprise navigation).
- [ ] Dark mode token coverage audit: `--c-session-on-dark` token may need separate audit (Wave C.1 hotfix only validated light)
  - Recommendation: dedicated a11y E2E spec asserting dark theme contrast ≥ 4.5:1 via axe-core.
- [ ] Mobile bottom-nav vs desktop 3-column divergence threshold: when does responsive Tailwind become insufficient?
  - Recommendation: single component tree, responsive classes, `isMobile` prop only for fundamentally different navigation (mobile bottom-nav vs desktop tabs).

## 12. References

- Phase 0.5 contract Wave C.1 (template): `docs/frontend/contracts/games-id-hooks.md`
- Phase 0.5 contract Wave C.2 (template multi-tab): `docs/frontend/contracts/agents-id-hooks.md`
- Phase 0.5 contract Wave 3 (index page + drawer): `docs/frontend/contracts/game-nights-hooks.md`
- Spec V2 migration §5 (Wave D scope): `docs/superpowers/specs/2026-04-26-v2-design-migration.md`
- Spec V2 phase 1 execution §6: `docs/superpowers/specs/2026-04-27-v2-migration-phase1-execution.md`
- **Spec-panel review + post-D.1 amendments**: `docs/superpowers/specs/2026-05-05-wave-d-spec-panel-review.md` (especially §10 amendments + §10.9 gates resolution)
- Issue #582 (Wave D umbrella sessions triade)
- PR #736 (Wave D.1 `/sessions` Tier S blueprint — schema reality v1 carryover pattern)
- PR #741 (post-D.1 amendments + 4 audit gates)
- Mockup source: `admin-mockups/design_files/sp4-session-live.{html,jsx}` + `sp4-session-live-parts.{html,jsx}`
- Stub directory: `apps/web/src/components/v2/session-live/` (7 stubs ready)
- Backend SSE endpoint: `apps/api/src/Api/Routing/SessionTracking/SessionQueryEndpoints.cs:321` (`/game-sessions/{id}/stream/v2`)
- Backend event records: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Events/`
- ParticipantRole enum: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/ParticipantRole.cs`
- Memory feedback files (lessons applicable to D.2):
  - `feedback_v2-tier-dispatch-strategy.md` (Tier classification)
  - `feedback_brownfield-route-redirect-audit.md` (redirect cleanup)
  - `feedback_subagent-serial-only.md` (no parallel dispatch)
  - `session_2026-05-05_wave-d1-sessions-impl.md` (D.1 lessons)
- Pattern parents: PR #702 (Wave C.1), PR #711 (Wave C.2), PR #717 (Wave 4 D1), PR #724 (Wave 3 Step 2), PR #736 (Wave D.1)

---

**Status**: DRAFT — pending review before D.2 Foundation sub-PR dispatch.

**Next steps post-approval**:
1. Open child issue D.2 under umbrella #582 (route `/sessions/[id]/live`)
2. **Foundation sub-PR**: dispatch implementation subagent referencing this contract + checklist FSM cells (5 base + 5 connection + 3 dialog + 3 role = 16 cells) + 7 components (foundation scope) + Tier L test ratio 50/35/15 + static fixture (no SSE wiring)
3. After Foundation sub-PR ships → **Phase 3 retro mid-stream** (per #741 §10.4 timeline) capturing D.1 + D.2 foundation lessons
4. **Interactions sub-PR**: dispatch implementation subagent for real SSE wiring + 6 interactive components + dialogs (lazy-loaded) + write actions + 403 rollback
5. After Interactions sub-PR ships → consider Phase 3 retro doc finalization in advance of D.3
