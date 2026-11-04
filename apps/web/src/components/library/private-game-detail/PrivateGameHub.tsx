'use client';

import { useState, useEffect, useCallback, useRef } from 'react';

import Image from 'next/image';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { PlayerSetupDialog, type PlayerSetup } from '@/components/game-night/PlayerSetupDialog';
import { CopyrightDisclaimerModal } from '@/components/pdf/CopyrightDisclaimerModal';
import { PdfProcessingProgressBar } from '@/components/pdf/PdfProcessingProgressBar';
import { ProgressCard } from '@/components/pdf/progress-card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { usePrivateGame } from '@/hooks/queries/useLibrary';
import { useChatPanel } from '@/hooks/useChatPanel';
import { api } from '@/lib/api';

import { ActivationChecklist, type PdfStatus } from './ActivationChecklist';
import { MeeplePausedSessionCard, type PausedSession } from './MeeplePausedSessionCard';

interface PrivateGameHubProps {
  privateGameId: string;
}

export function PrivateGameHub({ privateGameId }: PrivateGameHubProps) {
  const router = useRouter();
  const { data: game, isLoading } = usePrivateGame(privateGameId);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [showDisclaimer, setShowDisclaimer] = useState(false);
  const [pdfStatus, setPdfStatus] = useState<PdfStatus>('none');
  const [activePdfId, setActivePdfId] = useState<string | null>(null);
  const [activePdfName, setActivePdfName] = useState<string>('');

  // Issue #4137: the surface that was missing. The backend grants a PrivateGame
  // owner access to their own KB, but nothing in the UI could ask - this hub had
  // neither a chat panel nor an entry point, so the permission was unreachable.
  // The global slide-over is scoped by game (#4139 moved the stream from an agent
  // id to /agents/qa/stream), so opening it with the private game's id is all it
  // takes.
  const { open: openChatPanel } = useChatPanel();
  const [uploadProgress, setUploadProgress] = useState(0);
  const [pausedSessions, setPausedSessions] = useState<PausedSession[]>([]);
  const [showPlayerSetup, setShowPlayerSetup] = useState(false);
  const [isStarting, setIsStarting] = useState(false);

  // Derive PDF and session status from API on mount.
  // Issue #4138: the agents fetch is gone — nothing per-game is awaited now
  // that the agent is system-wide; the knowledge base is what gates an answer.
  useEffect(() => {
    if (!privateGameId) return;
    let cancelled = false;

    async function loadStatus() {
      try {
        const [pdfs, sessions] = await Promise.all([
          api.documents.getDocumentsByGame(privateGameId).catch(() => []),
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

  // Issue #4138: the "auto-create agent when the PDF becomes ready" effect that
  // stood here is gone. It called createUserAgent with agentType 'TutorAgent',
  // which AgentType.Parse rejects, so it answered 400 on every run and the
  // catch folded the failure back into 'none'. The backend already does this
  // correctly in AutoCreateAgentOnPdfReadyHandler.
  const handleUploadPdf = useCallback(() => {
    setShowDisclaimer(true);
  }, []);

  const handleDisclaimerAccept = useCallback(() => {
    setShowDisclaimer(false);
    // Trigger the hidden file picker after disclaimer acceptance
    fileInputRef.current?.click();
  }, []);

  const handleFileSelected = useCallback(
    async (event: React.ChangeEvent<HTMLInputElement>) => {
      const file = event.target.files?.[0];
      if (!file) return;

      // Reset the input so re-selecting the same file triggers onChange
      event.target.value = '';

      setPdfStatus('uploading');
      setUploadProgress(0);

      try {
        const result = await api.pdf.uploadPdf(
          privateGameId,
          file,
          percent => {
            setUploadProgress(percent);
          },
          { isPrivateGame: true }
        );

        setActivePdfId(result.documentId);
        setActivePdfName(file.name);
        setPdfStatus('processing');
      } catch {
        setPdfStatus('failed');
      }
    },
    [privateGameId]
  );

  // Issue #4137: `onTryQuestion` was already a prop of ActivationChecklist and no
  // host passed it, so the CTA never rendered. It is gated on the KB being ready,
  // which is also what decides whether an answer is possible.
  const handleTryQuestion = useCallback(() => {
    openChatPanel({
      id: privateGameId,
      name: game?.title ?? 'Gioco privato',
      pdfCount: activePdfId ? 1 : 0,
      kbStatus: pdfStatus === 'ready' ? 'ready' : pdfStatus === 'failed' ? 'failed' : 'indexing',
    });
  }, [openChatPanel, privateGameId, game?.title, activePdfId, pdfStatus]);

  const handleStartGame = useCallback(() => {
    setShowPlayerSetup(true);
  }, []);

  const handlePlayerSetupComplete = useCallback(
    async (players: PlayerSetup[]) => {
      setIsStarting(true);
      try {
        const sessionId = await api.liveSessions.createSession({
          gameId: privateGameId,
          gameName: game?.title,
        });

        // Add players to session
        for (const player of players) {
          await api.liveSessions.addPlayer(sessionId, {
            displayName: player.displayName,
            color: player.color,
          });
        }

        // Start the session (Created → InProgress)
        await api.liveSessions.startSession(sessionId);

        setShowPlayerSetup(false);
        router.push(`/sessions/${sessionId}/live`);
      } catch {
        toast.error('Impossibile avviare la partita', {
          description: 'Riprova tra qualche secondo.',
        });
      } finally {
        setIsStarting(false);
      }
    },
    [privateGameId, game?.title, router]
  );

  const handleResumeSession = useCallback(
    async (sessionId: string) => {
      try {
        await api.liveSessions.resumeSession(sessionId);
        router.push(`/sessions/${sessionId}/live`);
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
            <MeeplePausedSessionCard
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
        onUploadPdf={handleUploadPdf}
        onStartGame={handleStartGame}
        onTryQuestion={handleTryQuestion}
      >
        {pdfStatus === 'uploading' && (
          <div className="space-y-2">
            <p className="text-sm text-muted-foreground">
              Caricamento in corso... {uploadProgress}%
            </p>
            <div className="h-2 w-full rounded-full bg-muted">
              <div
                className="h-2 rounded-full bg-primary transition-all duration-300"
                style={{ width: `${uploadProgress}%` }}
              />
            </div>
          </div>
        )}
        {pdfStatus === 'processing' && activePdfId && (
          <>
            <ProgressCard
              documentId={activePdfId}
              title={activePdfName || 'PDF'}
              subtitle="Elaborazione in corso..."
              defaultExpanded
            />
            <PdfProcessingProgressBar
              pdfId={activePdfId}
              onComplete={() => setPdfStatus('ready')}
              onError={() => setPdfStatus('failed')}
              onCancel={() => setPdfStatus('none')}
            />
          </>
        )}
      </ActivationChecklist>

      {/* Hidden file input for PDF upload */}
      <input
        ref={fileInputRef}
        type="file"
        accept=".pdf"
        className="hidden"
        onChange={handleFileSelected}
      />

      {/* Copyright Disclaimer Modal */}
      <CopyrightDisclaimerModal
        open={showDisclaimer}
        onAccept={handleDisclaimerAccept}
        onCancel={() => setShowDisclaimer(false)}
      />

      {/* Issue 5 fix: conditional mount resets state on each open */}
      {showPlayerSetup && (
        <PlayerSetupDialog
          open={showPlayerSetup}
          onOpenChange={setShowPlayerSetup}
          gameName={game?.title ?? 'Gioco'}
          minPlayers={game?.minPlayers ?? 1}
          maxPlayers={game?.maxPlayers ?? 10}
          onStart={handlePlayerSetupComplete}
          isLoading={isStarting}
        />
      )}
    </div>
  );
}
