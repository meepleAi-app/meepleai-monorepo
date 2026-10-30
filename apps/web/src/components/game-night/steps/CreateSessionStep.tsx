/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 will introduce primitives encoding bg via className. */
'use client';

/**
 * CreateSessionStep — Step 3 of GameNightWizard.
 *
 * Player name inputs (2-8 players) + create session.
 *
 * Issue #123 — Game Night Quick Start Wizard
 */

import { useCallback, useEffect, useId, useRef, useState } from 'react';

import { Loader2, Plus, Trash2, Users } from 'lucide-react';

import { AddSelfButton, OptOutButton, SelfBadge } from '@/components/session/SelfPlayerControls';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { linkedFirst, useSelfPlayer } from '@/hooks/useSelfPlayer';
import { api } from '@/lib/api';
import { API_PLAYER_COLORS } from '@/lib/sessions/player-colors';
import { duplicatePlayerNameMessage, findDuplicatePlayerName } from '@/lib/sessions/player-names';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

interface CreateSessionStepProps {
  gameId?: string;
  gameTitle: string;
  onSessionCreated: (sessionId: string) => void;
}

interface PlayerSlot {
  id: string;
  name: string;
  /** Set only on the creator's own seat (#4750); the name does not decide it. */
  userId?: string;
}

// #4107: qui c'era una copia locale di otto nomi. Il vocabolario dell'API ne ha dieci, e
// tenerne una copia parziale significava che questo flusso non poteva assegnare White e Black
// — con l'aggravante che la divergenza era invisibile, perche' nulla confrontava le due liste.
// Ora la fonte e' una, e `player-colors.test.ts` la verifica contro l'enum del dominio.
const PLAYER_COLORS = API_PLAYER_COLORS;

// ============================================================================
// Helpers
// ============================================================================

let slotCounter = 0;
function createSlot(name = ''): PlayerSlot {
  return { id: `slot-${++slotCounter}`, name };
}

// ============================================================================
// Component
// ============================================================================

export function CreateSessionStep({ gameId, gameTitle, onSessionCreated }: CreateSessionStepProps) {
  const formId = useId();
  const [players, setPlayers] = useState<PlayerSlot[]>(() => [createSlot(), createSlot()]);
  const [isCreating, setIsCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canAddPlayer = players.length < 8;
  const canRemovePlayer = players.length > 2;
  const validPlayers = players.filter(p => p.name.trim().length > 0);
  // The server refuses two players with the same name: say so before the session exists.
  const duplicateName = findDuplicatePlayerName(players.map(p => p.name));
  const canCreate = validPlayers.length >= 2 && !duplicateName && !isCreating;

  // #4750: the creator is the first player, linked to their account. The link is a state of the
  // seat (`userId`), not something read from the name: editing the name keeps it (a nickname is
  // still the same person), and writing the same name in another seat does not create it. «Io
  // non gioco» removes it and empties the seat; so does emptying the name, an empty seat is
  // nobody's.
  const self = useSelfPlayer();
  const isSelfSeat = useCallback(
    (seat: PlayerSlot) => !!self && seat.userId === self.userId,
    [self]
  );
  const selfInSeats = players.some(isSelfSeat);
  const hasRoomForSelf = canAddPlayer || players.some(p => !p.name.trim());
  const seatsFullNoticeId = `${formId}-seats-full`;

  // Seeded once, into the first seat and only if it is still empty: a name already written
  // stays, and after «Io non gioco» the seats are the user's.
  const seededSelf = useRef(false);
  useEffect(() => {
    if (!self || seededSelf.current) return;
    seededSelf.current = true;
    setPlayers(prev =>
      prev[0]?.name === ''
        ? [{ ...prev[0], name: self.displayName, userId: self.userId }, ...prev.slice(1)]
        : prev
    );
  }, [self]);

  const handleOptOut = useCallback(() => {
    setPlayers(prev => prev.map(p => (isSelfSeat(p) ? { ...p, name: '', userId: undefined } : p)));
  }, [isSelfSeat]);

  const handleAddSelf = useCallback(() => {
    if (!self) return;
    setPlayers(prev => {
      const seat = { name: self.displayName, userId: self.userId };
      const emptyIndex = prev.findIndex(p => !p.name.trim());
      return emptyIndex >= 0
        ? prev.map((p, i) => (i === emptyIndex ? { ...p, ...seat } : p))
        : [...prev, { ...createSlot(), ...seat }];
    });
  }, [self]);

  const handlePlayerChange = useCallback((slotId: string, value: string) => {
    setPlayers(prev =>
      prev.map(p =>
        p.id === slotId ? { ...p, name: value, userId: value.trim() ? p.userId : undefined } : p
      )
    );
  }, []);

  const handleAddPlayer = useCallback(() => {
    if (canAddPlayer) {
      setPlayers(prev => [...prev, createSlot()]);
    }
  }, [canAddPlayer]);

  const handleRemovePlayer = useCallback(
    (slotId: string) => {
      if (canRemovePlayer) {
        setPlayers(prev => prev.filter(p => p.id !== slotId));
      }
    },
    [canRemovePlayer]
  );

  const handleCreate = useCallback(async () => {
    if (!canCreate) return;
    setIsCreating(true);
    setError(null);

    try {
      // Create session — gameId is optional, gameName is always set
      const sessionId = await api.liveSessions.createSession({
        ...(gameId ? { gameId } : {}),
        gameName: gameTitle,
      });

      // Add players. Colors follow the seats; the linked creator goes first whichever seat they
      // are in, because the server makes the first player the Host (#4750).
      const entries = validPlayers.map((p, i) => ({
        name: p.name,
        color: PLAYER_COLORS[i % PLAYER_COLORS.length],
        userId: p.userId,
      }));
      for (const entry of linkedFirst(entries)) {
        await api.liveSessions.addPlayer(sessionId, {
          displayName: entry.name,
          color: entry.color,
          ...(entry.userId ? { userId: entry.userId } : {}),
        });
      }

      // Start the session
      await api.liveSessions.startSession(sessionId);

      onSessionCreated(sessionId);
    } catch {
      setError('Errore nella creazione della sessione. Riprova.');
      setIsCreating(false);
    }
  }, [canCreate, gameId, gameTitle, validPlayers, onSessionCreated]);

  return (
    <div className="space-y-4" data-testid="create-session-step">
      <div>
        <h3 className="font-quicksand font-bold text-lg text-foreground">Giocatori</h3>
        <p className="text-sm text-muted-foreground mt-1">
          Aggiungi i giocatori per <strong>{gameTitle}</strong> (min 2, max 8).
        </p>
      </div>

      <div className="space-y-2">
        {players.map((player, index) => (
          <div key={player.id} className="flex items-center gap-2">
            <div
              className="h-8 w-8 rounded-full flex-shrink-0 flex items-center justify-center text-xs font-bold text-white"
              style={{
                backgroundColor:
                  index === 0
                    ? '#ef4444'
                    : index === 1
                      ? '#3b82f6'
                      : index === 2
                        ? '#22c55e'
                        : index === 3
                          ? '#eab308'
                          : index === 4
                            ? '#a855f7'
                            : index === 5
                              ? '#f97316'
                              : index === 6
                                ? '#ec4899'
                                : '#14b8a6',
              }}
            >
              {index + 1}
            </div>
            <Input
              placeholder={`Giocatore ${index + 1}`}
              value={player.name}
              onChange={e => handlePlayerChange(player.id, e.target.value)}
              data-testid={`player-input-${index}`}
              id={`${formId}-player-${index}`}
            />
            {isSelfSeat(player) && (
              <>
                <SelfBadge />
                <OptOutButton onClick={handleOptOut} className="flex-shrink-0" />
              </>
            )}
            {canRemovePlayer && !isSelfSeat(player) && (
              <Button
                variant="ghost"
                size="icon"
                onClick={() => handleRemovePlayer(player.id)}
                className="flex-shrink-0 text-muted-foreground hover:text-destructive"
                aria-label={`Rimuovi giocatore ${index + 1}`}
              >
                <Trash2 className="h-4 w-4" />
              </Button>
            )}
          </div>
        ))}
      </div>

      {duplicateName && (
        <p className="text-sm text-destructive" role="alert">
          {duplicatePlayerNameMessage(duplicateName)}
        </p>
      )}

      {canAddPlayer && (
        <Button
          variant="outline"
          onClick={handleAddPlayer}
          className="w-full"
          data-testid="add-player-button"
        >
          <Plus className="h-4 w-4 mr-2" aria-hidden="true" />
          Aggiungi giocatore
        </Button>
      )}

      {/* The creator opted out and wants back in (#4750) */}
      {self && !selfInSeats && (
        <div className="space-y-1">
          <AddSelfButton
            onClick={handleAddSelf}
            disabled={!hasRoomForSelf}
            describedBy={hasRoomForSelf ? undefined : seatsFullNoticeId}
            className="w-full"
          />
          {!hasRoomForSelf && (
            <p id={seatsFullNoticeId} className="text-xs text-muted-foreground">
              I posti sono pieni: libera un posto per aggiungerti.
            </p>
          )}
        </div>
      )}

      {error && (
        <p className="text-sm text-destructive text-center" data-testid="create-session-error">
          {error}
        </p>
      )}

      <Button
        onClick={handleCreate}
        disabled={!canCreate}
        className={cn('w-full bg-amber-600 hover:bg-amber-700 text-white')}
        data-testid="create-session-button"
      >
        {isCreating ? (
          <>
            <Loader2 className="h-4 w-4 mr-2 animate-spin" aria-hidden="true" />
            Creazione in corso...
          </>
        ) : (
          <>
            <Users className="h-4 w-4 mr-2" aria-hidden="true" />
            Inizia Partita ({validPlayers.length} giocatori)
          </>
        )}
      </Button>
    </div>
  );
}
