/**
 * useRouteContext Hook
 * Issue #3479 - Layout System v2: Route-based Context Detection
 *
 * Automatically determines the LayoutContext based on the current route.
 * Updates the layout context when the route changes.
 */

'use client';

import { useEffect, useMemo } from 'react';

import { usePathname } from 'next/navigation';

import { useLayout } from '@/components/layout/LayoutProvider';
import type { LayoutContext } from '@/types/layout';

/**
 * Route pattern configuration for context detection.
 * Order matters - more specific patterns should come first.
 */
interface RouteContextPattern {
  /** RegExp pattern to match the route */
  pattern: RegExp;
  /** Context to apply when pattern matches */
  context: LayoutContext;
  /** Optional condition function for dynamic context */
  condition?: (pathname: string, params?: Record<string, string>) => boolean;
}

/**
 * Route patterns mapped to layout contexts.
 * More specific patterns should be listed before general ones.
 */
const ROUTE_CONTEXT_PATTERNS: RouteContextPattern[] = [
  // Session routes
  { pattern: /^\/session\/[^/]+\/setup$/, context: 'session_setup' },
  { pattern: /^\/session\/[^/]+\/end$/, context: 'session_end' },
  { pattern: /^\/session\/[^/]+$/, context: 'session_active' },

  // Library routes
  { pattern: /^\/library\/[^/]+\/documents/, context: 'document_viewer' },
  { pattern: /^\/library\/games\/[^/]+$/, context: 'game_detail' },
  { pattern: /^\/library$/, context: 'library' },

  // Game/Catalog routes
  { pattern: /^\/games\/[^/]+$/, context: 'game_detail_not_owned' },
  { pattern: /^\/games$/, context: 'catalog' },

  // Chat routes
  { pattern: /^\/chat/, context: 'chat' },

  // User routes
  { pattern: /^\/wishlist/, context: 'wishlist' },
  { pattern: /^\/notifications/, context: 'notifications' },
  { pattern: /^\/profile/, context: 'profile' },
  { pattern: /^\/settings/, context: 'settings' },

  // Search
  { pattern: /^\/search/, context: 'search' },

  // Dashboard (home)
  { pattern: /^\/dashboard$/, context: 'dashboard' },
  { pattern: /^\/$/, context: 'dashboard' },

  // Default fallback
  { pattern: /.*/, context: 'default' },
];

/**
 * Determines the layout context based on the current pathname.
 */
function getContextFromPathname(pathname: string): LayoutContext {
  for (const { pattern, context, condition } of ROUTE_CONTEXT_PATTERNS) {
    if (pattern.test(pathname)) {
      // If there's a condition, check it
      if (condition && !condition(pathname)) {
        continue;
      }
      return context;
    }
  }
  return 'default';
}

/**
 * Options for useRouteContext hook.
 */
export interface UseRouteContextOptions {
  /** Override the automatically detected context */
  overrideContext?: LayoutContext;
  /** Disable automatic context updates */
  disabled?: boolean;
}

/**
 * Hook that automatically sets the layout context based on the current route.
 *
 * @param options - Configuration options
 * @returns The current layout context
 *
 * @example
 * ```tsx
 * // Basic usage - auto-detects context from route
 * function MyPage() {
 *   const context = useRouteContext();
 *   // Context is automatically set based on pathname
 * }
 *
 * // Override context
 * function GameDetailPage({ isOwned }) {
 *   const context = useRouteContext({
 *     overrideContext: isOwned ? 'game_detail' : 'game_detail_not_owned'
 *   });
 * }
 * ```
 */
export function useRouteContext(options: UseRouteContextOptions = {}): LayoutContext {
  const { overrideContext, disabled = false } = options;
  const pathname = usePathname();
  const { context, setContext } = useLayout();

  // Calculate the context from pathname
  const detectedContext = useMemo(() => {
    if (overrideContext) {
      return overrideContext;
    }
    return getContextFromPathname(pathname ?? '');
  }, [pathname, overrideContext]);

  // Update the layout context when it changes
  useEffect(() => {
    if (disabled) {
      return;
    }

    if (detectedContext !== context) {
      setContext(detectedContext);
    }
  }, [detectedContext, context, setContext, disabled]);

  return detectedContext;
}

/**
 * Hook to get the context for a given pathname without updating the layout state.
 * Useful for preview or comparison purposes.
 *
 * @param pathname - The pathname to check
 * @returns The layout context for that pathname
 */
export function useContextForPath(pathname: string): LayoutContext {
  return useMemo(() => getContextFromPathname(pathname), [pathname]);
}

/**
 * Export the context detection function for use outside of React components.
 */
export { getContextFromPathname };
