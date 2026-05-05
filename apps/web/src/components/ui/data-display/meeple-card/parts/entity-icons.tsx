import {
  BookOpen,
  Bot,
  Briefcase,
  Calendar,
  Dices,
  MessageCircle,
  Swords,
  UserCircle2,
  Wrench,
  type LucideIcon,
} from 'lucide-react';

import type { MeepleEntityType } from '../types';

/**
 * Unified Lucide icon registry for all MeepleEntityType values.
 *
 * Use this instead of `tokens.entityIcon` (emoji, deprecated) or `nav-items/icons.tsx`
 * (semantic keys, legacy). Every entity has a single canonical Lucide component.
 */
export const entityIcons: Record<MeepleEntityType, LucideIcon> = {
  game: Dices,
  player: UserCircle2,
  session: Swords,
  agent: Bot,
  kb: BookOpen,
  chat: MessageCircle,
  event: Calendar,
  toolkit: Briefcase,
  tool: Wrench,
};

export const ENTITY_ICON_STROKE = 1.75;

export const ENTITY_ICON_SIZE = {
  sm: 14,
  md: 16,
} as const;
