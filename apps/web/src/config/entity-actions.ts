// apps/web/src/config/entity-actions.ts
import {
  BookOpen,
  Compass,
  MessageSquare,
  ClipboardList,
  Gamepad2,
  Star,
  Bot,
  FileText,
  StickyNote,
  Trophy,
  StopCircle,
  Pencil,
  Database,
  RefreshCw,
  Mail,
  BarChart3,
  ArrowRight,
  Share2,
  Wrench,
  Link2,
  UserCheck,
  Eye,
} from 'lucide-react';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

import type { LucideIcon } from 'lucide-react';

export interface BottomNavActionDef {
  id: string;
  label: string;
  icon: LucideIcon;
  variant?: 'primary' | 'secondary' | 'ghost' | 'destructive';
  /** If set, this action draws a card instead of triggering onClick */
  drawCard?: { entity: MeepleEntityType; href: string };
  /** Route to navigate to (relative to current card's href) */
  route?: string;
}

/** Actions shown when a card of this entity type is focused */
export const ENTITY_ACTIONS: Record<MeepleEntityType, BottomNavActionDef[]> = {
  game: [
    { id: 'new-session', label: 'Nuova Sessione', icon: Gamepad2, variant: 'primary' },
    { id: 'wishlist', label: 'Wishlist', icon: Star },
    { id: 'chat-ai', label: 'Chat AI', icon: Bot },
    { id: 'upload-pdf', label: 'Carica PDF', icon: FileText },
  ],
  session: [
    { id: 'add-notes', label: 'Note', icon: StickyNote, variant: 'primary' },
    { id: 'score', label: 'Punteggi', icon: Trophy },
    { id: 'end-session', label: 'Termina', icon: StopCircle, variant: 'destructive' },
  ],
  agent: [
    { id: 'chat', label: 'Chat', icon: MessageSquare, variant: 'primary' },
    { id: 'edit', label: 'Modifica', icon: Pencil },
    { id: 'view-kb', label: 'KB Cards', icon: Database },
  ],
  kb: [
    { id: 'documents', label: 'Documenti', icon: FileText, variant: 'primary' },
    { id: 'reindex', label: 'Reindicizza', icon: RefreshCw },
    { id: 'edit', label: 'Modifica', icon: Pencil },
  ],
  player: [
    { id: 'sessions', label: 'Sessioni', icon: ClipboardList, variant: 'primary' },
    { id: 'invite', label: 'Invita', icon: Mail },
    { id: 'stats', label: 'Statistiche', icon: BarChart3 },
  ],
  chatSession: [
    { id: 'continue', label: 'Continua', icon: ArrowRight, variant: 'primary' },
    { id: 'export', label: 'Esporta', icon: FileText },
    { id: 'share', label: 'Condividi', icon: Share2 },
  ],
  toolkit: [
    { id: 'view-tools', label: 'Strumenti', icon: Wrench, variant: 'primary' },
    { id: 'link-games', label: 'Collega Giochi', icon: Link2 },
  ],
  tool: [
    { id: 'view-tool', label: 'Dettagli', icon: Eye, variant: 'primary' },
    { id: 'edit', label: 'Modifica', icon: Pencil },
  ],
  event: [
    { id: 'details', label: 'Dettagli', icon: Eye, variant: 'primary' },
    { id: 'rsvp', label: 'RSVP', icon: UserCheck },
    { id: 'share', label: 'Condividi', icon: Share2 },
  ],
  custom: [{ id: 'details', label: 'Dettagli', icon: Eye }],
};

/** Actions shown when no card is focused — "draw card" actions */
export const DEFAULT_ACTIONS: BottomNavActionDef[] = [
  {
    id: 'library',
    label: 'Library',
    icon: BookOpen,
    drawCard: { entity: 'game', href: '/library' },
  },
  {
    id: 'discover',
    label: 'Discover',
    icon: Compass,
    drawCard: { entity: 'game', href: '/discover' },
  },
  {
    id: 'chat',
    label: 'Chat',
    icon: MessageSquare,
    drawCard: { entity: 'chatSession', href: '/chat' },
  },
  {
    id: 'sessions',
    label: 'Sessions',
    icon: ClipboardList,
    drawCard: { entity: 'session', href: '/sessions' },
  },
];

/** Default pinned card definitions */
export const DEFAULT_PINNED_CARDS = DEFAULT_ACTIONS.filter(a => a.drawCard).map(a => ({
  id: `section-${a.id}`,
  entity: a.drawCard!.entity,
  title: a.label,
  href: a.drawCard!.href,
}));
