'use client';

import { useEffect, useRef } from 'react';

import { useMiniNavConfigStore, type MiniNavConfig } from '@/lib/stores/mini-nav-config-store';

/**
 * Pages call this hook to register their mini-nav config with the global shell.
 * The shell reads the store and renders MiniNavSlot automatically.
 * Config is cleared on unmount (so navigating away hides the mini-nav).
 *
 * Shallow structural equality on the config's observable fields prevents
 * re-registration loops when consumers pass inline object literals.
 * (Carry-forward fix M3 from Phase 1 code review.)
 */
export function useMiniNavConfig(config: MiniNavConfig): void {
  const setConfig = useMiniNavConfigStore(s => s.setConfig);
  const clear = useMiniNavConfigStore(s => s.clear);
  const previousKeyRef = useRef<string | null>(null);

  // Build a stable structural key from the config's observable fields.
  // `onClick` is excluded because function refs change between renders but
  // don't carry meaning independent of the label/icon.
  const key = JSON.stringify({
    breadcrumb: config.breadcrumb,
    activeTabId: config.activeTabId,
    tabs: config.tabs,
    primaryActionLabel: config.primaryAction?.label ?? null,
    primaryActionIcon: config.primaryAction?.icon ?? null,
  });

  useEffect(() => {
    if (previousKeyRef.current === key) return;
    previousKeyRef.current = key;
    setConfig(config);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, setConfig]);

  // Separate effect for unmount cleanup — runs once, clears on teardown.
  useEffect(() => {
    return () => {
      clear();
      previousKeyRef.current = null;
    };
  }, [clear]);
}
