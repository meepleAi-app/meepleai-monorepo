/**
 * Chat Sessions List Page - /chat
 *
 * Lists user's chat sessions grouped by agent with collapsible groups.
 * Shows a tier usage banner when the user reaches 80% of their session limit.
 *
 * @see Issue #4695 (original), #4913 (grouped history + tier banner)
 */

'use client';

import { useEffect, useMemo, useState } from 'react';

import { AlertTriangle, ChevronDown, ChevronRight, MessageCircle, Plus, Bot } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import type { MeepleCardProps } from '@/components/ui/data-display/meeple-card';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Progress } from '@/components/ui/feedback/progress';
import { Button } from '@/components/ui/primitives/button';
import { getNavigationLinks } from '@/config/entity-navigation';
import { useChatSessionLimit, useRecentChatSessions } from '@/hooks/queries/useChatSessions';
import type { ChatSessionSummaryDto } from '@/lib/api/schemas/chat-sessions.schemas';
import { useCardHand } from '@/stores/use-card-hand';

// ─── Agent Group ─────────────────────────────────────────────────────────────

interface AgentGroup {
  /** Stable key for collapsing state */
  key: string;
  /** Display label (agent name or system type) */
  label: string;
  sessions: ChatSessionSummaryDto[];
}

const SYSTEM_AGENT_LABELS: Record<string, string> = {
  auto: 'Agente Auto',
  tutor: 'Tutor',
  arbitro: 'Arbitro',
  stratega: 'Stratega',
  narratore: 'Narratore',
};

function groupSessionsByAgent(sessions: ChatSessionSummaryDto[]): AgentGroup[] {
  const map = new Map<string, AgentGroup>();

  for (const session of sessions) {
    let key: string;
    let label: string;

    if (session.agentId) {
      key = `agent:${session.agentId}`;
      label = session.agentName ?? 'Agente personalizzato';
    } else if (session.agentType) {
      key = `type:${session.agentType}`;
      label = SYSTEM_AGENT_LABELS[session.agentType.toLowerCase()] ?? session.agentType;
    } else {
      key = 'none';
      label = 'Chat generali';
    }

    if (!map.has(key)) {
      map.set(key, { key, label, sessions: [] });
    }
    const group = map.get(key);
    if (group) group.sessions.push(session);
  }

  // Sort: most recently active group first (use max across all sessions in group)
  const latestDate = (group: AgentGroup) =>
    group.sessions.reduce((max, s) => {
      const d = s.lastMessageAt ?? s.createdAt ?? '';
      return d > max ? d : max;
    }, '');

  return [...map.values()].sort(
    (a, b) => new Date(latestDate(b)).getTime() - new Date(latestDate(a)).getTime()
  );
}

// ─── Format helpers ───────────────────────────────────────────────────────────

function formatRelativeDate(dateString: string | null | undefined): string {
  if (!dateString) return 'Mai';
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60_000);
  if (diffMins < 1) return 'Adesso';
  if (diffMins < 60) return `${diffMins}m fa`;
  const diffHours = Math.floor(diffMins / 60);
  if (diffHours < 24) return `${diffHours}h fa`;
  const diffDays = Math.floor(diffHours / 24);
  if (diffDays < 7) return `${diffDays}g fa`;
  return date.toLocaleDateString('it-IT', { day: 'numeric', month: 'short' });
}

function renderChatSessionCard(
  session: ChatSessionSummaryDto
): Omit<MeepleCardProps, 'entity' | 'variant'> {
  const navLinks = getNavigationLinks('chatSession', {
    id: session.id,
    gameId: session.gameId,
  });

  return {
    title: session.title || 'Chat senza titolo',
    subtitle: session.gameTitle ?? undefined,
    metadata: [
      { icon: MessageCircle, value: `${session.messageCount} messaggi` },
      { value: formatRelativeDate(session.lastMessageAt) },
    ],
    badge: session.isArchived ? 'Archiviata' : undefined,
    chatPreview: session.lastMessagePreview
      ? { lastMessage: session.lastMessagePreview, sender: 'agent' as const }
      : undefined,
    navigateTo: navLinks,
  };
}

// ─── Agent Group Row ──────────────────────────────────────────────────────────

function AgentGroupSection({
  group,
  onSessionClick,
}: {
  group: AgentGroup;
  onSessionClick: (session: ChatSessionSummaryDto) => void;
}) {
  const [expanded, setExpanded] = useState(true);

  return (
    <section className="mb-6">
      {/* Group header */}
      <button
        type="button"
        className="flex items-center gap-2 w-full text-left py-2 px-1 rounded hover:bg-muted/50 transition-colors"
        onClick={() => setExpanded(v => !v)}
        aria-expanded={expanded}
      >
        {expanded ? (
          <ChevronDown className="h-4 w-4 text-muted-foreground shrink-0" />
        ) : (
          <ChevronRight className="h-4 w-4 text-muted-foreground shrink-0" />
        )}
        <Bot className="h-4 w-4 text-amber-600 shrink-0" />
        <span className="font-quicksand font-semibold text-sm text-foreground">{group.label}</span>
        <span className="text-xs text-muted-foreground font-nunito ml-1">
          ({group.sessions.length})
        </span>
      </button>

      {/* Sessions grid */}
      {expanded && (
        <div className="mt-2 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-3">
          {group.sessions.map(session => {
            const cardProps = renderChatSessionCard(session);
            return (
              <MeepleCard
                key={session.id}
                entity="chatSession"
                variant="grid"
                {...cardProps}
                onClick={() => onSessionClick(session)}
              />
            );
          })}
        </div>
      )}
    </section>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function ChatListPage() {
  const router = useRouter();
  const { drawCard } = useCardHand();
  const { data, isLoading, error } = useRecentChatSessions(500);
  const { data: limitData } = useChatSessionLimit();

  useEffect(() => {
    drawCard({
      id: 'section-chat',
      entity: 'chatSession',
      title: 'Chat',
      href: '/chat',
    });
  }, [drawCard]);

  const groups = useMemo(() => {
    const sessions = data?.sessions ?? [];
    return groupSessionsByAgent(sessions);
  }, [data]);

  // Tier banner: show when >= 80% of limit is used (limit=0 means unlimited)
  const showBanner =
    limitData !== null &&
    limitData !== undefined &&
    limitData.limit > 0 &&
    limitData.used / limitData.limit >= 0.8;

  const usagePercent =
    limitData && limitData.limit > 0
      ? Math.min(100, Math.round((limitData.used / limitData.limit) * 100))
      : 0;

  if (error) {
    return (
      <div className="min-h-screen bg-background py-8 px-4">
        <div className="container mx-auto max-w-7xl">
          <Alert variant="destructive">
            <AlertDescription>Errore nel caricamento delle sessioni chat.</AlertDescription>
          </Alert>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background py-8 px-4">
      <div className="container mx-auto max-w-7xl">
        {/* Header */}
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-3xl font-quicksand font-bold text-foreground">Le tue Chat</h1>
            <p className="text-muted-foreground font-nunito mt-1">
              Tutte le conversazioni con gli agenti AI
            </p>
          </div>
          <Button asChild className="font-nunito">
            <Link href="/chat/new">
              <Plus className="mr-2 h-4 w-4" />
              Nuova Chat
            </Link>
          </Button>
        </div>

        {/* Tier usage banner */}
        {showBanner && limitData && (
          <Alert className="mb-6 border-amber-200 bg-amber-50 text-amber-900">
            <AlertTriangle className="h-4 w-4 text-amber-600" />
            <AlertDescription className="flex flex-col gap-2">
              <span className="font-nunito font-medium">
                Stai utilizzando {limitData.used} su {limitData.limit} chat ({usagePercent}%). Le
                chat più vecchie verranno archiviate automaticamente quando raggiungi il limite.
              </span>
              <Progress value={usagePercent} className="h-2 bg-amber-200 [&>div]:bg-amber-500" />
            </AlertDescription>
          </Alert>
        )}

        {/* Loading */}
        {isLoading && (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-3">
            {Array.from({ length: 8 }).map((_, i) => (
              <div
                key={i}
                className="h-32 rounded-lg bg-muted/40 animate-pulse"
                aria-hidden="true"
              />
            ))}
          </div>
        )}

        {/* Grouped list */}
        {!isLoading && groups.length === 0 && (
          <div className="text-center py-16 text-muted-foreground font-nunito">
            <MessageCircle className="h-12 w-12 mx-auto mb-4 opacity-30" />
            <p>Nessuna chat trovata. Inizia una nuova conversazione!</p>
          </div>
        )}

        {!isLoading &&
          groups.map(group => (
            <AgentGroupSection
              key={group.key}
              group={group}
              onSessionClick={session => router.push(`/chat/${session.id}`)}
            />
          ))}
      </div>
    </div>
  );
}
