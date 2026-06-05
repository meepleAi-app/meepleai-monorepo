import type { ComponentType } from 'react';

import {
  Bell,
  Bot,
  CalendarHeart,
  Dice5,
  History,
  LayoutDashboard,
  Library,
  UserCircle,
} from 'lucide-react';

/**
 * Main user navigation item. Mirrors the structure of `AdminNavItem`
 * (admin-nav/admin-nav-config.ts) but adds two MVP fields:
 *
 *   - `gameRelated`: marks the two game-domain entries (Library + Games)
 *     enforcing invariante #20 (the sidebar has exactly TWO game-related
 *     voices; Discover is a TAB of /games, not a sidebar item).
 *   - `showCounter`: marks items whose icon can display a numeric badge.
 *     T6 (SSE wire) will populate the actual count; MVP keeps it at 0.
 *
 * Future: `requiredPermission?: string` for per-item filtering.
 */
export interface MainNavItem {
  id: string;
  label: string;
  defaultHref: string;
  icon: ComponentType<{ className?: string }>;
  gameRelated?: boolean;
  showCounter?: boolean;
}

/**
 * Canonical main user navigation — 8 voci, exact order per CRIT-8 fix and
 * invariante #20 of the GameNight/Session domain model spec
 * (docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md).
 *
 * Invariante #20: Library (personale) + Games (catalogo, ?tab=discover come
 * landing default) sono le DUE voci game-related; Discover NON è una voce
 * autonoma del sidebar.
 */
export const MAIN_NAV_ITEMS: ReadonlyArray<MainNavItem> = [
  { id: 'dashboard', label: 'Dashboard', defaultHref: '/dashboard', icon: LayoutDashboard },
  { id: 'library', label: 'Library', defaultHref: '/library', icon: Library, gameRelated: true },
  {
    id: 'games',
    label: 'Games',
    defaultHref: '/games?tab=discover',
    icon: Dice5,
    gameRelated: true,
  },
  { id: 'gamenights', label: 'Game Nights', defaultHref: '/game-nights', icon: CalendarHeart },
  { id: 'sessions', label: 'Sessions', defaultHref: '/sessions', icon: History },
  { id: 'agents', label: 'Agents', defaultHref: '/agents', icon: Bot },
  {
    id: 'notifications',
    label: 'Notifications',
    defaultHref: '/notifications',
    icon: Bell,
    showCounter: true,
  },
  { id: 'profile', label: 'Profile', defaultHref: '/profile', icon: UserCircle },
] as const;
