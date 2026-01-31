# 🎮 Session Toolkit - Usage Examples

Complete code examples for integrating the Game Session Toolkit into MeepleAI.

---

## 1. Generic Toolkit Page (Navbar Link)

**Route**: `/toolkit`
**Purpose**: Standalone scorekeeper without game association

```tsx
'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import {
  SessionHeader,
  Scoreboard,
  ScoreInput,
  type Session,
  type Participant,
  type ScoreEntry
} from '@/components/session';

export default function GenericToolkitPage() {
  const router = useRouter();
  const [session, setSession] = useState<Session | null>(null);
  const [participants, setParticipants] = useState<Participant[]>([]);
  const [scores, setScores] = useState<ScoreEntry[]>([]);

  // Create new generic session
  const handleCreateSession = async () => {
    const response = await fetch('/api/v1/game-sessions', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        sessionType: 'Generic',
        participants: [
          { displayName: 'Me', isOwner: true },
          { displayName: 'Player 2', isOwner: false }
        ]
      })
    });

    const data = await response.json();
    setSession(data.session);
    setParticipants(data.participants);

    // Redirect to active session
    router.push(`/toolkit/${data.session.id}`);
  };

  // Join existing session by code
  const handleJoinSession = async (code: string) => {
    const response = await fetch(`/api/v1/game-sessions/join/${code}`, {
      method: 'POST'
    });
    const data = await response.json();
    router.push(`/toolkit/${data.sessionId}`);
  };

  return (
    <div className="container mx-auto px-4 py-8">
      <h1 className="text-3xl font-bold mb-6">Game Toolkit</h1>

      {/* Create or Join Session UI */}
      <div className="grid gap-4 md:grid-cols-2">
        <button
          onClick={handleCreateSession}
          className="rounded-lg border-2 border-dashed border-amber-600 bg-amber-50 dark:bg-amber-950/20 p-8 hover:bg-amber-100 dark:hover:bg-amber-950/30"
        >
          <span className="text-4xl mb-2">🎲</span>
          <h2 className="text-xl font-bold">Create New Session</h2>
          <p className="text-sm text-slate-600 dark:text-slate-400">
            Start a new scorekeeper for any board game
          </p>
        </button>

        <div className="rounded-lg border p-6">
          <h2 className="font-bold mb-4">Join Session</h2>
          <form onSubmit={(e) => {
            e.preventDefault();
            const formData = new FormData(e.currentTarget);
            handleJoinSession(formData.get('code') as string);
          }}>
            <input
              name="code"
              type="text"
              placeholder="Enter code (e.g., ABC123)"
              className="w-full rounded border px-4 py-2"
              maxLength={6}
            />
            <button type="submit" className="mt-2 w-full rounded bg-amber-600 px-4 py-2 text-white">
              Join
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
```

---

## 2. Active Session Page (Generic)

**Route**: `/toolkit/[sessionId]`
**Features**: Real-time SSE sync, score entry, participant management

```tsx
'use client';

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import {
  SessionHeader,
  Scoreboard,
  ScoreInput,
  ParticipantCard,
  type Session,
  type Participant,
  type ScoreEntry,
  type ScoreboardData,
  type SyncStatus
} from '@/components/session';

export default function ActiveSessionPage() {
  const params = useParams();
  const sessionId = params.sessionId as string;

  const [session, setSession] = useState<Session | null>(null);
  const [participants, setParticipants] = useState<Participant[]>([]);
  const [scores, setScores] = useState<ScoreEntry[]>([]);
  const [syncStatus, setSyncStatus] = useState<SyncStatus>('idle');

  // Initialize session data
  useEffect(() => {
    const fetchSession = async () => {
      const response = await fetch(`/api/v1/game-sessions/${sessionId}/details`);
      const data = await response.json();

      setSession(data.session);
      setParticipants(data.participants);
      setScores(data.scores);
    };

    fetchSession();
  }, [sessionId]);

  // Real-time SSE subscription
  useEffect(() => {
    const eventSource = new EventSource(`/api/v1/game-sessions/${sessionId}/stream`);

    eventSource.addEventListener('ScoreUpdatedEvent', (e) => {
      const event = JSON.parse(e.data);
      setScores((prev) => [...prev, event.scoreEntry]);
      setSyncStatus('synced');
      setTimeout(() => setSyncStatus('idle'), 2000);
    });

    eventSource.addEventListener('NoteAddedEvent', (e) => {
      // Handle note updates
    });

    eventSource.onerror = () => {
      setSyncStatus('error');
      eventSource.close();
    };

    return () => eventSource.close();
  }, [sessionId]);

  // Submit score
  const handleScoreSubmit = async (data: {
    participantId: string;
    roundNumber: number | null;
    category: string | null;
    scoreValue: number;
  }) => {
    setSyncStatus('saving');

    try {
      const response = await fetch(`/api/v1/game-sessions/${sessionId}/scores`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
      });

      if (!response.ok) throw new Error('Failed to save score');

      // Optimistic update (real sync via SSE)
      const newScore: ScoreEntry = {
        id: Date.now().toString(),
        ...data,
        timestamp: new Date(),
        createdBy: 'current-user-id'
      };
      setScores((prev) => [...prev, newScore]);
    } catch (error) {
      setSyncStatus('error');
      console.error('Score submission failed:', error);
    }
  };

  // Undo last score
  const handleUndo = async () => {
    if (scores.length === 0) return;

    const lastScore = scores[scores.length - 1];
    await fetch(`/api/v1/game-sessions/${sessionId}/scores/${lastScore.id}`, {
      method: 'DELETE'
    });

    setScores((prev) => prev.slice(0, -1));
  };

  // Finalize session
  const handleFinalize = async () => {
    const ranks = participants.reduce((acc, p, idx) => {
      acc[p.id] = idx + 1;
      return acc;
    }, {} as Record<string, number>);

    await fetch(`/api/v1/game-sessions/${sessionId}/finalize`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ finalRanks: ranks })
    });

    setSession((prev) => prev ? { ...prev, status: 'Finalized' } : null);
  };

  if (!session) return <div>Loading...</div>;

  const scoreboardData: ScoreboardData = {
    participants,
    scores,
    rounds: [],
    categories: []
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 to-amber-50/20 dark:from-slate-950 dark:to-slate-900">
      <SessionHeader
        session={session}
        onFinalize={handleFinalize}
      />

      <div className="mx-auto max-w-7xl px-4 py-6">
        <div className="grid gap-6 lg:grid-cols-3">
          {/* Participants Sidebar */}
          <div className="lg:col-span-1">
            <h2 className="mb-4 font-bold text-sm uppercase tracking-wider text-slate-600 dark:text-slate-400">
              Players
            </h2>
            <div className="space-y-3">
              {participants.map((p) => (
                <ParticipantCard key={p.id} participant={p} variant="full" />
              ))}
            </div>
          </div>

          {/* Scoreboard */}
          <div className="lg:col-span-2">
            <Scoreboard data={scoreboardData} isRealTime={true} variant="compact" />
          </div>
        </div>
      </div>

      {/* Sticky Score Input */}
      <ScoreInput
        participants={participants}
        rounds={[]}
        onSubmit={handleScoreSubmit}
        onUndo={handleUndo}
        syncStatus={syncStatus}
      />
    </div>
  );
}
```

---

## 3. Game-Specific Toolkit (Library Integration)

**Route**: `/library/games/[gameId]/toolkit`
**Purpose**: Template scoresheet for specific board game

```tsx
'use client';

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import {
  SessionHeader,
  Scoreboard,
  ScoreInput,
  type ScoreboardData
} from '@/components/session';

// Game-specific templates
const GAME_TEMPLATES: Record<string, {
  rounds: number[];
  categories: string[];
  scoringRules?: string;
}> = {
  '7-wonders': {
    rounds: [1, 2, 3],
    categories: ['Military', 'Science', 'Commerce', 'Wonders', 'Coins', 'Guilds'],
    scoringRules: 'Final score = sum of all categories'
  },
  'splendor': {
    rounds: [],
    categories: ['Nobles', 'Cards', 'Tokens'],
    scoringRules: 'First to 15 points wins'
  },
  'catan': {
    rounds: [],
    categories: ['Settlements', 'Cities', 'Longest Road', 'Largest Army', 'Victory Points'],
    scoringRules: 'First to 10 points wins'
  }
};

export default function GameSpecificToolkitPage() {
  const params = useParams();
  const gameId = params.gameId as string;

  const [game, setGame] = useState<any>(null);
  const [session, setSession] = useState<any>(null);
  const [participants, setParticipants] = useState<any[]>([]);
  const [scores, setScores] = useState<any[]>([]);

  useEffect(() => {
    // Fetch game details
    const fetchGame = async () => {
      const response = await fetch(`/api/v1/games/${gameId}`);
      const data = await response.json();
      setGame(data);
    };

    fetchGame();
  }, [gameId]);

  const handleCreateSession = async () => {
    const template = GAME_TEMPLATES[game.slug] || { rounds: [], categories: [] };

    const response = await fetch('/api/v1/game-sessions', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        gameId,
        sessionType: 'GameSpecific',
        participants: [{ displayName: 'Me', isOwner: true }],
        template
      })
    });

    const data = await response.json();
    setSession(data.session);
    setParticipants(data.participants);
  };

  if (!game) return <div>Loading game...</div>;

  if (!session) {
    return (
      <div className="container mx-auto px-4 py-8">
        <div className="mb-6 flex items-center gap-4">
          <div className="text-5xl">{game.icon}</div>
          <div>
            <h1 className="text-3xl font-bold">{game.name}</h1>
            <p className="text-sm text-slate-600 dark:text-slate-400">
              Use the scorekeeper designed for this game
            </p>
          </div>
        </div>

        <button
          onClick={handleCreateSession}
          className="rounded-lg bg-amber-600 px-6 py-3 text-white font-bold hover:bg-amber-700"
        >
          Start {game.name} Session
        </button>

        {/* Show scoring rules */}
        {GAME_TEMPLATES[game.slug]?.scoringRules && (
          <div className="mt-6 rounded-lg border bg-amber-50 dark:bg-amber-950/20 p-4">
            <h3 className="font-bold mb-2">Scoring Rules</h3>
            <p className="text-sm">{GAME_TEMPLATES[game.slug].scoringRules}</p>
          </div>
        )}
      </div>
    );
  }

  const template = GAME_TEMPLATES[game.slug] || { rounds: [], categories: [] };
  const scoreboardData: ScoreboardData = {
    participants,
    scores,
    rounds: template.rounds,
    categories: template.categories
  };

  return (
    <div className="min-h-screen">
      <SessionHeader session={session} />

      <div className="mx-auto max-w-7xl px-4 py-6">
        <Scoreboard data={scoreboardData} isRealTime={true} variant="full" />
      </div>

      <ScoreInput
        participants={participants}
        rounds={template.rounds}
        categories={template.categories}
        onSubmit={async (data) => {
          await fetch(`/api/v1/game-sessions/${session.id}/scores`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
          });
        }}
      />
    </div>
  );
}
```

---

## 4. Session History Page

**Route**: `/toolkit/history`
**Purpose**: View past sessions with stats

```tsx
'use client';

import { useEffect, useState } from 'react';
import { Trophy, Calendar, Users } from 'lucide-react';

interface SessionSummary {
  sessionId: string;
  sessionDate: Date;
  gameName?: string;
  gameIcon?: string;
  participants: string[];
  winner: string;
  finalScores: Record<string, number>;
}

export default function SessionHistoryPage() {
  const [history, setHistory] = useState<SessionSummary[]>([]);

  useEffect(() => {
    const fetchHistory = async () => {
      const response = await fetch('/api/v1/game-sessions/history?limit=20');
      const data = await response.json();
      setHistory(data.sessions);
    };

    fetchHistory();
  }, []);

  return (
    <div className="container mx-auto px-4 py-8">
      <h1 className="text-3xl font-bold mb-6">Session History</h1>

      <div className="grid gap-4">
        {history.map((session) => (
          <div
            key={session.sessionId}
            className="rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800/50 p-6 hover:shadow-lg transition-shadow"
          >
            <div className="flex items-start justify-between gap-4">
              {/* Game Info */}
              <div className="flex items-center gap-3">
                {session.gameIcon && (
                  <div className="text-4xl">{session.gameIcon}</div>
                )}
                <div>
                  <h3 className="font-bold text-lg">
                    {session.gameName || 'Generic Session'}
                  </h3>
                  <div className="flex items-center gap-4 mt-1 text-sm text-slate-600 dark:text-slate-400">
                    <span className="flex items-center gap-1">
                      <Calendar className="h-4 w-4" />
                      {new Date(session.sessionDate).toLocaleDateString()}
                    </span>
                    <span className="flex items-center gap-1">
                      <Users className="h-4 w-4" />
                      {session.participants.length} players
                    </span>
                  </div>
                </div>
              </div>

              {/* Winner Badge */}
              <div className="flex items-center gap-2 rounded-lg bg-gradient-to-br from-amber-100 to-orange-100 dark:from-amber-950/30 dark:to-orange-950/20 px-4 py-2">
                <Trophy className="h-5 w-5 text-amber-600 dark:text-amber-400" />
                <div className="text-right">
                  <div className="text-xs font-medium text-slate-600 dark:text-slate-400">
                    Winner
                  </div>
                  <div className="font-bold text-amber-900 dark:text-amber-400">
                    {session.winner}
                  </div>
                </div>
              </div>
            </div>

            {/* Final Scores */}
            <div className="mt-4 grid grid-cols-2 sm:grid-cols-4 gap-2">
              {Object.entries(session.finalScores)
                .sort(([, a], [, b]) => b - a)
                .map(([name, score], idx) => (
                  <div
                    key={name}
                    className="rounded-lg border bg-slate-50 dark:bg-slate-800/30 px-3 py-2"
                  >
                    <div className="flex items-baseline gap-1">
                      {idx === 0 && <span className="text-lg">🥇</span>}
                      {idx === 1 && <span className="text-lg">🥈</span>}
                      {idx === 2 && <span className="text-lg">🥉</span>}
                      <span className="text-xs font-medium text-slate-600 dark:text-slate-400 truncate">
                        {name}
                      </span>
                    </div>
                    <div className="mt-1 font-mono text-lg font-bold tabular-nums">
                      {score}
                    </div>
                  </div>
                ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
```

---

## 5. Integration with UserLibrary

**Linking Sessions to Game Stats**

```typescript
// After finalizing a session, create games_played entry
async function finalizeSessionAndUpdateStats(sessionId: string) {
  // 1. Finalize session (calculate final ranks)
  const finalizeResponse = await fetch(`/api/v1/game-sessions/${sessionId}/finalize`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ finalRanks })
  });

  const { sessionSummary } = await finalizeResponse.json();

  // 2. Create games_played entry in UserLibrary
  if (sessionSummary.gameId) {
    await fetch('/api/v1/library/games-played', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        gameId: sessionSummary.gameId,
        sessionId: sessionSummary.sessionId,
        playedAt: sessionSummary.sessionDate,
        playersCount: sessionSummary.participantCount,
        won: sessionSummary.winnerId === currentUserId
      })
    });
  }

  // 3. Redirect to session history
  router.push('/toolkit/history');
}
```

---

## 6. Real-Time Collaboration Hook

**Custom React Hook for SSE**

```typescript
// hooks/useSessionSync.ts
import { useEffect, useState } from 'react';
import { ScoreEntry } from '@/components/session';

export function useSessionSync(sessionId: string) {
  const [scores, setScores] = useState<ScoreEntry[]>([]);
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    const eventSource = new EventSource(`/api/v1/game-sessions/${sessionId}/stream`);

    eventSource.onopen = () => setIsConnected(true);
    eventSource.onerror = () => setIsConnected(false);

    eventSource.addEventListener('ScoreUpdatedEvent', (e) => {
      const event = JSON.parse(e.data);
      setScores((prev) => [...prev, event.scoreEntry]);
    });

    return () => {
      eventSource.close();
      setIsConnected(false);
    };
  }, [sessionId]);

  return { scores, isConnected };
}

// Usage in component
const { scores, isConnected } = useSessionSync(sessionId);
```

---

**End of Examples** 🎯

All examples use the session toolkit components with proper TypeScript types, real-time sync, and integration with MeepleAI's backend architecture.
