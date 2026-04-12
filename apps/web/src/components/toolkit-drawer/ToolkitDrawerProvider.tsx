'use client';

/**
 * ToolkitDrawerProvider — React Context for the Toolkit Drawer
 *
 * Creates a per-game Zustand store and provides shared context
 * (tab state, event logging) to all toolkit tab components.
 */

import React, { createContext, useCallback, useContext, useRef, useState } from 'react';

import { createToolkitLocalStore, type ToolkitLocalStore } from '@/stores/toolkit-local-store';

import type { DiaryEvent, DiaryEventType, ToolkitTab } from './types';
import type { StoreApi, UseBoundStore } from 'zustand';

// ============================================================================
// Context Types
// ============================================================================

export interface ToolkitDrawerContextValue {
  gameId: string;
  sessionId: string | undefined;
  store: UseBoundStore<StoreApi<ToolkitLocalStore>>;
  /** Log a diary event to the local store (and POST to API when sessionId is present) */
  logEvent: (
    type: DiaryEventType,
    payload: Record<string, unknown>,
    extra?: { playerId?: string; playerName?: string; round?: number }
  ) => void;
  activeTab: ToolkitTab;
  setActiveTab: (tab: ToolkitTab) => void;
}

const ToolkitDrawerContext = createContext<ToolkitDrawerContextValue | null>(null);

// ============================================================================
// Provider
// ============================================================================

export interface ToolkitDrawerProviderProps {
  gameId: string;
  sessionId?: string;
  defaultTab?: ToolkitTab;
  children: React.ReactNode;
}

export function ToolkitDrawerProvider({
  gameId,
  sessionId,
  defaultTab = 'dice',
  children,
}: ToolkitDrawerProviderProps) {
  const storeRef = useRef<UseBoundStore<StoreApi<ToolkitLocalStore>> | null>(null);

  // Lazily create one store per gameId; recreate if gameId changes
  if (!storeRef.current || storeRef.current.getState().players !== undefined) {
    // Always create — the ref check ensures we only create once per mount
  }
  if (!storeRef.current) {
    storeRef.current = createToolkitLocalStore(gameId);
  }

  const [activeTab, setActiveTab] = useState<ToolkitTab>(defaultTab);

  const logEvent = useCallback(
    (
      type: DiaryEventType,
      payload: Record<string, unknown>,
      extra?: { playerId?: string; playerName?: string; round?: number }
    ) => {
      const event: DiaryEvent = {
        id: crypto.randomUUID(),
        type,
        timestamp: Date.now(),
        playerId: extra?.playerId,
        playerName: extra?.playerName,
        round: extra?.round,
        payload,
      };

      // Write to local store
      storeRef.current?.getState().logEvent(event);

      // If we have a sessionId, POST to the backend (fire-and-forget)
      if (sessionId) {
        // Lazy import to avoid circular deps and keep the provider lightweight.
        // The actual HTTP call is handled by the API client in the consuming layer.
        // For now we just dispatch a custom event that a parent component can listen to.
        window.dispatchEvent(
          new CustomEvent('toolkit:session-event', {
            detail: {
              sessionId,
              eventType: type,
              payloadJson: JSON.stringify(payload),
              participantId: extra?.playerId,
              roundNumber: extra?.round,
            },
          })
        );
      }
    },
    [sessionId]
  );

  const value: ToolkitDrawerContextValue = {
    gameId,
    sessionId,
    store: storeRef.current,
    logEvent,
    activeTab,
    setActiveTab,
  };

  return <ToolkitDrawerContext.Provider value={value}>{children}</ToolkitDrawerContext.Provider>;
}

// ============================================================================
// Hook
// ============================================================================

export function useToolkitDrawer(): ToolkitDrawerContextValue {
  const ctx = useContext(ToolkitDrawerContext);
  if (!ctx) {
    throw new Error('useToolkitDrawer must be used within a ToolkitDrawerProvider');
  }
  return ctx;
}
