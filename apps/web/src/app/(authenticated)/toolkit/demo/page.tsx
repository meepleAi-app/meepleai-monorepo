'use client';

import React, { useState, useEffect } from 'react';

import {
  SessionHeader,
  ParticipantCard,
  ScoreInput,
  Scoreboard,
  Participant,
  ScoreEntry,
  Session,
  ScoreboardData,
  SyncStatus
} from '@/components/session';

// Mock Data for Demo
const mockSession: Session = {
  id: '1',
  sessionCode: 'ABC123',
  sessionType: 'GameSpecific',
  gameName: '7 Wonders',
  gameIcon: '🏛️',
  sessionDate: new Date(),
  status: 'Active',
  participantCount: 4
};

const mockParticipants: Participant[] = [
  {
    id: '1',
    displayName: 'Andrea (io)',
    isOwner: true,
    isCurrentUser: true,
    avatarColor: '#D97706',
    totalScore: 87,
    rank: 1
  },
  {
    id: '2',
    displayName: 'Giovanni',
    isOwner: false,
    isCurrentUser: false,
    avatarColor: '#2563EB',
    totalScore: 73,
    rank: 2,
    isTyping: false
  },
  {
    id: '3',
    displayName: 'Pouli',
    isOwner: false,
    isCurrentUser: false,
    avatarColor: '#DC2626',
    totalScore: 65,
    rank: 3
  },
  {
    id: '4',
    displayName: 'ElCiapo',
    isOwner: false,
    isCurrentUser: false,
    avatarColor: '#059669',
    totalScore: 52,
    rank: 4
  }
];

const mockInitialScores: ScoreEntry[] = [
  // Round 1
  { id: '1', participantId: '1', roundNumber: 1, category: 'Military', scoreValue: 15, timestamp: new Date(), createdBy: '1' },
  { id: '2', participantId: '2', roundNumber: 1, category: 'Military', scoreValue: 10, timestamp: new Date(), createdBy: '2' },
  { id: '3', participantId: '3', roundNumber: 1, category: 'Military', scoreValue: 12, timestamp: new Date(), createdBy: '3' },
  { id: '4', participantId: '4', roundNumber: 1, category: 'Military', scoreValue: 8, timestamp: new Date(), createdBy: '4' },
  // Round 2
  { id: '5', participantId: '1', roundNumber: 2, category: 'Science', scoreValue: 25, timestamp: new Date(), createdBy: '1' },
  { id: '6', participantId: '2', roundNumber: 2, category: 'Science', scoreValue: 20, timestamp: new Date(), createdBy: '2' },
  { id: '7', participantId: '3', roundNumber: 2, category: 'Science', scoreValue: 18, timestamp: new Date(), createdBy: '3' },
  { id: '8', participantId: '4', roundNumber: 2, category: 'Science', scoreValue: 15, timestamp: new Date(), createdBy: '4' },
  // Round 3
  { id: '9', participantId: '1', roundNumber: 3, category: 'Commerce', scoreValue: 30, timestamp: new Date(), createdBy: '1' },
  { id: '10', participantId: '2', roundNumber: 3, category: 'Commerce', scoreValue: 28, timestamp: new Date(), createdBy: '2' },
  { id: '11', participantId: '3', roundNumber: 3, category: 'Commerce', scoreValue: 22, timestamp: new Date(), createdBy: '3' },
  { id: '12', participantId: '4', roundNumber: 3, category: 'Commerce', scoreValue: 20, timestamp: new Date(), createdBy: '4' },
  // Wonders
  { id: '13', participantId: '1', roundNumber: null, category: 'Wonders', scoreValue: 17, timestamp: new Date(), createdBy: '1' },
  { id: '14', participantId: '2', roundNumber: null, category: 'Wonders', scoreValue: 15, timestamp: new Date(), createdBy: '2' },
  { id: '15', participantId: '3', roundNumber: null, category: 'Wonders', scoreValue: 13, timestamp: new Date(), createdBy: '3' },
  { id: '16', participantId: '4', roundNumber: null, category: 'Wonders', scoreValue: 9, timestamp: new Date(), createdBy: '4' }
];

export default function ToolkitDemoPage() {
  const [participants, setParticipants] = useState<Participant[]>(mockParticipants);
  const [scores, setScores] = useState<ScoreEntry[]>(mockInitialScores);
  const [syncStatus, setSyncStatus] = useState<SyncStatus>('idle');
  const [currentRound] = useState(3);

  // Recalculate totals when scores change
  useEffect(() => {
    setParticipants((prevParticipants) => {
      const updatedParticipants = prevParticipants.map((p) => {
        const totalScore = scores
          .filter((s) => s.participantId === p.id)
          .reduce((sum, s) => sum + s.scoreValue, 0);
        return { ...p, totalScore };
      });

      // Update ranks
      const sorted = [...updatedParticipants].sort((a, b) => b.totalScore - a.totalScore);
      return updatedParticipants.map((p) => ({
        ...p,
        rank: sorted.findIndex((s) => s.id === p.id) + 1
      }));
    });
  }, [scores]);

  const handleScoreSubmit = async (data: {
    participantId: string;
    roundNumber: number | null;
    category: string | null;
    scoreValue: number;
  }) => {
    setSyncStatus('saving');

    // Simulate network delay
    await new Promise((resolve) => setTimeout(resolve, 800));

    const newScore: ScoreEntry = {
      id: `${scores.length + 1}`,
      participantId: data.participantId,
      roundNumber: data.roundNumber,
      category: data.category,
      scoreValue: data.scoreValue,
      timestamp: new Date(),
      createdBy: '1'
    };

    setScores([...scores, newScore]);
    setSyncStatus('synced');

    // Reset to idle after 2s
    setTimeout(() => setSyncStatus('idle'), 2000);
  };

  const handleUndo = () => {
    if (scores.length > 0) {
      setScores(scores.slice(0, -1));
    }
  };

  const scoreboardData: ScoreboardData = {
    participants,
    scores,
    rounds: [1, 2, 3],
    categories: ['Military', 'Science', 'Commerce', 'Wonders']
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 via-amber-50/30 to-orange-50/20 dark:from-slate-950 dark:via-slate-900 dark:to-slate-950">
      {/* Session Header */}
      <SessionHeader
        session={mockSession}
        onPause={() => alert('Pause session')}
        onFinalize={() => alert('Finalize session')}
        onShare={() => alert('Share results')}
      />

      {/* Main Content */}
      <div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
        {/* Page Title */}
        <div className="mb-6">
          <h1 className="text-3xl font-black text-slate-900 dark:text-amber-50 tracking-tight">
            🎲 Game Session Toolkit Demo
          </h1>
          <p className="mt-2 text-sm text-slate-600 dark:text-slate-400">
            Production-ready components for MeepleAI collaborative board game sessions
          </p>
        </div>

        {/* Demo Grid */}
        <div className="grid gap-6 lg:grid-cols-12">
          {/* Left Column - Participants (Desktop 3-col layout) */}
          <div className="lg:col-span-3 space-y-4">
            <h2 className="font-bold text-sm uppercase tracking-wider text-slate-600 dark:text-slate-400 px-1">
              Players
            </h2>
            <div className="space-y-3">
              {participants.map((p) => (
                <ParticipantCard key={p.id} participant={p} variant="full" />
              ))}
            </div>
          </div>

          {/* Middle Column - Scoreboard */}
          <div className="lg:col-span-6">
            <Scoreboard data={scoreboardData} isRealTime={true} variant="full" />
          </div>

          {/* Right Column - Individual Participant Cards (Compact) */}
          <div className="lg:col-span-3 space-y-4">
            <h2 className="font-bold text-sm uppercase tracking-wider text-slate-600 dark:text-slate-400 px-1">
              Quick View
            </h2>
            <div className="space-y-2">
              {participants.map((p) => (
                <ParticipantCard key={p.id} participant={p} variant="compact" />
              ))}
            </div>

            {/* Design System Info */}
            <div className="mt-6 rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800/50 p-4">
              <h3 className="font-bold text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400 mb-3">
                Design System
              </h3>
              <dl className="space-y-2 text-xs">
                <div>
                  <dt className="font-semibold text-slate-700 dark:text-slate-300">Theme</dt>
                  <dd className="text-slate-600 dark:text-slate-400">Analog Gaming Digitized</dd>
                </div>
                <div>
                  <dt className="font-semibold text-slate-700 dark:text-slate-300">Colors</dt>
                  <dd className="flex gap-1 mt-1">
                    <div className="h-4 w-4 rounded bg-amber-600" title="Primary Amber" />
                    <div className="h-4 w-4 rounded bg-orange-600" title="Accent Orange" />
                    <div className="h-4 w-4 rounded bg-emerald-600" title="Success" />
                    <div className="h-4 w-4 rounded bg-red-600" title="Danger" />
                    <div className="h-4 w-4 rounded bg-slate-700" title="Neutral" />
                  </dd>
                </div>
                <div>
                  <dt className="font-semibold text-slate-700 dark:text-slate-300">Features</dt>
                  <dd className="text-slate-600 dark:text-slate-400">
                    • Wood grain textures<br />
                    • Embossed numbers<br />
                    • Physics-based interactions<br />
                    • Real-time SSE sync<br />
                    • Mobile-first responsive
                  </dd>
                </div>
              </dl>
            </div>
          </div>
        </div>
      </div>

      {/* Score Input - Sticky Bottom */}
      <ScoreInput
        participants={participants}
        rounds={[1, 2, 3]}
        categories={['Military', 'Science', 'Commerce', 'Wonders']}
        currentRound={currentRound}
        onSubmit={handleScoreSubmit}
        onUndo={handleUndo}
        syncStatus={syncStatus}
      />
    </div>
  );
}
