'use client';

import { Suspense, useCallback } from 'react';

import { AnimatePresence, motion } from 'framer-motion';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { ExtraMeepleCardDrawer } from '@/components/ui/data-display/extra-meeple-card';
import { MeepleCard, MeepleCardSkeleton } from '@/components/ui/data-display/meeple-card';
import type { AgentStatus } from '@/components/ui/data-display/meeple-card';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import { useAgents } from '@/hooks/queries/useAgents';
import { useRecentlyAddedGames } from '@/hooks/queries/useLibrary';
import type { GameSessionDto } from '@/lib/api/schemas';
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import { useDashboardSearchStore } from '@/stores/useDashboardSearchStore';

import { AddToLibraryModal } from './AddToLibraryModal';
import { TavoloLayout, TavoloSection, ActiveSessionCard } from './tavolo';
import { useDashboardMode } from './useDashboardMode';
import { HeroZone, SessionBar, ScoreboardZone } from './zones';
import './dashboard-transitions.css';

// ─── Helpers ─────────────────────────────────────────────────────────────────

function mapAgentStatus(agent: AgentDto): AgentStatus {
  if (!agent.isActive) return 'idle';
  if (agent.isIdle) return 'idle';
  return 'active';
}

function formatDuration(minutes: number): string {
  if (minutes < 60) return `${minutes} min`;
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return m > 0 ? `${h}h ${m}m` : `${h}h`;
}

// ─── Skeleton fallback ────────────────────────────────────────────────────────

function ZoneSkeleton({ testId }: { testId: string }) {
  return <div data-testid={testId} className="animate-pulse rounded-2xl bg-muted h-32 w-full" />;
}

function SkeletonRow({ count }: { count: number }) {
  return (
    <div className="flex gap-4 overflow-x-auto pb-2">
      {Array.from({ length: count }, (_, i) => (
        <div key={i} className="min-w-[200px] max-w-[220px] shrink-0">
          <MeepleCardSkeleton variant="grid" />
        </div>
      ))}
    </div>
  );
}

// ─── Section: Active Sessions ─────────────────────────────────────────────────

function ActiveSessionsSection() {
  const { data: activeData, isLoading } = useActiveSessions(5);
  const sessions: GameSessionDto[] = activeData?.sessions ?? [];

  if (isLoading) {
    return (
      <div className="space-y-2">
        {Array.from({ length: 2 }).map((_, i) => (
          <div key={i} className="animate-pulse h-16 rounded-xl bg-muted" />
        ))}
      </div>
    );
  }

  if (sessions.length === 0) {
    return (
      <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-[#30363d] bg-[#0d1117]/50 py-8 text-center">
        <span className="text-2xl">🎲</span>
        <p className="text-sm text-[#8b949e]">Nessuna partita in corso — inizia a giocare</p>
        <Link href="/sessions" className="text-xs font-semibold text-[#f0a030] hover:underline">
          Nuova partita →
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      {sessions.map(session => (
        <ActiveSessionCard
          key={session.id}
          session={{
            id: session.id,
            gameName: `Partita in corso`,
            playerCount: session.playerCount,
            duration: formatDuration(session.durationMinutes),
          }}
        />
      ))}
    </div>
  );
}

// ─── Section: Recent Games ────────────────────────────────────────────────────

function RecentGamesSection() {
  const { data, isLoading } = useRecentlyAddedGames(8);
  const games: UserLibraryEntry[] = data?.items ?? [];

  if (isLoading) {
    return <SkeletonRow count={4} />;
  }

  if (games.length === 0) {
    return (
      <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-[#30363d] bg-[#0d1117]/50 py-8 text-center">
        <span className="text-2xl">📚</span>
        <p className="text-sm text-[#8b949e]">Il tavolo è vuoto — esplora il catalogo</p>
        <Link
          href="/library?action=add"
          className="text-xs font-semibold text-[#f0a030] hover:underline"
        >
          Aggiungi giochi →
        </Link>
      </div>
    );
  }

  return (
    <div
      className="flex gap-4 overflow-x-auto pb-2 snap-x snap-mandatory"
      style={{ WebkitOverflowScrolling: 'touch' }}
    >
      {games.map((game: UserLibraryEntry) => (
        <div key={game.id} className="min-w-[200px] max-w-[220px] snap-start shrink-0">
          <MeepleCard
            id={game.gameId}
            entity="game"
            variant="grid"
            title={game.gameTitle}
            subtitle={game.gamePublisher ?? undefined}
            imageUrl={game.gameImageUrl ?? undefined}
            data-testid={`tavolo-game-${game.gameId}`}
          />
        </div>
      ))}
    </div>
  );
}

// ─── Section: Agents ─────────────────────────────────────────────────────────

function AgentsSection() {
  const router = useRouter();
  const { data: agents = [], isLoading } = useAgents();

  if (isLoading) {
    return (
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="animate-pulse h-16 rounded-xl bg-muted" />
        ))}
      </div>
    );
  }

  if (agents.length === 0) {
    return (
      <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-[#30363d] bg-[#0d1117]/50 py-8 text-center">
        <span className="text-2xl">🤖</span>
        <p className="text-sm text-[#8b949e]">Configura il tuo primo agente AI</p>
        <Link href="/agents" className="text-xs font-semibold text-[#f0a030] hover:underline">
          Crea agente →
        </Link>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
      {agents.map(agent => (
        <MeepleCard
          key={agent.id}
          entity="agent"
          variant="compact"
          title={agent.name}
          subtitle={agent.type}
          agentStatus={mapAgentStatus(agent)}
          agentModel={{ modelName: agent.strategyName }}
          onClick={() => router.push(`/agents/${agent.id}`)}
          data-testid={`tavolo-agent-${agent.id}`}
        />
      ))}
    </div>
  );
}

// ─── Exploration mode with TavoloLayout ──────────────────────────────────────

function ExplorationView() {
  return (
    <>
      <motion.div layoutId="hero">
        <Suspense fallback={<ZoneSkeleton testId="hero-skeleton" />}>
          <HeroZone />
        </Suspense>
      </motion.div>

      <TavoloLayout>
        <TavoloSection icon="🎮" title="Sessioni attive">
          <ActiveSessionsSection />
        </TavoloSection>

        <TavoloSection icon="📚" title="Giochi recenti dalla tua libreria">
          <RecentGamesSection />
        </TavoloSection>

        <TavoloSection icon="🤖" title="I tuoi agenti">
          <AgentsSection />
        </TavoloSection>
      </TavoloLayout>
    </>
  );
}

// ─── DashboardRenderer ────────────────────────────────────────────────────────

/**
 * Main dashboard layout renderer.
 *
 * Reads the current mode from `useDashboardMode()` and renders
 * either the TavoloLayout exploration view or game-mode zones with
 * animated transitions via framer-motion `AnimatePresence`.
 */
export function DashboardRenderer() {
  const { state, isGameMode, isExploration } = useDashboardMode();
  const { selectedGame, setSelectedGame, openChatDrawer, drawerState, closeChatDrawer } =
    useDashboardSearchStore();

  const handleModalSuccess = useCallback(
    ({
      gameId,
      threadId,
      agentId,
      gameName,
    }: {
      gameId: string;
      threadId: string;
      agentId: string;
      gameName: string;
    }) => {
      setSelectedGame(null);
      openChatDrawer({ threadId, agentId, gameId, gameName });
    },
    [setSelectedGame, openChatDrawer]
  );

  return (
    <div data-testid="dashboard-renderer" className="flex flex-col gap-6 w-full">
      <AnimatePresence mode="wait">
        {(isExploration || state === 'transitioning') && !isGameMode && (
          <motion.div
            key="exploration"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="flex flex-col gap-6 w-full"
          >
            <ExplorationView />
          </motion.div>
        )}

        {isGameMode && (
          <motion.div
            key="gameMode"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="flex flex-col gap-6 w-full"
          >
            <motion.div layoutId="hero">
              <Suspense fallback={<ZoneSkeleton testId="session-bar-skeleton" />}>
                <SessionBar />
              </Suspense>
            </motion.div>

            <Suspense fallback={<ZoneSkeleton testId="scoreboard-skeleton" />}>
              <ScoreboardZone />
            </Suspense>
          </motion.div>
        )}
      </AnimatePresence>

      <AddToLibraryModal
        game={selectedGame}
        isOpen={selectedGame !== null}
        onClose={() => setSelectedGame(null)}
        onSuccess={handleModalSuccess}
      />

      {drawerState && (
        <ExtraMeepleCardDrawer
          entityType="chatSession"
          entityId={drawerState.threadId}
          open={true}
          onClose={closeChatDrawer}
          liveChatData={drawerState}
          data-testid="dashboard-chat-drawer"
        />
      )}
    </div>
  );
}
