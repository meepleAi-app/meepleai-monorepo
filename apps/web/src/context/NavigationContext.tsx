'use client';

/**
 * NavigationContext — 3-tier Navigation Config System
 * Issue #5034 - Navigation Config System
 *
 * Allows pages/layouts to declare their MiniNav (L2) tabs
 * and ActionBar (L3) actions declaratively via useSetNavConfig().
 *
 * Usage:
 *   // In root layout: wrap with <NavigationProvider>
 *   // In page layout: useSetNavConfig({ miniNav: [...], actionBar: [...] })
 *   // In components: const { miniNavTabs, actionBarActions } = useNavigation()
 */

import { createContext, useContext, useCallback, useState, type ReactNode } from 'react';

import { logger } from '@/lib/logger';
import type { NavAction, NavTab, NavigationContextValue, PageNavConfig } from '@/types/navigation';

// ─── Default Value ────────────────────────────────────────────────────────────

const DEFAULT_VALUE: NavigationContextValue = {
  miniNavTabs: [],
  actionBarActions: [],
  activeZone: null,
  setNavConfig: () => {},
  clearNavConfig: () => {},
};

// ─── Context ──────────────────────────────────────────────────────────────────

const NavigationContext = createContext<NavigationContextValue>(DEFAULT_VALUE);

NavigationContext.displayName = 'NavigationContext';

// ─── Provider ─────────────────────────────────────────────────────────────────

export interface NavigationProviderProps {
  children: ReactNode;
}

/**
 * NavigationProvider
 *
 * Wrap the root layout (or authenticated shell) with this provider.
 * Pages set their nav config via useSetNavConfig() and components
 * consume it via useNavigation().
 */
export function NavigationProvider({ children }: NavigationProviderProps) {
  const [miniNavTabs, setMiniNavTabs] = useState<NavTab[]>([]);
  const [actionBarActions, setActionBarActions] = useState<NavAction[]>([]);
  const [activeZone, setActiveZone] = useState<string | null>(null);

  const setNavConfig = useCallback((config: PageNavConfig) => {
    setMiniNavTabs(config.miniNav ?? []);
    setActionBarActions(config.actionBar ?? []);
    setActiveZone(config.zone ?? null);
  }, []);

  const clearNavConfig = useCallback(() => {
    setMiniNavTabs([]);
    setActionBarActions([]);
    setActiveZone(null);
  }, []);

  const value: NavigationContextValue = {
    miniNavTabs,
    actionBarActions,
    activeZone,
    setNavConfig,
    clearNavConfig,
  };

  return <NavigationContext.Provider value={value}>{children}</NavigationContext.Provider>;
}

// ─── Hooks ────────────────────────────────────────────────────────────────────

/**
 * useNavigation
 *
 * Consume the current MiniNav tabs and ActionBar actions set by the active page.
 * Must be used inside <NavigationProvider>.
 */
export function useNavigation(): NavigationContextValue {
  const ctx = useContext(NavigationContext);
  if (ctx === DEFAULT_VALUE && process.env.NODE_ENV === 'development') {
    logger.warn(
      '[useNavigation] Used outside <NavigationProvider>. ' +
        'Make sure <NavigationProvider> wraps the layout.'
    );
  }
  return ctx;
}

/**
 * useSetNavConfig
 *
 * Hook for pages/layouts to declare their MiniNav + ActionBar configuration.
 * Call this inside a layout.tsx or page.tsx to configure the nav for that section.
 *
 * The config is applied lazily via a returned setter — call it in a useEffect
 * or directly on user interactions.
 *
 * Example:
 *   export default function LibraryLayout({ children }) {
 *     const setNavConfig = useSetNavConfig();
 *     useEffect(() => {
 *       setNavConfig({
 *         miniNav: [
 *           { id: 'games', label: 'Games', href: '/library' },
 *           { id: 'wishlist', label: 'Wishlist', href: '/library?tab=wishlist' },
 *         ],
 *         actionBar: [
 *           { id: 'add-game', label: 'Add Game', icon: PlusIcon, onClick: handleAdd, variant: 'primary' },
 *         ],
 *       });
 *     }, []);
 *     return <>{children}</>;
 *   }
 */
export function useSetNavConfig(): (config: PageNavConfig) => void {
  const { setNavConfig } = useContext(NavigationContext);
  return setNavConfig;
}
