'use client';

import { useState, useEffect, useCallback } from 'react';

import Image from 'next/image';
import { useRouter } from 'next/navigation';

import { CopyrightDisclaimerModal } from '@/components/pdf/CopyrightDisclaimerModal';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { usePrivateGame } from '@/hooks/queries/useLibrary';
import { api } from '@/lib/api';

import { ActivationChecklist, type PdfStatus, type AgentStatus } from './ActivationChecklist';
import { PausedSessionCard, type PausedSession } from './PausedSessionCard';

interface PrivateGameHubProps {
  privateGameId: string;
}

export function PrivateGameHub({ privateGameId }: PrivateGameHubProps) {
  const router = useRouter();
  const { data: game, isLoading } = usePrivateGame(privateGameId);
  const [showDisclaimer, setShowDisclaimer] = useState(false);
  const [pdfStatus, setPdfStatus] = useState<PdfStatus>('none');
  const [agentStatus, setAgentStatus] = useState<AgentStatus>('none');
  const [pausedSessions, setPausedSessions] = useState<PausedSession[]>([]);

  // Derive PDF, agent, and session status from API on mount
  useEffect(() => {
    if (!privateGameId) return;
    let cancelled = false;

    async function loadStatus() {
      try {
        // Fetch PDFs, agents, and active sessions in parallel
        const [pdfs, agents, sessions] = await Promise.all([
          api.documents.getDocumentsByGame(privateGameId).catch(() => []),
          api.agents.getUserAgentsForGame(privateGameId).catch(() => []),
          api.liveSessions.getActive().catch(() => []),
        ]);

        if (cancelled) return;

        // Derive PDF status from latest document
        if (pdfs.length > 0) {
          const latestPdf = pdfs[0];
          const state = (latestPdf as Record<string, unknown>).processingState as string;
          if (state === 'Ready' || state === 'Indexed') {
            setPdfStatus('ready');
          } else if (['Extracting', 'Chunking', 'Embedding', 'Indexing'].includes(state)) {
            setPdfStatus('processing');
          } else if (state === 'Failed') {
            setPdfStatus('failed');
          }
        }

        // Derive agent status
        if (agents.length > 0) {
          setAgentStatus('ready');
        }

        // Derive paused sessions for this game
        const paused = sessions
          .filter(
            (s: Record<string, unknown>) => s.status === 'Paused' && s.gameId === privateGameId
          )
          .sort(
            (a: Record<string, unknown>, b: Record<string, unknown>) =>
              new Date(b.sessionDate as string).getTime() -
              new Date(a.sessionDate as string).getTime()
          );

        setPausedSessions(
          paused.map((s: Record<string, unknown>) => ({
            id: s.id as string,
            sessionDate: s.sessionDate as string,
            participants:
              (s.players as Array<Record<string, unknown>> | undefined)?.map(p => ({
                displayName: p.displayName as string,
                score: (p.totalScore as number) ?? 0,
              })) ?? [],
            hasPhotos: false,
            hasNotes: false,
            hasAgentSummary: false,
          }))
        );
      } catch {
        // Non-critical — hub still renders with defaults
      }
    }

    loadStatus();
    return () => {
      cancelled = true;
    };
  }, [privateGameId]);

  // Auto-create agent when PDF becomes ready (per spec)
  useEffect(() => {
    if (pdfStatus === 'ready' && agentStatus === 'none') {
      setAgentStatus('creating');
      api.agents
        .createUserAgent({ gameId: privateGameId, agentType: 'TutorAgent' })
        .then(() => setAgentStatus('ready'))
        .catch(() => setAgentStatus('none'));
    }
  }, [pdfStatus, agentStatus, privateGameId]);

  const handleUploadPdf = useCallback(() => {
    setShowDisclaimer(true);
  }, []);

  const handleDisclaimerAccept = useCallback(() => {
    setShowDisclaimer(false);
    // Actual file picker + upload wiring is done in Task 4
    setPdfStatus('uploading');
  }, []);

  const handleCreateAgent = useCallback(async () => {
    setAgentStatus('creating');
    try {
      await api.agents.createUserAgent({
        gameId: privateGameId,
        agentType: 'TutorAgent',
      });
      setAgentStatus('ready');
    } catch {
      setAgentStatus('none');
    }
  }, [privateGameId]);

  const handleStartGame = useCallback(async () => {
    try {
      const sessionId = await api.liveSessions.createSession({
        gameId: privateGameId,
      });
      router.push(`/sessions/${sessionId}/play`);
    } catch {
      // Error handling deferred to Task 4+
    }
  }, [privateGameId, router]);

  const handleResumeSession = useCallback(
    async (sessionId: string) => {
      try {
        await api.liveSessions.resumeSession(sessionId);
        router.push(`/sessions/${sessionId}/play`);
      } catch {
        // Error handling deferred
      }
    },
    [router]
  );

  const handleAbandonSession = useCallback(async (sessionId: string) => {
    if (!confirm('La partita verra\u0027 archiviata. Sei sicuro?')) return;
    try {
      await api.liveSessions.completeSession(sessionId);
      setPausedSessions(prev => prev.filter(s => s.id !== sessionId));
    } catch {
      // Error handling deferred
    }
  }, []);

  if (isLoading) {
    return (
      <div className="max-w-2xl mx-auto space-y-4">
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (!game) {
    return (
      <div className="max-w-2xl mx-auto">
        <p className="text-muted-foreground">Gioco non trovato.</p>
      </div>
    );
  }

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      {/* Game Header */}
      <div className="flex gap-4">
        {game.thumbnailUrl && (
          <Image
            src={game.thumbnailUrl}
            alt={game.title}
            width={96}
            height={96}
            className="rounded-lg object-cover"
          />
        )}
        <div>
          <h1 className="text-2xl font-bold">{game.title}</h1>
          {game.yearPublished && <p className="text-muted-foreground">{game.yearPublished}</p>}
          <p className="text-sm text-muted-foreground">
            {game.minPlayers}&ndash;{game.maxPlayers} giocatori
          </p>
        </div>
      </div>

      {/* Paused Sessions */}
      {pausedSessions.length > 0 && (
        <div className="space-y-3">
          <h3 className="text-lg font-semibold">Partite in pausa</h3>
          {pausedSessions.map(session => (
            <PausedSessionCard
              key={session.id}
              session={session}
              onResume={handleResumeSession}
              onAbandon={handleAbandonSession}
            />
          ))}
        </div>
      )}

      {/* Activation Checklist */}
      <ActivationChecklist
        gameAdded={true}
        pdfStatus={pdfStatus}
        agentStatus={agentStatus}
        onUploadPdf={handleUploadPdf}
        onCreateAgent={handleCreateAgent}
        onStartGame={handleStartGame}
      />

      {/* Copyright Disclaimer Modal */}
      <CopyrightDisclaimerModal
        open={showDisclaimer}
        onAccept={handleDisclaimerAccept}
        onCancel={() => setShowDisclaimer(false)}
      />
    </div>
  );
}
