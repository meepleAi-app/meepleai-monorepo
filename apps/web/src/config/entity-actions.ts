/**
 * Entity Actions Configuration
 *
 * Defines reusable action card definitions for entity-specific UI,
 * such as the session mode dashboard quick actions and the bottom nav.
 */

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

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card-styles';

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
    drawCard: { entity: 'custom', href: '/dashboard' },
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
    drawCard: { entity: 'chatSession', href: '/chat' },
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
export const ENTITY_ACTIONS: Partial<Record<MeepleEntityType, BottomNavActionDef[]>> & {
  custom: BottomNavActionDef[];
} = {
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
  chatSession: [
    { id: 'chat', label: 'Chat', icon: MessageSquare, variant: 'primary' },
    { id: 'ask-ai', label: 'Chiedi AI', icon: Bot },
  ],
  custom: [{ id: 'home', label: 'Home', icon: Home, variant: 'ghost' }],
};
