# #2376 G5c ToolkitRenderer — Implementation Plan

> Spec: `docs/superpowers/specs/2026-06-16-issue-2376-g5c-toolkit-renderer-design.md`
> Branch: `feature/issue-2376-g5c-toolkit-renderer` (parent `main-dev`)
> 6 DEC: full impl · Zustand+PATCH · replace SessionToolsRail · discriminated union · single-expanded accordion · Unknown warning

## Task 1: widget-state.ts types

Create `apps/web/src/lib/session-live/widget-state.ts`:

```ts
'use client';

export type WidgetType =
  | 'RandomGenerator' | 'TurnManager' | 'ScoreTracker'
  | 'ResourceManager' | 'NoteManager' | 'Whiteboard';

export interface RandomGeneratorConfig {
  readonly name: string;
  readonly faces: ReadonlyArray<string | number>;
  readonly quantity: number;
  readonly last: string | number | null;
}
export interface TurnManagerConfig {
  readonly phaseBased: boolean;
  readonly phases?: ReadonlyArray<string>;
  readonly activeIndex: number;
}
export interface ScoreTrackerConfig {
  readonly scores: Readonly<Record<string, number>>;
}
export interface ResourceManagerConfig {
  readonly shared: boolean;
  readonly resources: ReadonlyArray<{
    readonly label: string; readonly value: number; readonly max: number; readonly danger?: boolean;
  }>;
}
export interface NoteManagerConfig { readonly text: string; }
export interface WhiteboardConfig {
  readonly strokes: ReadonlyArray<{
    readonly tool: string; readonly points: ReadonlyArray<[number, number]>; readonly color: string;
  }>;
}

export type WidgetConfigByType = {
  RandomGenerator: RandomGeneratorConfig;
  TurnManager: TurnManagerConfig;
  ScoreTracker: ScoreTrackerConfig;
  ResourceManager: ResourceManagerConfig;
  NoteManager: NoteManagerConfig;
  Whiteboard: WhiteboardConfig;
};

export type ParsedWidget = {
  [K in WidgetType]: {
    readonly id: string; readonly type: K; readonly isEnabled: boolean;
    readonly displayOrder: number; readonly config: WidgetConfigByType[K];
  };
}[WidgetType];

export function defaultConfigFor<T extends WidgetType>(type: T): WidgetConfigByType[T] {
  switch (type) {
    case 'RandomGenerator':
      return { name: 'Generatore', faces: [1,2,3,4,5,6], quantity: 1, last: null } as WidgetConfigByType[T];
    case 'TurnManager':
      return { phaseBased: false, activeIndex: 0 } as WidgetConfigByType[T];
    case 'ScoreTracker':
      return { scores: {} } as WidgetConfigByType[T];
    case 'ResourceManager':
      return { shared: false, resources: [] } as WidgetConfigByType[T];
    case 'NoteManager':
      return { text: '' } as WidgetConfigByType[T];
    case 'Whiteboard':
      return { strokes: [] } as WidgetConfigByType[T];
    default: { const _exhaustive: never = type; return _exhaustive; }
  }
}

export function parseWidgetConfig<T extends WidgetType>(type: T, json: string): WidgetConfigByType[T] {
  try {
    const parsed = JSON.parse(json) as Partial<WidgetConfigByType[T]>;
    return { ...defaultConfigFor(type), ...parsed };
  } catch (err) {
    console.warn('[ToolkitRenderer] parseWidgetConfig failed:', type, err);
    return defaultConfigFor(type);
  }
}
```

Commit: `feat(session-live): #2376 widget-state types + parser`

## Task 2: internals — WidgetShell + Stepper

Create `apps/web/src/components/features/session-live/toolkit-renderer/internals/WidgetShell.tsx`:

Accordion-style shell with header button, icon, title, type pill, body unmounts when collapsed (mirrors ChatAgentPanel §5 contract pattern from #2375).

Create `Stepper.tsx`: +/- buttons with aria-labels, controlled by `value` + `onChange`.

Commit: `feat(session-live): #2376 internals WidgetShell + Stepper`

## Task 3: 6 widget components

Each widget under `widgets/` consumes parsed config + emits config changes via `onChange`. Each has unit test in `__tests__/<Widget>.test.tsx`. Use mockup `sp4-session-skeleton-renderers.jsx` lines 501-562 as visual reference but use semantic Tailwind tokens (no raw HSL).

Per widget:
- RandomGeneratorWidget: "Last" display + "Genera" button (random pick from faces).
- TurnManagerWidget: ‹ Prev / [phase or '—'] / Next › buttons.
- ScoreTrackerWidget: Player rows with Stepper.
- ResourceManagerWidget: Resource rows with Stepper + max + danger flag.
- NoteManagerWidget: Textarea with 30s autosave via debounce.
- WhiteboardWidget: Tool palette + dashed placeholder canvas (MVP).
- UnknownWidget: Warning banner.

Commit per widget: `feat(session-live): #2376 <WidgetName>`.

## Task 4: ToolkitRenderer dispatcher

Create `ToolkitRenderer.tsx`:

```tsx
'use client';

import { useToolkitRendererStore } from '@/lib/stores/toolkit-renderer-store';

export function ToolkitRenderer({ labels, players }: ToolkitRendererProps): ReactElement {
  const { widgets, openWidgetId, setOpenWidget, updateWidgetConfig } = useToolkitRendererStore();
  const enabled = widgets.filter(w => w.isEnabled).sort((a, b) => a.displayOrder - b.displayOrder);
  if (enabled.length === 0) return <EmptyState labels={labels} />;
  return (
    <section data-slot="toolkit-renderer" role="region" aria-label={labels.title}>
      {enabled.map(w => {
        const collapsed = w.id !== openWidgetId;
        const onHeaderClick = () => setOpenWidget(collapsed ? w.id : null);
        switch (w.type) {
          case 'RandomGenerator':
            return <RandomGeneratorWidget key={w.id} widget={w} collapsed={collapsed}
              onHeaderClick={onHeaderClick} onConfigChange={c => updateWidgetConfig(w.id, c)}
              labels={labels} />;
          // ... (5 other branches)
          default:
            console.warn('[ToolkitRenderer] Unknown widget type:', w.type);
            return <UnknownWidget key={w.id} widget={w} labels={labels} />;
        }
      })}
    </section>
  );
}
```

10 unit tests covering each dispatch path + empty state + Unknown.

Commit: `feat(session-live): #2376 ToolkitRenderer dispatcher + 10 tests`

## Task 5: Zustand store with backend PATCH

Create `apps/web/src/lib/stores/toolkit-renderer-store.ts`:

```ts
import { create } from 'zustand';
import type { ParsedWidget, WidgetType, WidgetConfigByType } from '@/lib/session-live/widget-state';
import { parseWidgetConfig } from '@/lib/session-live/widget-state';

interface State {
  widgets: ReadonlyArray<ParsedWidget>;
  openWidgetId: string | null;
  setOpenWidget: (id: string | null) => void;
  hydrate: (toolkit: ToolkitDashboardDto) => void;
  updateWidgetConfig: <T extends WidgetType>(id: string, nextConfig: WidgetConfigByType[T]) => Promise<void>;
}

export const useToolkitRendererStore = create<State>()((set, get) => ({
  widgets: [],
  openWidgetId: null,
  setOpenWidget: id => set({ openWidgetId: id }),
  hydrate: toolkit => {
    const widgets = toolkit.widgets.map(w => ({
      id: w.id, type: w.type as WidgetType, isEnabled: w.isEnabled,
      displayOrder: w.displayOrder, config: parseWidgetConfig(w.type as WidgetType, w.config),
    })) as ParsedWidget[];
    set({ widgets });
  },
  updateWidgetConfig: async (id, nextConfig) => {
    const prev = get().widgets;
    set({ widgets: prev.map(w => w.id === id ? { ...w, config: nextConfig } as ParsedWidget : w) });
    try {
      const res = await fetch(`/api/v1/toolkits/widgets/${id}`, {
        method: 'PATCH', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ configJson: JSON.stringify(nextConfig) }),
      });
      if (!res.ok) throw new Error(`PATCH failed: ${res.status}`);
    } catch (err) {
      console.warn('[useToolkitRendererStore] PATCH failed, rolling back:', err);
      set({ widgets: prev });
    }
  },
}));
```

5 store unit tests: hydrate, setOpenWidget, updateWidgetConfig optimistic, rollback on failure, parseWidgetConfig fallback.

Commit: `feat(session-live): #2376 useToolkitRendererStore + 5 tests`

## Task 6: i18n keys (~30 keys)

Add to `it.json` and `en.json` under `pages.sessionLive.toolkitRenderer`:

```json
{
  "title": "Strumenti",
  "emptyTitle": "Nessun widget attivo",
  "emptyBody": "Abilita i widget dal toolkit.",
  "unknownTitle": "Widget non supportato",
  "unknownBody": "Aggiorna l'app.",
  "expandAriaTemplate": "Espandi widget {name}",
  "collapseAriaTemplate": "Collassa widget {name}",
  "randomGenerator": { "heading": "Generatore", "rollLabel": "Genera", "lastLabel": "Ultimo" },
  "turnManager": { "heading": "Gestore turni", "prevLabel": "Precedente", "nextLabel": "Successivo", "turnOfLabel": "Turno di", "phaseLabel": "Fase" },
  "scoreTracker": { "heading": "Punteggio", "incrementAriaTemplate": "Aumenta punteggio {name}", "decrementAriaTemplate": "Diminuisci punteggio {name}" },
  "resourceManager": { "heading": "Risorse", "sharedHeading": "Risorse condivise", "incrementAriaTemplate": "Aumenta {label}", "decrementAriaTemplate": "Diminuisci {label}" },
  "noteManager": { "heading": "Note", "inputAriaLabel": "Scrivi una nota", "savingLabel": "Salvataggio…", "savedLabel": "Salvato" },
  "whiteboard": { "heading": "Lavagna", "toolPenLabel": "Penna", "toolEraserLabel": "Gomma", "toolCircleLabel": "Cerchio", "placeholderLabel": "Area disegno condivisa" }
}
```

Mirror en.json with English translations.

Commit: `feat(i18n): #2376 toolkitRenderer ~30 new keys`

## Task 7: SessionLiveView swap SessionToolsRail → ToolkitRenderer

Wire `<ToolkitRenderer>` in `desktopRightColumn` 'widget' tab + `mobileSheetContent` 'widget' case. Remove `<SessionToolsRail>` from those slots (keep import only if other consumers).

Add hook to load `ToolkitDashboardDto` (use existing `useToolkit` query if available, else `useQuery({ queryKey: ['toolkit', sessionId], queryFn: …, enabled: !!sessionId })`).

Call `hydrate(toolkit)` once loaded.

For mobile use compact prop.

Commit: `feat(session-live): #2376 wire SessionLiveView to ToolkitRenderer`

## Task 8: axe AA test

Create `apps/web/__tests__/session-live-toolkit-renderer-axe.test.tsx` covering 8 states (6 widget types isolated + Unknown + empty).

Commit: `test(a11y): #2376 ToolkitRenderer axe AA 8 tests`

## Task 9: Final verify + PR

```bash
cd apps/web && pnpm typecheck && pnpm test session-live --run && pnpm lint
git push -u origin feature/issue-2376-g5c-toolkit-renderer
gh pr create --base main-dev --title "feat(session-live): #2376 G5c ToolkitRenderer polymorphic dispatch (6 widget)" --body "..."
```

Commit: none (PR creation only).

---

## Execution

Subagent-driven; dispatch tasks sequentially. Skip per-task code review (rely on TDD self-test + final review). Final comprehensive review before merge.
