/**
 * SessionSheet - Quick session management sheet for placeholder action card
 *
 * Two states:
 * - Active sessions exist: list of active/paused sessions, click to navigate
 * - No active sessions: creation form with game selector and session type
 *
 * Responsive layout:
 * - Mobile: Bottom sheet
 * - Desktop: Right drawer, 480px max-width
 */

'use client';

import { useState, useCallback } from 'react';

import { X, Loader2, PlayCircle, PauseCircle, Users, Hash, Plus, AlertCircle } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { GameSelector } from '@/components/agent/config/GameSelector';
import { Badge } from '@/components/ui/badge';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetFooter,
} from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import type { GameSessionDto } from '@/lib/api/schemas';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import { logger } from '@/lib/logger';

// --- Types ---

export interface SessionSheetProps {
  isOpen: boolean;
  onClose: () => void;
}

type SessionType = 'Generica' | 'Specifica';

// --- Helpers ---

function statusLabel(status: string): string {
  switch (status) {
    case 'InProgress':
      return 'Attiva';
    case 'Paused':
      return 'In Pausa';
    case 'Setup':
      return 'Setup';
    default:
      return status;
  }
}

function statusVariant(status: string): 'default' | 'secondary' | 'outline' {
  switch (status) {
    case 'InProgress':
      return 'default';
    case 'Paused':
      return 'secondary';
    default:
      return 'outline';
  }
}

// --- Sub-components ---

function SessionRow({
  session,
  onClick,
}: {
  session: GameSessionDto;
  onClick: (session: GameSessionDto) => void;
}) {
  const isPaused = session.status === 'Paused';

  return (
    <button
      type="button"
      className="w-full flex items-center gap-3 px-4 py-3 hover:bg-muted/50 transition-colors text-left"
      onClick={() => onClick(session)}
    >
      <div className="h-9 w-9 rounded-md bg-muted flex items-center justify-center shrink-0">
        {isPaused ? (
          <PauseCircle className="h-4 w-4 text-muted-foreground" />
        ) : (
          <PlayCircle className="h-4 w-4 text-primary" />
        )}
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <p className="text-sm font-medium truncate">Sessione di gioco</p>
          <Badge variant={statusVariant(session.status)} className="text-[10px] shrink-0">
            {statusLabel(session.status)}
          </Badge>
        </div>
        <div className="flex items-center gap-3 mt-0.5">
          <span className="flex items-center gap-1 text-xs text-muted-foreground">
            <Users className="h-3 w-3" />
            {session.playerCount} giocatori
          </span>
          <span className="flex items-center gap-1 text-xs text-muted-foreground font-mono">
            <Hash className="h-3 w-3" />
            {/* session code would be on a different DTO; use id prefix as fallback */}
            {session.id.slice(0, 6).toUpperCase()}
          </span>
        </div>
      </div>
    </button>
  );
}

function CreationForm({
  onSubmit,
  onCancel,
}: {
  onSubmit: (gameId: string | undefined, sessionType: SessionType) => void;
  onCancel: () => void;
}) {
  const [selectedGameId, setSelectedGameId] = useState<string | undefined>();
  const [sessionType, setSessionType] = useState<SessionType>('Generica');

  const handleGameChange = useCallback((gameId: string, _game: UserLibraryEntry | null) => {
    setSelectedGameId(gameId);
  }, []);

  const handleSubmit = useCallback(() => {
    onSubmit(selectedGameId, sessionType);
  }, [selectedGameId, sessionType, onSubmit]);

  return (
    <div className="flex-1 flex flex-col">
      {/* Form content */}
      <div className="flex-1 overflow-y-auto px-6 py-4 space-y-5">
        {/* Game selector */}
        <GameSelector
          value={selectedGameId}
          onChange={handleGameChange}
          placeholder="Seleziona un gioco..."
        />

        {/* Session type */}
        <div className="space-y-2">
          <label className="block text-sm font-medium">Tipo di sessione</label>
          <div className="flex gap-2">
            {(['Generica', 'Specifica'] as SessionType[]).map(type => (
              <button
                key={type}
                type="button"
                className={`flex-1 py-2 px-3 rounded-lg text-sm font-medium border transition-colors ${
                  sessionType === type
                    ? 'border-primary bg-primary/5 text-primary'
                    : 'border-border text-muted-foreground hover:text-foreground hover:border-foreground/30'
                }`}
                onClick={() => setSessionType(type)}
              >
                {type}
              </button>
            ))}
          </div>
          <p className="text-xs text-muted-foreground">
            {sessionType === 'Generica'
              ? 'Partita generica senza regole specifiche.'
              : 'Partita con regolamento specifico del gioco selezionato.'}
          </p>
        </div>
      </div>

      {/* Footer */}
      <SheetFooter className="border-t px-6 py-4">
        <div className="w-full flex gap-3 justify-end">
          <Button variant="outline" onClick={onCancel}>
            Annulla
          </Button>
          <Button onClick={handleSubmit}>
            <Plus className="h-4 w-4 mr-2" />
            Crea Sessione
          </Button>
        </div>
      </SheetFooter>
    </div>
  );
}

// --- Main Component ---

export function SessionSheet({ isOpen, onClose }: SessionSheetProps) {
  const router = useRouter();
  const [showCreationForm, setShowCreationForm] = useState(false);

  const { data: sessionsData, isLoading, error } = useActiveSessions(10, isOpen);

  const activeSessions = sessionsData?.sessions ?? [];
  const hasSessions = activeSessions.length > 0;

  // Show creation form when no sessions OR user explicitly clicks "Nuova sessione"
  const shouldShowForm = showCreationForm || (!isLoading && !error && !hasSessions);

  const handleSessionClick = useCallback(
    (session: GameSessionDto) => {
      onClose();
      router.push(`/sessions/${session.id}`);
    },
    [onClose, router]
  );

  const handleCreateSubmit = useCallback(
    (gameId: string | undefined, sessionType: SessionType) => {
      // Log for now — real API integration in future issue
      logger.debug(`[SessionSheet] Create session: gameId=${gameId} type=${sessionType}`);
      onClose();
    },
    [onClose]
  );

  const handleClose = useCallback(() => {
    setShowCreationForm(false);
    onClose();
  }, [onClose]);

  // --- Render ---

  return (
    <Sheet open={isOpen} onOpenChange={open => !open && handleClose()}>
      <SheetContent side="right" className="w-full sm:max-w-[480px] flex flex-col p-0">
        {/* Header */}
        <SheetHeader className="border-b px-6 py-4">
          <div className="flex items-center justify-between">
            <SheetTitle className="text-lg font-semibold">
              {shouldShowForm ? 'Nuova Sessione' : 'Sessioni Attive'}
            </SheetTitle>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8"
              onClick={handleClose}
              aria-label="Chiudi"
            >
              <X className="h-4 w-4" />
            </Button>
          </div>
        </SheetHeader>

        {/* Loading state */}
        {isLoading && (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        )}

        {/* Error state */}
        {error && !isLoading && (
          <div className="flex items-center gap-2 mx-4 mt-4 p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
            <AlertCircle className="h-4 w-4 text-red-500 shrink-0" />
            <p className="text-sm text-red-700 dark:text-red-300">
              Impossibile caricare le sessioni. Riprova.
            </p>
          </div>
        )}

        {/* Sessions list */}
        {!isLoading && !error && !shouldShowForm && hasSessions && (
          <>
            <div className="flex-1 overflow-y-auto divide-y">
              {activeSessions.map(session => (
                <SessionRow key={session.id} session={session} onClick={handleSessionClick} />
              ))}
            </div>

            {/* Footer with "Nuova sessione" button */}
            <SheetFooter className="border-t px-6 py-4">
              <Button
                variant="outline"
                className="w-full"
                onClick={() => setShowCreationForm(true)}
              >
                <Plus className="h-4 w-4 mr-2" />
                Nuova sessione
              </Button>
            </SheetFooter>
          </>
        )}

        {/* Creation form */}
        {!isLoading && !error && shouldShowForm && (
          <CreationForm
            onSubmit={handleCreateSubmit}
            onCancel={hasSessions ? () => setShowCreationForm(false) : handleClose}
          />
        )}
      </SheetContent>
    </Sheet>
  );
}
