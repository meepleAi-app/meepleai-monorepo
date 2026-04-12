'use client';

/**
 * Toolkit Timer Store — Module-level singleton per gameId
 *
 * The store and the `setInterval` live outside React's lifecycle so the
 * countdown continues even when the ToolkitDrawer is closed.
 * Callers (ToolkitDrawerInner, TimerTab) subscribe via Zustand selectors.
 *
 * Module-level registries:
 *  - intervalRegistry: stores the active setInterval ID per gameId
 *  - storeRegistry:    stores the Zustand store instance per gameId
 */

import { create } from 'zustand';

import type { StoreApi, UseBoundStore } from 'zustand';

// ============================================================================
// Types
// ============================================================================

export type TimerStatus = 'idle' | 'running' | 'paused' | 'ended';

export interface TimerStoreState {
  totalSeconds: number;
  remaining: number;
  status: TimerStatus;
  autoResetOnTurn: boolean;
}

export interface TimerStoreActions {
  start: () => void;
  pause: () => void;
  reset: () => void;
  setDuration: (seconds: number) => void;
  /** Add/subtract seconds from remaining (does NOT change totalSeconds). */
  adjustTime: (deltaSec: number) => void;
  setAutoResetOnTurn: (enabled: boolean) => void;
}

export type TimerStore = TimerStoreState & TimerStoreActions;

// ============================================================================
// Module-level registries (survive React unmounts)
// ============================================================================

const intervalRegistry = new Map<string, ReturnType<typeof setInterval>>();
const storeRegistry = new Map<string, UseBoundStore<StoreApi<TimerStore>>>();

function clearTimerInterval(gameId: string) {
  const id = intervalRegistry.get(gameId);
  if (id !== undefined) {
    clearInterval(id);
    intervalRegistry.delete(gameId);
  }
}

// ============================================================================
// Factory
// ============================================================================

/**
 * Returns (or lazily creates) the timer store for a given gameId.
 * Safe to call multiple times — always returns the same instance.
 */
export function getTimerStore(gameId: string): UseBoundStore<StoreApi<TimerStore>> {
  if (!storeRegistry.has(gameId)) {
    const store = create<TimerStore>()((set, get) => ({
      totalSeconds: 60,
      remaining: 60,
      status: 'idle',
      autoResetOnTurn: false,

      start() {
        const { status, remaining } = get();
        if (status === 'running' || remaining <= 0) return;

        set({ status: 'running' });
        clearTimerInterval(gameId); // guard against double-start

        const id = setInterval(() => {
          const { remaining: rem } = get();
          if (rem <= 1) {
            clearTimerInterval(gameId);
            set({ remaining: 0, status: 'ended' });
            if (typeof window !== 'undefined') {
              window.dispatchEvent(new CustomEvent('toolkit:timer-end', { detail: { gameId } }));
            }
          } else {
            set({ remaining: rem - 1 });
          }
        }, 1000);

        intervalRegistry.set(gameId, id);
      },

      pause() {
        if (get().status !== 'running') return;
        clearTimerInterval(gameId);
        set({ status: 'paused' });
      },

      reset() {
        clearTimerInterval(gameId);
        set(s => ({ remaining: s.totalSeconds, status: 'idle' }));
      },

      setDuration(seconds: number) {
        clearTimerInterval(gameId);
        // Clamp: 5 s – 99m59s
        const clamped = Math.max(5, Math.min(seconds, 5999));
        set({ totalSeconds: clamped, remaining: clamped, status: 'idle' });
      },

      adjustTime(deltaSec: number) {
        set(s => ({
          remaining: Math.max(0, Math.min(s.remaining + deltaSec, 5999)),
        }));
      },

      setAutoResetOnTurn(enabled: boolean) {
        set({ autoResetOnTurn: enabled });
      },
    }));

    storeRegistry.set(gameId, store);
  }

  return storeRegistry.get(gameId)!;
}
