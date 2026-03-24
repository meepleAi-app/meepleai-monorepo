/**
 * GuestJoinView — Interactive guest session view
 *
 * Game Night Improvvisata — Task 18
 *
 * Testable inner component that receives a plain `inviteToken` string.
 * All interactive logic lives here; the parent page.tsx only unwraps params.
 */

'use client';

import { useEffect, useState, useCallback } from 'react';

import { Loader2, Users } from 'lucide-react';

import { GuestScoreProposal } from '@/components/session/live/GuestScoreProposal';
import { ScoreBoard } from '@/components/session/live/ScoreBoard';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

// ─── Constants ────────────────────────────────────────────────────────────────

const STORAGE_KEY = 'improvvisata_participant_token';
const STORAGE_NAME_KEY = 'improvvisata_guest_name';

// ─── Types ────────────────────────────────────────────────────────────────────

type JoinState = 'loading' | 'name-entry' | 'joined' | 'error';

interface SessionInfo {
  sessionId: string;
  gameName: string;
  hostName: string;
  inviteCode: string;
}

// ─── Storage helpers ──────────────────────────────────────────────────────────

function getSavedToken(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function getSavedName(): string | null {
  try {
    return localStorage.getItem(STORAGE_NAME_KEY);
  } catch {
    return null;
  }
}

function saveParticipantData(token: string, name: string) {
  try {
    localStorage.setItem(STORAGE_KEY, token);
    localStorage.setItem(STORAGE_NAME_KEY, name);
  } catch {
    // Storage unavailable — continue without persistence
  }
}

// ─── Component ────────────────────────────────────────────────────────────────

export interface GuestJoinViewProps {
  inviteToken: string;
}

export function GuestJoinView({ inviteToken }: GuestJoinViewProps) {
  const [joinState, setJoinState] = useState<JoinState>('loading');
  const [sessionInfo, setSessionInfo] = useState<SessionInfo | null>(null);
  const [guestName, setGuestName] = useState('');
  const [inputName, setInputName] = useState('');
  const [nameError, setNameError] = useState<string | null>(null);
  const [isJoining, setIsJoining] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const setSession = useLiveSessionStore(s => s.setSession);
  const addProposal = useLiveSessionStore(s => s.addProposal);

  // ── Load session ─────────────────────────────────────────────────────────────

  const loadSession = useCallback(async () => {
    setJoinState('loading');
    try {
      const res = await fetch(`/api/v1/live-sessions/code/${encodeURIComponent(inviteToken)}`);
      if (!res.ok) {
        setErrorMessage('Link non valido o sessione non trovata.');
        setJoinState('error');
        return;
      }

      const data = (await res.json()) as {
        id: string;
        gameName: string;
        inviteCode: string;
        players?: Array<{ displayName: string; isHost?: boolean; isOnline?: boolean; id: string }>;
        scores?: Record<string, number>;
        status?: string;
        currentTurn?: number;
      };

      const info: SessionInfo = {
        sessionId: data.id,
        gameName: data.gameName ?? 'Partita',
        hostName: data.players?.find(p => p.isHost)?.displayName ?? 'Host',
        inviteCode: data.inviteCode,
      };
      setSessionInfo(info);

      setSession({
        sessionId: data.id,
        gameName: data.gameName ?? '',
        status: (data.status as 'InProgress' | 'Paused' | 'Completed') ?? 'InProgress',
        currentTurn: data.currentTurn ?? 1,
        players:
          data.players?.map(p => ({
            id: p.id,
            name: p.displayName,
            isHost: p.isHost ?? false,
            isOnline: p.isOnline ?? false,
          })) ?? [],
        scores: data.scores ?? {},
      });

      // Auto-rejoin if valid saved token
      const savedToken = getSavedToken();
      const savedName = getSavedName();

      if (savedToken && savedName) {
        const tokenRes = await fetch(
          `/api/v1/live-sessions/${encodeURIComponent(data.id)}/guest/validate`,
          {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ participantToken: savedToken }),
          }
        );
        if (tokenRes.ok) {
          setGuestName(savedName);
          setJoinState('joined');
          return;
        }
        try {
          localStorage.removeItem(STORAGE_KEY);
          localStorage.removeItem(STORAGE_NAME_KEY);
        } catch {
          // ignore
        }
      }

      setJoinState('name-entry');
    } catch {
      setErrorMessage('Errore di connessione. Riprova tra qualche secondo.');
      setJoinState('error');
    }
  }, [inviteToken, setSession]);

  useEffect(() => {
    loadSession();
  }, [loadSession]);

  // ── Join ──────────────────────────────────────────────────────────────────────

  async function handleJoin(e: React.FormEvent) {
    e.preventDefault();
    const trimmed = inputName.trim();
    if (!trimmed) {
      setNameError('Inserisci il tuo nome');
      return;
    }
    if (trimmed.length < 2) {
      setNameError('Il nome deve avere almeno 2 caratteri');
      return;
    }
    if (!sessionInfo) return;

    setNameError(null);
    setIsJoining(true);

    try {
      const res = await fetch(
        `/api/v1/live-sessions/${encodeURIComponent(sessionInfo.sessionId)}/guest/join`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ displayName: trimmed }),
        }
      );

      if (!res.ok) {
        const body = (await res.json().catch(() => ({}))) as { message?: string };
        setNameError(body.message ?? 'Impossibile unirsi alla sessione');
        return;
      }

      const joinData = (await res.json()) as { participantToken?: string };
      if (joinData.participantToken) {
        saveParticipantData(joinData.participantToken, trimmed);
      }

      setGuestName(trimmed);
      setJoinState('joined');
    } catch {
      setNameError('Errore di connessione. Riprova.');
    } finally {
      setIsJoining(false);
    }
  }

  // ── Score proposal ────────────────────────────────────────────────────────────

  async function handlePropose(delta: number) {
    if (!sessionInfo) return;

    const res = await fetch(
      `/api/v1/live-sessions/${encodeURIComponent(sessionInfo.sessionId)}/scores/propose`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ playerName: guestName, delta }),
      }
    );

    if (!res.ok) throw new Error('Proposta rifiutata dal server');

    const data = (await res.json()) as { proposalId?: string };
    addProposal({
      id: data.proposalId ?? `proposal-${Date.now()}`,
      playerName: guestName,
      delta,
      timestamp: Date.now(),
    });
  }

  // ── Loading ───────────────────────────────────────────────────────────────────

  if (joinState === 'loading') {
    return (
      <main className="min-h-screen bg-gradient-to-br from-amber-50 to-orange-50 flex items-center justify-center p-4">
        <div className="flex flex-col items-center gap-3 text-gray-600">
          <Loader2 className="h-8 w-8 animate-spin text-amber-500" />
          <p className="font-nunito" data-testid="loading-text">
            Caricamento sessione...
          </p>
        </div>
      </main>
    );
  }

  // ── Error ─────────────────────────────────────────────────────────────────────

  if (joinState === 'error') {
    return (
      <main className="min-h-screen bg-gradient-to-br from-amber-50 to-orange-50 flex items-center justify-center p-4">
        <div className="max-w-sm w-full text-center space-y-4">
          <div className="h-16 w-16 mx-auto rounded-2xl bg-red-100 flex items-center justify-center">
            <Users className="h-8 w-8 text-red-500" />
          </div>
          <h1 className="text-xl font-quicksand font-bold text-gray-900">Sessione non trovata</h1>
          <p className="text-sm font-nunito text-gray-600">
            {errorMessage ?? 'Il link potrebbe essere scaduto o non valido.'}
          </p>
          <Button onClick={() => loadSession()} variant="outline" className="w-full">
            Riprova
          </Button>
        </div>
      </main>
    );
  }

  // ── Name Entry ────────────────────────────────────────────────────────────────

  if (joinState === 'name-entry' && sessionInfo) {
    return (
      <main className="min-h-screen bg-gradient-to-br from-amber-50 to-orange-50 flex items-center justify-center p-4">
        <div className="max-w-sm w-full space-y-6">
          <div className="text-center space-y-2">
            <div className="h-16 w-16 mx-auto rounded-2xl bg-amber-100 flex items-center justify-center">
              <Users className="h-8 w-8 text-amber-600" />
            </div>
            <h1 className="text-2xl font-quicksand font-bold text-gray-900">
              Unisciti alla partita
            </h1>
            <p className="text-sm font-nunito text-gray-600">
              <span className="font-semibold text-gray-900">{sessionInfo.gameName}</span> ospitata
              da <span className="font-semibold text-gray-900">{sessionInfo.hostName}</span>
            </p>
            <Badge variant="outline" className="font-mono text-lg px-4 py-1">
              {sessionInfo.inviteCode}
            </Badge>
          </div>

          <form onSubmit={handleJoin} className="space-y-3">
            <div className="space-y-1.5">
              <label htmlFor="guest-name" className="text-sm font-nunito font-medium text-gray-700">
                Il tuo nome
              </label>
              <Input
                id="guest-name"
                type="text"
                value={inputName}
                onChange={e => {
                  setInputName(e.target.value);
                  setNameError(null);
                }}
                placeholder="Come ti chiami?"
                autoFocus
                autoComplete="name"
                maxLength={40}
                disabled={isJoining}
                className="text-center text-lg"
                aria-describedby={nameError ? 'name-error' : undefined}
              />
              {nameError && (
                <p id="name-error" className="text-xs text-red-600 font-nunito" role="alert">
                  {nameError}
                </p>
              )}
            </div>

            <Button
              type="submit"
              className="w-full bg-amber-500 hover:bg-amber-600 text-white font-quicksand font-semibold"
              disabled={isJoining || !inputName.trim()}
            >
              {isJoining ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Connessione...
                </>
              ) : (
                'Entra nella partita'
              )}
            </Button>
          </form>
        </div>
      </main>
    );
  }

  // ── Joined ────────────────────────────────────────────────────────────────────

  if (joinState === 'joined' && sessionInfo) {
    return (
      <main className="min-h-screen bg-gradient-to-br from-amber-50 to-orange-50 p-4">
        <div className="max-w-sm mx-auto space-y-4">
          <div className="text-center pt-4 pb-2">
            <h1 className="text-xl font-quicksand font-bold text-gray-900">
              {sessionInfo.gameName}
            </h1>
            <p className="text-sm font-nunito text-gray-500">
              Giochi come <span className="font-semibold text-amber-700">{guestName}</span>
            </p>
          </div>

          <div className="rounded-2xl bg-white/70 backdrop-blur-md border border-white/40 shadow-sm overflow-hidden">
            <ScoreBoard sessionId={sessionInfo.sessionId} isHost={false} />
          </div>

          <GuestScoreProposal guestName={guestName} onPropose={handlePropose} />
        </div>
      </main>
    );
  }

  return null;
}
