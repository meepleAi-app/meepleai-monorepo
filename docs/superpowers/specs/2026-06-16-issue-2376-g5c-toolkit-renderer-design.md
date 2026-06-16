# Issue #2376 G5c — ToolkitRenderer polymorphic dispatch (design)

**Date**: 2026-06-16
**Parent epic**: [#2354 Session live shell](https://github.com/meepleAi-app/meepleai-monorepo/issues/2354)
**Sub-issue**: [#2376](https://github.com/meepleAi-app/meepleai-monorepo/issues/2376)
**Effort estimate**: ~3-5gg

## 1. Context

G5c adds a polymorphic `ToolkitRenderer` switching on `WidgetType` over **6 variants**:

1. `RandomGenerator` — dice/coin/card draw.
2. `TurnManager` — turn order, phase progression.
3. `ScoreTracker` — multi-player score increments.
4. `ResourceManager` — resource counters (meeple, tokens).
5. `NoteManager` — per-player notes.
6. `Whiteboard` — collaborative canvas.

Backend `WidgetType` enum (`apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Enums/WidgetType.cs`) has all 6 variants. `ToolkitWidgetDto` carries `Type`, `IsEnabled`, `DisplayOrder`, `Config` (JSON string).

### Architectural boundary (ADR-079 candidate)

Per audit `claudedocs/2026-06-16-toolkit-vs-session-live-duplication-audit.md`:

- `apps/web/src/components/toolkit/` is the **AI-config domain** (admin UI consumers, `AiToolkitSuggestion` / `ToolkitDashboardDto` contracts, used by `app/(authenticated)/toolkit/[sessionId]/_content.tsx` and `/library/private/[id]/toolkit/configure/`).
- `apps/web/src/components/features/session-live/` is the **live-play runtime domain** (SSE-driven, used by `SessionLiveView`).

This spec builds **NEW** components under `features/session-live/toolkit-renderer/`, **NOT** in `toolkit/`. Existing `toolkit/ToolkitDashboard.tsx` stays untouched and serves the admin config UI.

## 2. Decisions locked

| ID | Decision | Rationale |
|---|---|---|
| DEC-1 | **Full implementation** — 6 widget components with state + interaction | User-locked. Aligns with mockup expectations. |
| DEC-2 | **Zustand store + backend PATCH** for widget state persistence | User-locked. Optimistic updates with `PATCH /api/v1/toolkits/widgets/{id}`. |
| DEC-3 | **Replace SessionToolsRail** in `SessionLiveView` RIGHT 'widget' tab | User-locked. Mockup canonical (`sp4-session-skeleton-renderers.jsx`) uses ToolkitRenderer in this slot. |
| DEC-4 | **Discriminated union** for widget config TS-side | Mirror #2378 G5b pattern. Backend Config is JSON string; FE parses into typed union via `parseWidgetConfig(type, json)`. |
| DEC-5 | **Single-expanded accordion** (mockup default) | Match mockup `sp4-session-skeleton-renderers.jsx` line 599-601 (`openId` state, click toggles single open widget). |
| DEC-6 | **Unknown widget type → warning banner + `console.warn`** | Mirror #2378 G5b Nygard pattern. |

## 3. Component architecture

```
features/session-live/toolkit-renderer/
  ├─ ToolkitRenderer.tsx                ← parent dispatcher (accordion FSM + widget list)
  ├─ labels.ts                          ← TS interface for i18n keys
  ├─ widgets/
  │   ├─ RandomGeneratorWidget.tsx      ← dice/coin/card draw
  │   ├─ TurnManagerWidget.tsx          ← turn cycle controls
  │   ├─ ScoreTrackerWidget.tsx         ← per-player +/- score
  │   ├─ ResourceManagerWidget.tsx      ← resource counters
  │   ├─ NoteManagerWidget.tsx          ← textarea per-player
  │   ├─ WhiteboardWidget.tsx           ← coloured stroke palette + dashed canvas (MVP)
  │   └─ UnknownWidget.tsx              ← warning banner
  ├─ internals/
  │   ├─ WidgetShell.tsx                ← header + icon + title + type pill + accordion button
  │   └─ Stepper.tsx                    ← +/- numeric input shared by Score/Resource
  └─ __tests__/
      └─ ToolkitRenderer.test.tsx       ← dispatcher tests
```

Each widget gets its own `__tests__/<Widget>.test.tsx` next to itself.

`data-slot="toolkit-renderer"` on `<ToolkitRenderer>` root. Each widget gets `data-slot="widget-{kebabCase(type)}"`.

## 4. State + persistence

### 4.1 Backend wire

Existing endpoints (verify in `apps/api/src/Api/BoundedContexts/GameToolkit/Routing/`):
- `GET /api/v1/toolkits/{toolkitId}/widgets` — load full `ToolkitDashboardDto`.
- `PATCH /api/v1/toolkits/widgets/{widgetId}` — update `IsEnabled` and/or `ConfigJson`.

### 4.2 Zustand store

```ts
// apps/web/src/lib/stores/toolkit-renderer-store.ts (new)

interface ToolkitRendererState {
  widgets: ReadonlyArray<ParsedWidget>;            // typed config union per type
  openWidgetId: string | null;                     // single-expanded accordion
  setOpenWidget: (id: string | null) => void;
  updateWidgetConfig: <T extends WidgetType>(
    id: string,
    nextConfig: WidgetConfigByType[T]
  ) => Promise<void>;                              // optimistic + PATCH
  hydrate: (toolkit: ToolkitDashboardDto) => void; // initial load from backend
}
```

The store keeps the parsed `WidgetConfigByType` union locally. On `updateWidgetConfig`:
1. Update local widget config in-place (optimistic).
2. POST `PATCH /api/v1/toolkits/widgets/{id}` with `ConfigJson = JSON.stringify(nextConfig)`.
3. On error: rollback to previous value + toast notification.

### 4.3 Config typing (DEC-4)

```ts
// apps/web/src/lib/session-live/widget-state.ts (new)

export type WidgetConfigByType = {
  RandomGenerator: { name: string; faces: ReadonlyArray<string | number>; quantity: number; last: string | number | null };
  TurnManager: { phaseBased: boolean; phases?: ReadonlyArray<string>; activeIndex: number };
  ScoreTracker: { scores: Record<string /* playerId */, number> };
  ResourceManager: { shared: boolean; resources: ReadonlyArray<{ label: string; value: number; max: number; danger?: boolean }> };
  NoteManager: { text: string };
  Whiteboard: { strokes: ReadonlyArray<{ tool: string; points: ReadonlyArray<[number, number]>; color: string }> };
};

export type ParsedWidget = {
  [K in WidgetType]: { id: string; type: K; isEnabled: boolean; displayOrder: number; config: WidgetConfigByType[K] };
}[WidgetType];

export function parseWidgetConfig<T extends WidgetType>(
  type: T,
  json: string
): WidgetConfigByType[T] {
  try {
    const parsed = JSON.parse(json) as Partial<WidgetConfigByType[T]>;
    return mergeWithDefaults(type, parsed);
  } catch {
    console.warn('[ToolkitRenderer] Failed to parse widget config:', type, json);
    return defaultConfigFor(type);
  }
}
```

`defaultConfigFor(type)` returns a safe empty config per variant.

## 5. Failure handling

- Unknown `widget.type` → `UnknownWidget` renders warning + `console.warn`. Dispatcher does not throw.
- Empty widgets list → empty-state component "Nessun widget abilitato".
- Failed JSON parse → fall back to `defaultConfigFor(type)`, log warning.
- Backend PATCH error → rollback optimistic update + toast notification (forward via `useToast`).
- SSR safety: store hydrates from `ToolkitDashboardDto` server-side; `'use client'` directive on every component.

## 6. A11y (axe AA)

- Accordion header is `<button aria-expanded>`.
- Widget body unmounts when collapsed (no hidden focus traps).
- Score steppers labeled with `aria-label="{playerName} score increment/decrement"`.
- Whiteboard tool palette labeled per tool.
- `aria-live="polite"` on widget body changes (SSE arrivals).

## 7. Testing strategy

- **Unit (Vitest)**: 1 test per widget × 6 + dispatch tests + Unknown fallback + JSON-parse failure + PATCH-error rollback = **~15 tests**.
- **Integration**: extend `SessionLiveView.test.tsx` to verify `tab='widget'` now mounts `<ToolkitRenderer>` (replaces `SessionToolsRail`).
- **Axe AA**: 8 tests (6 widgets + Unknown + empty state).
- **Zustand store**: 5 store unit tests (hydrate, setOpenWidget, updateWidgetConfig optimistic + rollback, JSON parse fallback).

## 8. i18n

~30 new keys under `pages.sessionLive.toolkitRenderer.*`:

- 6 widget headings + 6 widget type labels
- accordion expand/collapse aria-labels
- Unknown title/body
- empty state title/body
- per-widget action labels (rollDice, +/-, addStroke, etc.)

## 9. Acceptance criteria

- [x] Dispatch corretto per ogni `WidgetType` (6 variant) — Task 3 dispatcher.
- [x] Stato widget persistente per sessione (Zustand store + backend PATCH) — Task 5 store.
- [ ] axe AA 0 violations — Task 6.
- [ ] Unit test 1-per-widget — Tasks 2.x.

## 10. Out of scope

- Backend `WidgetType` enum changes (already complete).
- `toolkit/` directory refactor — separate cleanup issue per audit.
- Whiteboard interactive drawing (canvas event handlers, persistence) — MVP renders tool palette + dashed placeholder area; real drawing deferred to follow-up.
- ADR-079 boundary doc — optional follow-up (current audit doc sufficient for #2376).

## 11. Refs

- Epic: [#2354](https://github.com/meepleAi-app/meepleai-monorepo/issues/2354).
- Audit: `claudedocs/2026-06-16-toolkit-vs-session-live-duplication-audit.md`.
- Mockup: `admin-mockups/design_files/sp4-session-skeleton-renderers.jsx` lines 480-616.
- Backend enum: `apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Enums/WidgetType.cs`.
- Backend DTO: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/DTOs/ToolkitDashboardDtos.cs`.

🤖 Brainstormed via main-thread Q/A — 6 DEC user-locked.
