/**
 * Entity Actions Configuration
 *
 * Defines reusable action card definitions for entity-specific UI,
 * such as the session mode dashboard quick actions.
 */

import { BookOpen, Bot, HelpCircle, Trophy } from 'lucide-react';

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
  variant?: 'primary' | 'default';
  /** Optional relative route suffix (e.g. '/rules' → /games/:id/rules) */
  route?: string;
}

/** Quick-action card definitions for session mode dashboard */
export const SESSION_QUICK_ACTIONS: BottomNavActionDef[] = [
  { id: 'rules', label: 'Regole', icon: BookOpen, variant: 'primary', route: '/rules' },
  { id: 'faqs', label: 'FAQ', icon: HelpCircle, route: '/faqs' },
  { id: 'ask-ai', label: 'Chiedi AI', icon: Bot, variant: 'primary' },
  { id: 'scores', label: 'Punteggi', icon: Trophy, route: '/scoreboard' },
];
