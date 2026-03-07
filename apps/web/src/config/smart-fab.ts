/**
 * SmartFAB Context Configuration
 * Issue #6 from mobile-first-ux-epic.md
 *
 * Maps route patterns to context-aware primary actions for the SmartFAB.
 * Order matters: first match wins.
 */

import { MessageSquare, Plus, Play, Search, Sparkles, type LucideIcon } from 'lucide-react';

export interface SmartFabAction {
  /** Icon component to display */
  icon: LucideIcon;
  /** Accessible label describing the action */
  label: string;
  /** Navigation target or action identifier */
  href: string;
}

interface SmartFabRoute {
  /** Route pattern (prefix match) */
  pattern: string;
  /** Whether the pattern requires an exact match */
  exact?: boolean;
  /** Action configuration for this route */
  action: SmartFabAction;
}

/**
 * Route-to-action mappings. Evaluated top-to-bottom, first match wins.
 * More specific patterns must come before general ones.
 */
export const SMART_FAB_ROUTES: SmartFabRoute[] = [
  {
    pattern: '/library/',
    action: { icon: Play, label: 'Avvia sessione', href: '/sessions/new' },
  },
  {
    pattern: '/library',
    exact: true,
    action: { icon: Plus, label: 'Aggiungi gioco', href: '/games' },
  },
  {
    pattern: '/games/',
    action: { icon: Plus, label: 'Aggiungi alla libreria', href: '#add-to-library' },
  },
  {
    pattern: '/games',
    exact: true,
    action: { icon: Search, label: 'Cerca giochi', href: '#search' },
  },
  {
    pattern: '/chat',
    action: { icon: Plus, label: 'Nuova chat', href: '/chat/new' },
  },
  {
    pattern: '/sessions',
    action: { icon: Plus, label: 'Nuova sessione', href: '/sessions/new' },
  },
  {
    pattern: '/dashboard',
    action: { icon: Sparkles, label: "Chiedi all'AI", href: '/chat/new' },
  },
];

/** Default action when no route matches */
export const SMART_FAB_DEFAULT: SmartFabAction = {
  icon: MessageSquare,
  label: 'Chat',
  href: '/chat/new',
};

/**
 * Resolve the SmartFAB action for a given pathname.
 */
export function resolveSmartFabAction(pathname: string): SmartFabAction {
  for (const route of SMART_FAB_ROUTES) {
    if (route.exact) {
      if (pathname === route.pattern) return route.action;
    } else {
      if (pathname.startsWith(route.pattern)) return route.action;
    }
  }
  return SMART_FAB_DEFAULT;
}
