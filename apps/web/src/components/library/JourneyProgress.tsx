/**
 * JourneyProgress — Collapsible onboarding strip
 * Issue #4949: Add collapsible journey progress banner for new players
 *
 * Shows a 5-step progress strip:
 *   Crea gioco → Carica PDF → KB pronta → Crea agente → Chat
 *
 * Visibility rules:
 *   - Hidden when all 5 steps are completed
 *   - Hidden after user clicks ✕ (persisted in localStorage)
 *
 * Usage:
 *   - On /library/private: pass the most recently created private game's ID to enable Steps 2-5
 *   - On /library/games/{id}: pass gameId for full per-game tracking
 *   - Pass no gameId to show only step-1 status (game not yet created)
 */

'use client';

import { useEffect, useMemo, useState } from 'react';

import { Check, ChevronDown, ChevronUp, Loader2, X } from 'lucide-react';

import { useQuery } from '@tanstack/react-query';

import { useGameAgents } from '@/hooks/queries/useGameAgents';
import { useRecentChatSessions } from '@/hooks/queries/useChatSessions';
import { usePdfProcessingStatus } from '@/hooks/queries/usePdfProcessingStatus';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

type StepStatus = 'completed' | 'active' | 'pending';

interface Step {
  id: string;
  label: string;
  status: StepStatus;
  /** Optional inline detail shown under the active step label */
  detail?: string;
  /** Navigate here when clicking a pending step */
  href?: string;
}

export interface JourneyProgressProps {
  /**
   * UUID of the private game to track.
   * When provided, steps 2–5 are evaluated for this specific game.
   * When absent, only step 1 is evaluated (no game selected yet).
   */
  gameId?: string;
  /**
   * AgentDefinitionId of the private game (from PrivateGameDto).
   * When explicitly provided (even as null), the agent step is checked via this
   * field instead of calling useGameAgents (which requires a shared catalog game ID).
   * Pass `undefined` (default) to keep legacy behaviour for shared catalog games.
   */
  agentDefinitionId?: string | null;
  /** Optional CSS class for the container element */
  className?: string;
}

// ============================================================================
// Constants
// ============================================================================

const STORAGE_KEY = 'journey-progress-dismissed';

// ============================================================================
// Component
// ============================================================================

export function JourneyProgress({ gameId, agentDefinitionId, className }: JourneyProgressProps) {
  // ── Dismissal / collapse state ──────────────────────────────────────────
  // Always start as false to avoid SSR hydration mismatch; read from
  // localStorage in useEffect (client-only) after the first render.
  const [dismissed, setDismissed] = useState(false);
  const [collapsed, setCollapsed] = useState(false);

  // Read persisted dismiss state after mount (SSR-safe).
  // Only the global key is checked; per-game auto-dismiss keys are intentionally
  // not checked here so the banner re-appears if the user revisits the page
  // and the journey has regressed (e.g., agent deleted).
  useEffect(() => {
    try {
      if (localStorage.getItem(STORAGE_KEY) === 'true') setDismissed(true);
    } catch { /* localStorage unavailable */ }
  }, []);

  // ── Data sources ─────────────────────────────────────────────────────────

  // Step 1 — private games exist (query only needed when no specific gameId)
  const { data: privateGamesData } = useQuery({
    queryKey: ['private-games', 'journey-check'],
    queryFn: () => api.library.getPrivateGames({ pageSize: 1 }),
    enabled: !gameId,
    staleTime: 30_000,
  });

  // Steps 2–3 — PDF upload & indexing status
  const { data: pdfStatus, isLoading: pdfLoading, isError: pdfError } =
    usePdfProcessingStatus(gameId ?? null);

  // Step 4 — agent created
  // When agentDefinitionId is explicitly provided (private game context), skip the
  // shared-catalog /games/{id}/agents call (which returns 404 for private game UUIDs)
  // and derive agent presence directly from the DTO field instead.
  const useSharedGameAgents = agentDefinitionId === undefined;
  const { data: agents } = useGameAgents({
    gameId: gameId ?? null,
    enabled: !!gameId && useSharedGameAgents,
  });

  // Step 5 — chat threads for this game
  const { data: chatData } = useRecentChatSessions(50);

  // ── Derived step states ────────────────────────────────────────────────

  const steps = useMemo<Step[]>(() => {
    // Step 1: has at least one private game
    const hasGame = !!gameId || (privateGamesData?.totalCount ?? 0) > 0;

    // Step 2: a PDF was uploaded (query succeeded with data — 404 means no PDF)
    const hasPdf = !!gameId && !pdfLoading && !pdfError && pdfStatus !== undefined;

    // Step 3: PDF is indexed (or processing)
    const pdfIsIndexed = pdfStatus?.status === 'indexed';
    const pdfIsProcessing = pdfStatus?.status === 'processing' || pdfStatus?.status === 'pending';

    // Step 4: agent configured
    // For private games: agentDefinitionId prop is explicitly set → use it directly.
    // For shared catalog games: fall back to agents list from useGameAgents.
    const hasAgent = !!gameId && (
      agentDefinitionId !== undefined
        ? !!agentDefinitionId          // private game: truthy UUID = has agent
        : (agents?.length ?? 0) > 0   // shared catalog game: check agents list
    );

    // Step 5: at least one chat session exists for this game
    const hasChat = !!gameId && (chatData?.sessions ?? []).some(s => s.gameId === gameId);

    // --- Build step statuses (completed/active/pending) ---
    const s1 = hasGame;
    const s2 = hasPdf;
    const s3 = pdfIsIndexed;
    const s4 = hasAgent;
    const s5 = hasChat;

    // First incomplete step becomes "active"
    function resolveStatus(completed: boolean, prevCompleted: boolean): StepStatus {
      if (completed) return 'completed';
      if (prevCompleted) return 'active';
      return 'pending';
    }

    const step1Status = s1 ? 'completed' : 'active';
    const step2Status = resolveStatus(s2, s1);
    // Step 3: once PDF is uploaded (s2), any non-indexed state is "active"
    const step3Status: StepStatus = (() => {
      if (s3) return 'completed';
      if (s2) return 'active'; // pdf uploaded but not yet indexed (includes processing)
      return 'pending';
    })();
    const step4Status = resolveStatus(s4, s3);
    const step5Status = resolveStatus(s5, s4);

    // Inline detail for active KB step
    const kbDetail = step3Status === 'active' && pdfIsProcessing && pdfStatus?.progress != null
      ? `Indicizzazione: ${pdfStatus.progress}%…`
      : step3Status === 'active' && pdfIsProcessing
        ? 'Indicizzazione in corso…'
        : undefined;

    return [
      {
        id: 'create-game',
        label: 'Crea gioco',
        status: step1Status,
        href: '/library/private/add',
      },
      {
        id: 'upload-pdf',
        label: 'Carica PDF',
        status: step2Status,
        href: gameId ? `/library/private/add?gameId=${gameId}&step=2` : '/library/private/add',
      },
      {
        id: 'kb-ready',
        label: 'KB pronta',
        status: step3Status,
        detail: kbDetail,
      },
      {
        id: 'create-agent',
        label: 'Crea agente',
        status: step4Status,
        href: gameId ? `/library/games/${gameId}/agent` : undefined,
      },
      {
        id: 'chat',
        label: 'Chat',
        status: step5Status,
        href: gameId ? `/chat?gameId=${gameId}` : undefined,
      },
    ];
  }, [
    privateGamesData,
    gameId,
    agentDefinitionId,
    pdfStatus,
    pdfLoading,
    pdfError,
    agents,
    chatData,
  ]);

  // ── Journey complete? ──────────────────────────────────────────────────
  const isComplete = steps.every(s => s.status === 'completed');

  // Auto-dismiss when complete.
  // Writes a per-game key (when gameId is provided) so different games track
  // their own completion state independently.
  useEffect(() => {
    if (isComplete) {
      try {
        const key = gameId ? `${STORAGE_KEY}-${gameId}` : STORAGE_KEY;
        localStorage.setItem(key, 'true');
      } catch { /* ignore */ }
      setDismissed(true);
    }
  }, [isComplete, gameId]);

  // ── Hide when dismissed or complete ───────────────────────────────────
  if (dismissed) return null;

  // Manual dismiss writes to the global key to suppress the banner across all games.
  const handleDismiss = () => {
    try {
      localStorage.setItem(STORAGE_KEY, 'true');
    } catch { /* ignore */ }
    setDismissed(true);
  };

  // ============================================================================
  // Render
  // ============================================================================

  return (
    <div
      className={cn(
        'rounded-xl border border-border/50 bg-card/80 backdrop-blur-sm overflow-hidden',
        className
      )}
      data-testid="journey-progress"
      role="region"
      aria-label="Percorso di configurazione"
    >
      {/* Header row */}
      <div className="flex items-center justify-between px-4 py-2.5">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          Il tuo percorso verso l&apos;AI coach
        </h3>
        <div className="flex items-center gap-1">
          <button
            type="button"
            onClick={() => setCollapsed(c => !c)}
            className="p-1 rounded hover:bg-muted/60 transition-colors text-muted-foreground hover:text-foreground"
            aria-label={collapsed ? 'Espandi' : 'Comprimi'}
            data-testid="journey-collapse-btn"
          >
            {collapsed ? (
              <ChevronDown className="h-3.5 w-3.5" />
            ) : (
              <ChevronUp className="h-3.5 w-3.5" />
            )}
          </button>
          <button
            type="button"
            onClick={handleDismiss}
            className="p-1 rounded hover:bg-muted/60 transition-colors text-muted-foreground hover:text-foreground"
            aria-label="Chiudi percorso"
            data-testid="journey-dismiss-btn"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        </div>
      </div>

      {/* Steps strip */}
      {!collapsed && (
        <div className="px-4 pb-4">
          <div
            className="flex items-start gap-0 overflow-x-auto"
            data-testid="journey-steps"
            role="list"
          >
            {steps.map((step, index) => (
              <StepItem
                key={step.id}
                step={step}
                isLast={index === steps.length - 1}
              />
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

// ============================================================================
// StepItem sub-component
// ============================================================================

function StepItem({
  step,
  isLast,
}: {
  step: Step;
  isLast: boolean;
}) {
  const isCompleted = step.status === 'completed';
  const isActive = step.status === 'active';

  const icon = (
    <div
      className={cn(
        'relative h-6 w-6 rounded-full flex items-center justify-center shrink-0 border-2 transition-all duration-300',
        isCompleted && 'border-teal-500 bg-teal-500 text-white',
        isActive && 'border-orange-500 bg-background text-orange-500',
        !isCompleted && !isActive && 'border-muted-foreground/30 bg-background text-muted-foreground/40'
      )}
      aria-hidden="true"
    >
      {isCompleted ? (
        <Check className="h-3.5 w-3.5" />
      ) : isActive && step.id === 'kb-ready' && step.detail ? (
        <Loader2 className="h-3 w-3 animate-spin" />
      ) : (
        <span className="h-1.5 w-1.5 rounded-full bg-current" />
      )}
      {/* Pulse ring for active step */}
      {isActive && (
        <span
          className="absolute inset-0 rounded-full border-2 border-orange-400/50 animate-ping"
          aria-hidden="true"
        />
      )}
    </div>
  );

  const content = (
    <div
      className={cn(
        'flex flex-col items-center gap-1.5 min-w-0',
        isCompleted && 'opacity-100',
        isActive && 'opacity-100',
        !isCompleted && !isActive && 'opacity-40'
      )}
    >
      {icon}
      <div className="flex flex-col items-center gap-0.5">
        <span
          className={cn(
            'text-xs font-medium whitespace-nowrap',
            isCompleted && 'text-teal-600 dark:text-teal-400',
            isActive && 'text-orange-600 dark:text-orange-400',
            !isCompleted && !isActive && 'text-muted-foreground'
          )}
        >
          {step.label}
        </span>
        {step.detail && (
          <span
            className="text-[10px] text-orange-500 dark:text-orange-400 whitespace-nowrap"
            data-testid={`journey-step-detail-${step.id}`}
            aria-live="polite"
            aria-atomic="true"
          >
            {step.detail}
          </span>
        )}
      </div>
    </div>
  );

  const ariaLabel = `${step.label}${isCompleted ? ' (completato)' : ''}`;

  const wrappedContent =
    step.href && !isCompleted ? (
      <a
        href={step.href}
        className="focus:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded"
        aria-label={ariaLabel}
      >
        {content}
      </a>
    ) : (
      <>{content}</>
    );

  return (
    <div
      className="flex items-center flex-1 min-w-0"
      role="listitem"
      aria-label={!step.href || isCompleted ? ariaLabel : undefined}
      data-testid={`journey-step-${step.id}`}
      data-status={step.status}
    >
      {/* Step content */}
      <div className="flex flex-col items-center gap-1 flex-shrink-0 px-2">
        {wrappedContent}
      </div>

      {/* Connector line (not shown after last step) */}
      {!isLast && (
        <div
          className={cn(
            'h-0.5 flex-1 mx-1 rounded-full transition-all duration-300',
            isCompleted ? 'bg-teal-500/70' : 'bg-muted-foreground/20'
          )}
          aria-hidden="true"
        />
      )}
    </div>
  );
}
