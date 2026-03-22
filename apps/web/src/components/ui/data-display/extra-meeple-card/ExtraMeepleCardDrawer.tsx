'use client';

/**
 * ExtraMeepleCardDrawer — Sheet wrapper for entity detail cards
 * Issue #5024 - ExtraMeepleCard Drawer System (Epic #5023)
 *
 * A Sheet (drawer) that renders the appropriate ExtraMeepleCard variant
 * based on entity type. Each entity content component handles its own
 * data fetching by entityId.
 *
 * Routing:
 *   game        → GameDrawerContent    (GameExtraMeepleCard)
 *   agent       → AgentDrawerContent   (AgentExtraMeepleCard — Issue #5026)
 *   chat/chatSession → ChatDrawerContent (ChatExtraMeepleCard — Issue #5027)
 *   kb          → KbDrawerContent      (KbExtraMeepleCard — Issue #5028)
 *   others      → DrawerComingSoon     (placeholder for future implementation)
 */

import React from 'react';

import {
  Award,
  Bot,
  Calendar,
  FileText,
  Gamepad2,
  Layers,
  Link as LinkIcon,
  Map,
  MessageCircle,
  PackagePlus,
  StickyNote,
  ToolCase,
  Trophy,
  User,
  Users,
  Wrench,
  X,
  Zap,
} from 'lucide-react';

import { EmbeddedChatView } from '@/components/chat-unified/EmbeddedChatView';
import { useDashboardMode } from '@/components/dashboard';
import { Sheet, SheetClose, SheetContent, SheetTitle } from '@/components/ui/navigation/sheet';
import { cn } from '@/lib/utils';

import { DrawerLoadingSkeleton, DrawerErrorState, DrawerComingSoon } from './drawer-states';
import { DRAWER_TEST_IDS } from './drawer-test-ids';
import {
  GameExtraMeepleCard,
  AgentExtraMeepleCard,
  ChatExtraMeepleCard,
  KbExtraMeepleCard,
} from './EntityExtraMeepleCard';
import { useGameDetail, useAgentDetail, useChatDetail, useKbDetail } from './hooks';
import { RelatedEntitiesSection } from '../entity-link/related-entities-section';

import type { LinkEntityType } from '../entity-link/entity-link-types';
import type { MeepleEntityType } from '../meeple-card-styles';

// ============================================================================
// Types
// ============================================================================

/**
 * All supported drawer entity types.
 * Includes all MeepleEntityType values plus:
 *   - 'chat'  — legacy alias for 'chatSession' (backward compat)
 *   - 'links' — shows the RelatedEntitiesSection
 */
export type DrawerEntityType = MeepleEntityType | 'chat' | 'links';

export interface ExtraMeepleCardDrawerProps {
  /** Type of entity to display */
  entityType: DrawerEntityType;
  /** ID of the entity to load */
  entityId: string;
  /** Controls drawer visibility */
  open: boolean;
  /** Called when the drawer requests to close */
  onClose: () => void;
  /**
   * Required when entityType='links'.
   * Maps the MeepleEntityType to the LinkEntityType for the API call.
   */
  linkEntityType?: LinkEntityType;
  /** When present with chatSession entity type, renders EmbeddedChatView instead of ChatDrawerContent */
  liveChatData?: {
    threadId: string;
    agentId: string;
    gameId: string;
    gameName: string;
  };
  className?: string;
  'data-testid'?: string;
}

// ============================================================================
// Entity Configuration
// ============================================================================

const ENTITY_CONFIG: Record<
  DrawerEntityType,
  {
    label: string;
    /** HSL color string (without hsl()) */
    color: string;
    Icon: React.ComponentType<{ className?: string }>;
  }
> = {
  // Implemented entity types
  game: { label: 'Dettaglio Gioco', color: '25 95% 45%', Icon: Gamepad2 },
  agent: { label: 'Dettaglio Agente', color: '38 92% 50%', Icon: Bot },
  chatSession: { label: 'Dettaglio Chat', color: '220 80% 55%', Icon: MessageCircle },
  chat: { label: 'Dettaglio Chat', color: '220 80% 55%', Icon: MessageCircle },
  kb: { label: 'Documento KB', color: '174 60% 40%', Icon: FileText },
  links: { label: 'Connections', color: '210 40% 55%', Icon: LinkIcon },
  // New entity types (Coming Soon)
  player: { label: 'Dettaglio Giocatore', color: '262 83% 58%', Icon: User },
  session: { label: 'Dettaglio Sessione', color: '240 60% 55%', Icon: Layers },
  event: { label: 'Dettaglio Evento', color: '350 89% 60%', Icon: Calendar },
  collection: { label: 'Dettaglio Collezione', color: '20 70% 42%', Icon: Trophy },
  group: { label: 'Dettaglio Gruppo', color: '280 50% 48%', Icon: Users },
  location: { label: 'Dettaglio Luogo', color: '200 55% 45%', Icon: Map },
  expansion: { label: 'Dettaglio Espansione', color: '290 65% 50%', Icon: PackagePlus },
  toolkit: { label: 'Dettaglio Toolkit', color: '142 70% 45%', Icon: ToolCase },
  tool: { label: 'Dettaglio Strumento', color: '195 80% 50%', Icon: Wrench },
  achievement: { label: 'Dettaglio Achievement', color: '45 90% 48%', Icon: Award },
  note: { label: 'Dettaglio Nota', color: '40 30% 42%', Icon: StickyNote },
  custom: { label: 'Entità Personalizzata', color: '220 15% 45%', Icon: Zap },
};

// ============================================================================
// ExtraMeepleCardDrawer
// ============================================================================

export const ExtraMeepleCardDrawer = React.memo(function ExtraMeepleCardDrawer({
  entityType,
  entityId,
  open,
  onClose,
  linkEntityType,
  liveChatData,
  className,
  'data-testid': testId,
}: ExtraMeepleCardDrawerProps) {
  const config = ENTITY_CONFIG[entityType];
  const { Icon } = config;
  const { isGameMode, activeSessionId } = useDashboardMode();

  // During an active game session, highlight the drawer to signal session context
  const inSessionContext = isGameMode && !!activeSessionId;

  return (
    <Sheet
      open={open}
      onOpenChange={isOpen => {
        if (!isOpen) onClose();
      }}
    >
      <SheetContent
        side="right"
        className={cn(
          // Layout overrides: remove default padding, set full-height flex column
          'flex flex-col p-0',
          // Width override for wide drawer
          'w-full sm:w-[600px] sm:max-w-[600px]',
          // Hide the built-in close button (we render our own in the colored header)
          '[&>button:first-child]:hidden',
          // Session context: subtle indigo ring glow
          inSessionContext && 'ring-2 ring-indigo-400/60',
          className
        )}
        data-testid={testId}
      >
        {/* Screen-reader title (required by Radix Dialog for accessibility) */}
        <SheetTitle className="sr-only">{config.label}</SheetTitle>

        {/* Entity-colored header */}
        <div
          className="relative flex h-14 shrink-0 items-center gap-2.5 px-5"
          style={{ backgroundColor: `hsl(${config.color})` }}
        >
          <Icon className="h-4.5 w-4.5 text-white" aria-hidden="true" />
          <h2
            className="font-quicksand text-base font-bold text-white"
            data-testid={DRAWER_TEST_IDS.ENTITY_LABEL}
          >
            {config.label}
          </h2>

          {/* Session context badge */}
          {inSessionContext && (
            <span
              className="ml-auto mr-8 rounded-full bg-white/20 px-2 py-0.5 text-xs font-medium text-white"
              data-testid="drawer-session-badge"
            >
              In sessione
            </span>
          )}

          {/* Close button */}
          <SheetClose
            className={cn(
              'absolute right-4 top-1/2 -translate-y-1/2',
              'flex h-7 w-7 items-center justify-center rounded-full',
              'bg-white/20 text-white transition-colors duration-150',
              'hover:bg-white/30 focus:outline-none focus:ring-2 focus:ring-white/50'
            )}
            aria-label="Chiudi pannello"
          >
            <X className="h-3.5 w-3.5" />
          </SheetClose>
        </div>

        {/* Scrollable entity content */}
        <div className="flex flex-1 flex-col overflow-y-auto">
          <DrawerEntityRouter
            entityType={entityType}
            entityId={entityId}
            linkEntityType={linkEntityType}
            liveChatData={liveChatData}
          />
        </div>
      </SheetContent>
    </Sheet>
  );
});

// ============================================================================
// Entity Router
// ============================================================================

function DrawerEntityRouter({
  entityType,
  entityId,
  linkEntityType,
  liveChatData,
}: {
  entityType: DrawerEntityType;
  entityId: string;
  linkEntityType?: LinkEntityType;
  liveChatData?: ExtraMeepleCardDrawerProps['liveChatData'];
}) {
  switch (entityType) {
    case 'game':
      return <GameDrawerContent entityId={entityId} />;
    case 'agent':
      return <AgentDrawerContent entityId={entityId} />;
    case 'chatSession':
    case 'chat':
      if (liveChatData) {
        return (
          <EmbeddedChatView
            threadId={liveChatData.threadId}
            agentId={liveChatData.agentId}
            gameId={liveChatData.gameId}
            gameName={liveChatData.gameName}
          />
        );
      }
      return <ChatDrawerContent entityId={entityId} />;
    case 'kb':
      return <KbDrawerContent entityId={entityId} />;
    case 'links':
      return <LinksDrawerContent entityType={linkEntityType ?? 'Game'} entityId={entityId} />;
    // New entity types — Coming Soon until dedicated content components are implemented
    case 'player':
    case 'session':
    case 'event':
    case 'collection':
    case 'group':
    case 'location':
    case 'expansion':
    case 'toolkit':
    case 'tool':
    case 'achievement':
    case 'note':
    case 'custom':
      return <DrawerComingSoon label={ENTITY_CONFIG[entityType].label} issueNumber={0} />;
    default:
      return <DrawerErrorState error="Tipo entità non supportato" />;
  }
}

// ============================================================================
// Links Drawer Content
// ============================================================================

function LinksDrawerContent({
  entityType,
  entityId,
}: {
  entityType: LinkEntityType;
  entityId: string;
}) {
  return <RelatedEntitiesSection entityType={entityType} entityId={entityId} />;
}

// ============================================================================
// Game Drawer Content
// Fetches game data by ID and renders GameExtraMeepleCard.
// ============================================================================

function GameDrawerContent({ entityId }: { entityId: string }) {
  const { data, loading, error, retry } = useGameDetail(entityId);

  if (loading) return <DrawerLoadingSkeleton />;
  if (error) return <DrawerErrorState error={error} onRetry={retry} />;
  if (!data) return <DrawerLoadingSkeleton />;

  return (
    <GameExtraMeepleCard
      data={data}
      className="w-full rounded-none border-0 shadow-none bg-transparent"
    />
  );
}

// ============================================================================
// Agent Drawer Content
// Fetches agent data by ID and renders AgentExtraMeepleCard.
// ============================================================================

function AgentDrawerContent({ entityId }: { entityId: string }) {
  const { data, loading, error, retry } = useAgentDetail(entityId);

  if (loading) return <DrawerLoadingSkeleton />;
  if (error) return <DrawerErrorState error={error} onRetry={retry} />;
  if (!data) return <DrawerLoadingSkeleton />;

  return (
    <AgentExtraMeepleCard
      data={data}
      className="w-full rounded-none border-0 shadow-none bg-transparent"
    />
  );
}

function ChatDrawerContent({ entityId }: { entityId: string }) {
  const { data, loading, error, retry } = useChatDetail(entityId);

  if (loading) return <DrawerLoadingSkeleton />;
  if (error) return <DrawerErrorState error={error} onRetry={retry} />;
  if (!data) return <DrawerLoadingSkeleton />;

  return (
    <ChatExtraMeepleCard
      data={data}
      className="w-full rounded-none border-0 shadow-none bg-transparent"
    />
  );
}

function KbDrawerContent({ entityId }: { entityId: string }) {
  const { data, loading, error, retry } = useKbDetail(entityId);

  if (loading) return <DrawerLoadingSkeleton />;
  if (error) return <DrawerErrorState error={error} onRetry={retry} />;
  if (!data) return <DrawerLoadingSkeleton />;

  // v1: delete and retry are omitted in the drawer (read-only preview);
  // consumers needing these actions should use KbExtraMeepleCard standalone.
  return (
    <KbExtraMeepleCard
      data={data}
      className="w-full rounded-none border-0 shadow-none bg-transparent"
    />
  );
}

// ============================================================================
// Backward-compatible re-exports
// ============================================================================

export { DrawerLoadingSkeleton, DrawerErrorState } from './drawer-states';
