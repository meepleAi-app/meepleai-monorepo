/**
 * Toolkit Renderer Store — #2376 G5c.
 *
 * Zustand store managing parsed widget state + single-expanded accordion FSM
 * + optimistic config updates with backend PATCH + rollback on failure.
 *
 * @see docs/superpowers/specs/2026-06-16-issue-2376-g5c-toolkit-renderer-design.md §4.2
 */

import { create } from 'zustand';

import type { ParsedWidget, WidgetConfigByType, WidgetType } from '@/lib/session-live/widget-state';
import { parseWidgetConfig } from '@/lib/session-live/widget-state';

interface BackendToolkitWidget {
  readonly id: string;
  readonly type: string;
  readonly isEnabled: boolean;
  readonly displayOrder: number;
  readonly config: string;
}

interface BackendToolkitDashboard {
  readonly widgets: ReadonlyArray<BackendToolkitWidget>;
}

interface State {
  widgets: ReadonlyArray<ParsedWidget>;
  openWidgetId: string | null;
  hydrate: (toolkit: BackendToolkitDashboard) => void;
  setOpenWidget: (id: string | null) => void;
  updateWidgetConfig: <T extends WidgetType>(
    widgetId: string,
    nextConfig: WidgetConfigByType[T]
  ) => Promise<void>;
  reset: () => void;
}

export const useToolkitRendererStore = create<State>()((set, get) => ({
  widgets: [],
  openWidgetId: null,

  hydrate: toolkit => {
    const widgets = toolkit.widgets.map(w => ({
      id: w.id,
      type: w.type as WidgetType,
      isEnabled: w.isEnabled,
      displayOrder: w.displayOrder,
      config: parseWidgetConfig(w.type as WidgetType, w.config),
    })) as ParsedWidget[];
    set({ widgets });
  },

  setOpenWidget: id => set({ openWidgetId: id }),

  updateWidgetConfig: async (widgetId, nextConfig) => {
    const prev = get().widgets;
    // Optimistic update
    set({
      widgets: prev.map(w =>
        w.id === widgetId ? ({ ...w, config: nextConfig } as ParsedWidget) : w
      ),
    });

    try {
      const res = await fetch(`/api/v1/toolkits/widgets/${widgetId}`, {
        method: 'PATCH',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ configJson: JSON.stringify(nextConfig) }),
      });
      if (!res.ok) {
        throw new Error(`PATCH failed: ${res.status}`);
      }
    } catch (err) {
      console.warn('[useToolkitRendererStore] PATCH failed, rolling back:', err);
      set({ widgets: prev });
    }
  },

  reset: () => set({ widgets: [], openWidgetId: null }),
}));
