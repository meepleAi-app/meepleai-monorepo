/**
 * Entity Actions Configuration
 *
 * Defines reusable action card definitions for entity-specific UI,
 * such as the session mode dashboard quick actions and the bottom nav.
 */
/* eslint-disable @typescript-eslint/no-non-null-assertion -- pre-existing pattern: array/object access guarded by length/key check or by upstream validator; assertion is correct by construction. Cleanup tracked for follow-up audit. */

import {
  BookOpen,
  Bot,
  Gamepad2,
  HelpCircle,
  Home,
  Library,
  MessageSquare,
  Trophy,
  User,
} from 'lucide-react';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

/**
 * Local copy of HandCard shape — kept for PLACEHOLDER_ACTION_CARDS and
 * ALL_DEFAULT_CARDS until those are removed in the Task 5/6 cleanup.
 * @deprecated Will be removed when the card-hand UI is deleted.
 */
interface HandCard {
  id: string;
  entity: MeepleEntityType;
  title: string;
  href: string;
  subtitle?: string;
  imageUrl?: string;
  isPlaceholder?: boolean;
  placeholderAction?: string;
}

import type { LucideIcon } from 'lucide-react';

/**
 * A single action card shown in bottom-nav / quick-actions grids.
 */
export interface BottomNavActionDef {
  /** Unique identifier for the action */
  id: string;
  /** Display label */
  label: string;
  /** Lucide icon component */
  icon: LucideIcon;
  /** Optional visual variant */
  variant?: 'primary' | 'secondary' | 'ghost' | 'destructive' | 'default';
  /** Optional relative route suffix (e.g. '/rules' → /games/:id/rules) */
  route?: string;
  /**
   * If set, clicking this default action draws a card and navigates.
   * Used only for DEFAULT_ACTIONS.
   */
  drawCard?: {
    entity: MeepleEntityType;
    href: string;
  };
}

/** Quick-action card definitions for session mode dashboard */
export const SESSION_QUICK_ACTIONS: BottomNavActionDef[] = [
  { id: 'rules', label: 'Regole', icon: BookOpen, variant: 'primary', route: '/rules' },
  { id: 'faqs', label: 'FAQ', icon: HelpCircle, route: '/faqs' },
  { id: 'ask-ai', label: 'Chiedi AI', icon: Bot, variant: 'primary' },
  { id: 'scores', label: 'Punteggi', icon: Trophy, route: '/scoreboard' },
];

/**
 * Default nav actions shown when no card is focused in the hand.
 */
export const DEFAULT_ACTIONS: BottomNavActionDef[] = [
  {
    id: 'home',
    label: 'Home',
    icon: Home,
    variant: 'ghost',
    drawCard: { entity: 'game', href: '/library' },
  },
  {
    id: 'library',
    label: 'Libreria',
    icon: Library,
    variant: 'ghost',
    drawCard: { entity: 'game', href: '/library' },
  },
  {
    id: 'sessions',
    label: 'Sessioni',
    icon: Gamepad2,
    variant: 'ghost',
    drawCard: { entity: 'session', href: '/sessions' },
  },
  {
    id: 'chat',
    label: 'Chat',
    icon: MessageSquare,
    variant: 'ghost',
    drawCard: { entity: 'chat', href: '/chat' },
  },
  {
    id: 'profile',
    label: 'Profilo',
    icon: User,
    variant: 'ghost',
    drawCard: { entity: 'player', href: '/profile' },
  },
];

/**
 * Entity-specific actions shown when a card of that type is focused.
 * Each array is rendered as the bottom nav action set.
 */
export const ENTITY_ACTIONS: Partial<Record<MeepleEntityType, BottomNavActionDef[]>> = {
  game: [
    { id: 'rules', label: 'Regole', icon: BookOpen, variant: 'primary', route: '/rules' },
    { id: 'faqs', label: 'FAQ', icon: HelpCircle, route: '/faqs' },
    { id: 'ask-ai', label: 'Chiedi AI', icon: Bot, variant: 'primary' },
  ],
  session: [
    { id: 'rules', label: 'Regole', icon: BookOpen, variant: 'primary', route: '/rules' },
    { id: 'scores', label: 'Punteggi', icon: Trophy, route: '/scoreboard' },
    { id: 'ask-ai', label: 'Chiedi AI', icon: Bot, variant: 'primary' },
  ],
  chat: [
    { id: 'chat', label: 'Chat', icon: MessageSquare, variant: 'primary' },
    { id: 'ask-ai', label: 'Chiedi AI', icon: Bot },
  ],
};

/** Default pinned card definitions — auto-seeded on first load */
export const DEFAULT_PINNED_CARDS = DEFAULT_ACTIONS.filter(a => a.drawCard).map(a => ({
  id: `section-${a.id}`,
  entity: a.drawCard!.entity,
  title: a.label,
  href: a.drawCard!.href,
}));

/**
 * Placeholder action cards — special cards that open action sheets instead
 * of navigating to a page. They are protected from FIFO eviction.
 */
export const PLACEHOLDER_ACTION_CARDS: HandCard[] = [
  {
    id: 'action-search-agent',
    entity: 'agent',
    title: 'Cerca Agente',
    href: '#action-search-agent',
    isPlaceholder: true,
    placeholderAction: 'search-agent',
  },
  {
    id: 'action-search-game',
    entity: 'game',
    title: 'Cerca Gioco',
    href: '#action-search-game',
    isPlaceholder: true,
    placeholderAction: 'search-game',
  },
  {
    id: 'action-start-session',
    entity: 'session',
    title: 'Avvia Sessione',
    href: '#action-start-session',
    isPlaceholder: true,
    placeholderAction: 'start-session',
  },
  {
    id: 'action-toolkit',
    entity: 'toolkit',
    title: 'Toolkit',
    href: '#action-toolkit',
    isPlaceholder: true,
    placeholderAction: 'toolkit',
  },
];

/**
 * Combined default cards: pinned navigation cards + placeholder action cards.
 * Use this as the initial hand contents on first app load.
 */
export const ALL_DEFAULT_CARDS: HandCard[] = [...DEFAULT_PINNED_CARDS, ...PLACEHOLDER_ACTION_CARDS];
