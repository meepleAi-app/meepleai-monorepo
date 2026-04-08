'use client';

import { useEffect } from 'react';

import { useMiniNavConfigStore, type MiniNavConfig } from '@/lib/stores/mini-nav-config-store';

/**
 * Pages call this hook to register their mini-nav config with the global shell.
 * The shell reads the store and renders MiniNavSlot automatically.
 * Config is cleared on unmount (so navigating away hides the mini-nav).
 */
export function useMiniNavConfig(config: MiniNavConfig): void {
  const setConfig = useMiniNavConfigStore(s => s.setConfig);
  const clear = useMiniNavConfigStore(s => s.clear);

  useEffect(() => {
    setConfig(config);
    return () => clear();
  }, [config, setConfig, clear]);
}
