/**
 * useToolkitEditor — Admin toolkit CRUD management (Issue #4978).
 *
 * Manages create/update/publish operations for a GameToolkit and its child tools.
 * Fetches the first (unpublished or published) toolkit for the given gameId.
 *
 * API surface:
 *  GET    /api/v1/game-toolkits/by-game/{gameId}         — fetch
 *  POST   /api/v1/game-toolkits/                          — create
 *  PUT    /api/v1/game-toolkits/{id}                      — update overrides/name
 *  POST   /api/v1/game-toolkits/{id}/publish              — publish
 *  POST   /api/v1/game-toolkits/{id}/{type}-tools         — add tool
 *  DELETE /api/v1/game-toolkits/{id}/{type}-tools/{name}  — remove tool
 */

import { useCallback, useEffect, useState } from 'react';

import type {
  CardToolDto,
  CounterToolDto,
  DiceToolDto,
  GameToolkitDto,
  TimerToolDto,
} from '../types/gameToolkit';

// ── Types ─────────────────────────────────────────────────────────────────────

export interface UpdateOverridesPayload {
  name?: string;
  overridesTurnOrder?: boolean;
  overridesScoreboard?: boolean;
  overridesDiceSet?: boolean;
}

export interface UseToolkitEditorResult {
  toolkit: GameToolkitDto | null;
  isLoading: boolean;
  isSaving: boolean;
  error: string | null;
  // Actions
  createToolkit: (name: string) => Promise<void>;
  updateOverrides: (payload: UpdateOverridesPayload) => Promise<void>;
  addDiceTool: (tool: DiceToolDto) => Promise<void>;
  removeDiceTool: (name: string) => Promise<void>;
  addCardTool: (tool: CardToolDto) => Promise<void>;
  removeCardTool: (name: string) => Promise<void>;
  addTimerTool: (tool: TimerToolDto) => Promise<void>;
  removeTimerTool: (name: string) => Promise<void>;
  addCounterTool: (tool: CounterToolDto) => Promise<void>;
  removeCounterTool: (name: string) => Promise<void>;
  publish: () => Promise<void>;
}

// ── Hook ──────────────────────────────────────────────────────────────────────

function getBaseUrl(): string {
  return process.env.NEXT_PUBLIC_API_BASE ?? '';
}

async function apiFetch<T>(url: string, options: RequestInit = {}): Promise<T> {
  const res = await fetch(url, {
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...options.headers },
    ...options,
  });
  if (!res.ok) {
    throw new Error(`HTTP ${res.status}: ${res.statusText}`);
  }
  return res.json() as Promise<T>;
}

/**
 * @param gameId - Shared-catalog game ID to load/manage the toolkit for.
 */
export function useToolkitEditor(gameId: string): UseToolkitEditorResult {
  const [toolkit, setToolkit] = useState<GameToolkitDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // ── Initial fetch ──────────────────────────────────────────────────────────

  useEffect(() => {
    let cancelled = false;
    setIsLoading(true);
    setError(null);

    const base = getBaseUrl();
    fetch(`${base}/api/v1/game-toolkits/by-game/${gameId}`, {
      credentials: 'include',
    })
      .then(res => {
        if (!res.ok) {
          if (res.status === 404) return [] as GameToolkitDto[];
          throw new Error(`HTTP ${res.status}: ${res.statusText}`);
        }
        return res.json() as Promise<GameToolkitDto[]>;
      })
      .then((toolkits: GameToolkitDto[]) => {
        if (cancelled) return;
        // Prefer the latest (first) toolkit regardless of published state
        setToolkit(toolkits[0] ?? null);
        setIsLoading(false);
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setError(err instanceof Error ? err.message : 'Failed to load toolkit');
        setIsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [gameId]);

  // ── Helpers ───────────────────────────────────────────────────────────────

  const withSaving = useCallback(async (fn: () => Promise<GameToolkitDto>) => {
    setIsSaving(true);
    setError(null);
    try {
      const updated = await fn();
      setToolkit(updated);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Operation failed';
      setError(msg);
      throw err;
    } finally {
      setIsSaving(false);
    }
  }, []);

  // ── Actions ───────────────────────────────────────────────────────────────

  const createToolkit = useCallback(
    async (name: string) => {
      await withSaving(() =>
        apiFetch<GameToolkitDto>(`${getBaseUrl()}/api/v1/game-toolkits/`, {
          method: 'POST',
          body: JSON.stringify({
            gameId,
            name,
            overridesTurnOrder: false,
            overridesScoreboard: false,
            overridesDiceSet: false,
          }),
        })
      );
    },
    [gameId, withSaving]
  );

  const updateOverrides = useCallback(
    async (payload: UpdateOverridesPayload) => {
      if (!toolkit) throw new Error('No toolkit loaded');
      await withSaving(() =>
        apiFetch<GameToolkitDto>(`${getBaseUrl()}/api/v1/game-toolkits/${toolkit.id}`, {
          method: 'PUT',
          body: JSON.stringify({
            name: payload.name ?? toolkit.name,
            overridesTurnOrder: payload.overridesTurnOrder ?? toolkit.overridesTurnOrder,
            overridesScoreboard: payload.overridesScoreboard ?? toolkit.overridesScoreboard,
            overridesDiceSet: payload.overridesDiceSet ?? toolkit.overridesDiceSet,
          }),
        })
      );
    },
    [toolkit, withSaving]
  );

  const addDiceTool = useCallback(
    async (tool: DiceToolDto) => {
      if (!toolkit) throw new Error('No toolkit loaded');
      await withSaving(() =>
        apiFetch<GameToolkitDto>(`${getBaseUrl()}/api/v1/game-toolkits/${toolkit.id}/dice-tools`, {
          method: 'POST',
          body: JSON.stringify(tool),
        })
      );
    },
    [toolkit, withSaving]
  );

  const removeDiceTool = useCallback(
    async (name: string) => {
      if (!toolkit) throw new Error('No toolkit loaded');
      await withSaving(() =>
        apiFetch<GameToolkitDto>(
          `${getBaseUrl()}/api/v1/game-toolkits/${toolkit.id}/dice-tools/${encodeURIComponent(name)}`,
          { method: 'DELETE' }
        )
      );
    },
    [toolkit, withSaving]
  );

  const addCardTool = useCallback(
    async (tool: CardToolDto) => {
      if (!toolkit) throw new Error('No toolkit loaded');
      await withSaving(() =>
        apiFetch<GameToolkitDto>(`${getBaseUrl()}/api/v1/game-toolkits/${toolkit.id}/card-tools`, {
          method: 'POST',
          body: JSON.stringify(tool),
        })
      );
    },
    [toolkit, withSaving]
  );

  const removeCardTool = useCallback(
    async (name: string) => {
      if (!toolkit) throw new Error('No toolkit loaded');
      await withSaving(() =>
        apiFetch<GameToolkitDto>(
          `${getBaseUrl()}/api/v1/game-toolkits/${toolkit.id}/card-tools/${encodeURIComponent(name)}`,
          { method: 'DELETE' }
        )
      );
    },
    [toolkit, withSaving]
  );

  const addTimerTool = useCallback(
    async (tool: TimerToolDto) => {
      if (!toolkit) throw new Error('No toolkit loaded');
      await withSaving(() =>
        apiFetch<GameToolkitDto>(`${getBaseUrl()}/api/v1/game-toolkits/${toolkit.id}/timer-tools`, {
          method: 'POST',
          body: JSON.stringify(tool),
        })
      );
    },
    [toolkit, withSaving]
  );

  const removeTimerTool = useCallback(
    async (name: string) => {
      if (!toolkit) throw new Error('No toolkit loaded');
      await withSaving(() =>
        apiFetch<GameToolkitDto>(
          `${getBaseUrl()}/api/v1/game-toolkits/${toolkit.id}/timer-tools/${encodeURIComponent(name)}`,
          { method: 'DELETE' }
        )
      );
    },
    [toolkit, withSaving]
  );

  const addCounterTool = useCallback(
    async (tool: CounterToolDto) => {
      if (!toolkit) throw new Error('No toolkit loaded');
      await withSaving(() =>
        apiFetch<GameToolkitDto>(
          `${getBaseUrl()}/api/v1/game-toolkits/${toolkit.id}/counter-tools`,
          { method: 'POST', body: JSON.stringify(tool) }
        )
      );
    },
    [toolkit, withSaving]
  );

  const removeCounterTool = useCallback(
    async (name: string) => {
      if (!toolkit) throw new Error('No toolkit loaded');
      await withSaving(() =>
        apiFetch<GameToolkitDto>(
          `${getBaseUrl()}/api/v1/game-toolkits/${toolkit.id}/counter-tools/${encodeURIComponent(name)}`,
          { method: 'DELETE' }
        )
      );
    },
    [toolkit, withSaving]
  );

  const publish = useCallback(async () => {
    if (!toolkit) throw new Error('No toolkit loaded');
    await withSaving(() =>
      apiFetch<GameToolkitDto>(`${getBaseUrl()}/api/v1/game-toolkits/${toolkit.id}/publish`, {
        method: 'POST',
      })
    );
  }, [toolkit, withSaving]);

  return {
    toolkit,
    isLoading,
    isSaving,
    error,
    createToolkit,
    updateOverrides,
    addDiceTool,
    removeDiceTool,
    addCardTool,
    removeCardTool,
    addTimerTool,
    removeTimerTool,
    addCounterTool,
    removeCounterTool,
    publish,
  };
}
