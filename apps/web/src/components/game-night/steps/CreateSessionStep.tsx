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

function sameName(a: string, b: string): boolean {
  return a.trim().toLowerCase() === b.trim().toLowerCase();
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
  const canCreate = validPlayers.length >= 2 && !isCreating;

  // #4750: the creator is the first player, linked to their account. Here the link is not a
  // state of its own: a seat is the user's while it carries their name. Renaming it unlinks it,
  // and «Io non gioco» empties it.
  const self = useSelfPlayer();
  const selfSlotId = self
    ? players.find(p => p.name.trim() && sameName(p.name, self.displayName))?.id
    : undefined;
  const hasRoomForSelf = canAddPlayer || players.some(p => !p.name.trim());

  // Seeded once, into the first seat if it is still empty: after «Io non gioco» the seats are
  // the user's.
  const seededSelf = useRef(false);
  useEffect(() => {
    if (!self || seededSelf.current) return;
    seededSelf.current = true;
    setPlayers(prev =>
      prev[0]?.name === '' ? [{ ...prev[0], name: self.displayName }, ...prev.slice(1)] : prev
    );
  }, [self]);

  const handleOptOut = useCallback(() => {
    setPlayers(prev => prev.map(p => (p.id === selfSlotId ? { ...p, name: '' } : p)));
  }, [selfSlotId]);

  const handleAddSelf = useCallback(() => {
    if (!self) return;
    setPlayers(prev => {
      const emptyIndex = prev.findIndex(p => !p.name.trim());
      if (emptyIndex >= 0) {
        return prev.map((p, i) => (i === emptyIndex ? { ...p, name: self.displayName } : p));
      }
      return prev.length < 8 ? [...prev, createSlot(self.displayName)] : prev;
    });
  }, [self]);

  const handlePlayerChange = useCallback((slotId: string, value: string) => {
    setPlayers(prev => prev.map(p => (p.id === slotId ? { ...p, name: value } : p)));
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
        userId: self && p.id === selfSlotId ? self.userId : undefined,
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
  }, [canCreate, gameId, gameTitle, validPlayers, self, selfSlotId, onSessionCreated]);

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
            {player.id === selfSlotId && (
              <>
                <SelfBadge />
                <OptOutButton onClick={handleOptOut} className="flex-shrink-0" />
              </>
            )}
            {canRemovePlayer && player.id !== selfSlotId && (
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
      {self && !selfSlotId && (
        <AddSelfButton onClick={handleAddSelf} disabled={!hasRoomForSelf} className="w-full" />
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
