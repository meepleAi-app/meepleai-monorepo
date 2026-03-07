/**
 * SmartFAB Context Configuration
 * Issues #6, #11, #12 from mobile-first-ux-epic.md
 *
 * Maps route patterns to context-aware primary actions for the SmartFAB.
 * Each route can also define secondary actions shown in a long-press QuickMenu.
 * Order matters: first match wins.
 */

import {
  BookOpen,
  Heart,
  MessageSquare,
  Play,
  Plus,
  Search,
  Sparkles,
  Star,
  Users,
  type LucideIcon,
} from 'lucide-react';

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
  /** Primary action configuration for this route */
  action: SmartFabAction;
  /** Secondary actions shown in long-press QuickMenu (max 3) */
  secondaryActions?: SmartFabAction[];
}

/**
 * Route-to-action mappings. Evaluated top-to-bottom, first match wins.
 * More specific patterns must come before general ones.
 */
export const SMART_FAB_ROUTES: SmartFabRoute[] = [
  {
    pattern: '/library/',
    action: { icon: Play, label: 'Avvia sessione', href: '/sessions/new' },
    secondaryActions: [
      { icon: MessageSquare, label: "Chiedi all'AI", href: '/chat/new' },
      { icon: Star, label: 'Preferiti', href: '/library/wishlist' },
    ],
  },
  {
    pattern: '/library',
    exact: true,
    action: { icon: Plus, label: 'Aggiungi gioco', href: '/games' },
    secondaryActions: [
      { icon: BookOpen, label: 'Giochi privati', href: '/library/private/add' },
      { icon: Heart, label: 'Wishlist', href: '/library/wishlist' },
    ],
  },
  {
    pattern: '/games/',
    action: { icon: Plus, label: 'Aggiungi alla libreria', href: '#add-to-library' },
    secondaryActions: [{ icon: MessageSquare, label: "Chiedi all'AI", href: '/chat/new' }],
  },
  {
    pattern: '/games',
    exact: true,
    action: { icon: Search, label: 'Cerca giochi', href: '#search' },
    secondaryActions: [{ icon: Plus, label: 'Aggiungi gioco', href: '/games/add' }],
  },
  {
    pattern: '/chat',
    action: { icon: Plus, label: 'Nuova chat', href: '/chat/new' },
  },
  {
    pattern: '/sessions',
    action: { icon: Plus, label: 'Nuova sessione', href: '/sessions/new' },
    secondaryActions: [{ icon: Users, label: 'Invita giocatori', href: '/players' }],
  },
  {
    pattern: '/dashboard',
    action: { icon: Sparkles, label: "Chiedi all'AI", href: '/chat/new' },
    secondaryActions: [
      { icon: Plus, label: 'Nuova sessione', href: '/sessions/new' },
      { icon: BookOpen, label: 'Libreria', href: '/library' },
    ],
  },
];

/** Default action when no route matches */
export const SMART_FAB_DEFAULT: SmartFabAction = {
  icon: MessageSquare,
  label: 'Chat',
  href: '/chat/new',
};

/** Default secondary actions when no route matches */
export const SMART_FAB_DEFAULT_SECONDARY: SmartFabAction[] = [];

export interface ResolvedSmartFab {
  primary: SmartFabAction;
  secondary: SmartFabAction[];
}

/**
 * Resolve the SmartFAB actions for a given pathname.
 */
export function resolveSmartFabAction(pathname: string): SmartFabAction {
  return resolveSmartFab(pathname).primary;
}

/**
 * Resolve primary + secondary actions for a given pathname.
 */
export function resolveSmartFab(pathname: string): ResolvedSmartFab {
  for (const route of SMART_FAB_ROUTES) {
    if (route.exact) {
      if (pathname === route.pattern) {
        return { primary: route.action, secondary: route.secondaryActions ?? [] };
      }
    } else {
      if (pathname.startsWith(route.pattern)) {
        return { primary: route.action, secondary: route.secondaryActions ?? [] };
      }
    }
  }
  return { primary: SMART_FAB_DEFAULT, secondary: SMART_FAB_DEFAULT_SECONDARY };
}
