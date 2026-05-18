'use client';

import { useCallback, useState } from 'react';

import { useRouter } from 'next/navigation';

import {
  NightLiveHub,
  type NightLiveHubCurrentGame,
  type NightLiveHubNight,
  type NightLiveStatus,
} from '@/components/features/game-nights/live';
import type {
  DiaryEvent,
  DiaryGameRef,
  DiaryPlayerRef,
} from '@/components/features/game-nights/live';
import type { PlannedGame } from '@/components/features/game-nights/live';
import {
  GameTransitionDialog,
  type TransitionLastGame,
  type TransitionNextGame,
  type TransitionSubmodal,
} from '@/components/features/game-nights/transition';

// ─── Fixture data (TODO: replace with backend hook `useGameNightLive(id)`)
// Continuità con mockup K/L mergiati nel PR #1250 — issue #487
// ─────────────────────────────────────────────────────────────────────────

const NIGHT: NightLiveHubNight = {
  title: 'Sabato boardgame con i Padovani',
  shortTitle: 'Padovani · 17 mag',
  nightCode: '#GN-042',
};

const DIARY_PLAYERS: ReadonlyArray<DiaryPlayerRef> = [
  { id: 'p-marco', initials: 'MR', color: 262 },
  { id: 'p-giulia', initials: 'GM', color: 10 },
  { id: 'p-davide', initials: 'DC', color: 200 },
  { id: 'p-luca', initials: 'LB', color: 180 },
  { id: 'p-sara', initials: 'ST', color: 320 },
  { id: 'p-aaron', initials: 'AK', color: 140 },
];

const DIARY_GAMES: ReadonlyArray<DiaryGameRef> = [
  { id: 'gs-brass-1', title: 'Brass: Birmingham', emoji: '🏭' },
  { id: 'gs-spirit-1', title: 'Spirit Island', emoji: '🌋' },
];

const PLANNED_GAMES: ReadonlyArray<PlannedGame> = [
  {
    id: 'gs-brass-1',
    title: 'Brass: Birmingham',
    publisher: 'Roxley',
    emoji: '🏭',
    cover: ['hsl(220 35% 28%)', 'hsl(28 60% 38%)'],
    status: 'completed',
    order: 1,
    actual: '113m',
    estimated: '120m',
    score: '178–142',
    winner: { name: 'Davide', initials: 'DC', color: 200 },
  },
  {
    id: 'gs-spirit-1',
    title: 'Spirit Island',
    publisher: 'GMT · co-op',
    emoji: '🌋',
    cover: ['hsl(210 50% 30%)', 'hsl(150 50% 38%)'],
    status: 'inprogress',
    order: 2,
    actual: '35m elapsed',
    estimated: '90m',
    score: 'round 2 · in corso',
  },
  {
    id: 'gs-wing-1',
    title: 'Wingspan',
    publisher: 'Stonemaier',
    emoji: '🦜',
    cover: ['hsl(85 40% 45%)', 'hsl(35 60% 50%)'],
    status: 'upcoming',
    order: 3,
    estimated: '60m',
  },
];

const CURRENT_GAME: NightLiveHubCurrentGame = {
  id: 'gs-spirit-1',
  sessionId: 's-spirit-may17',
  title: 'Spirit Island',
  emoji: '🌋',
  cover: ['hsl(210 50% 30%)', 'hsl(150 50% 38%)'],
  score: 'round 2 · in corso',
};

const DIARY_EVENTS: ReadonlyArray<DiaryEvent> = [
  {
    id: 'd01',
    time: '21:02',
    gameId: 'gs-brass-1',
    kind: 'turn',
    icon: '🎯',
    actors: ['p-marco'],
    text: 'Marco apre il Canal Era — porto a Stoke-on-Trent',
  },
  {
    id: 'd08',
    time: '22:48',
    gameId: 'gs-brass-1',
    kind: 'score',
    icon: '📊',
    actors: ['p-marco'],
    text: 'Marco completa rete Birmingham (+12 link points)',
  },
  {
    id: 'd09',
    time: '22:55',
    gameId: 'gs-brass-1',
    kind: 'end',
    icon: '🏆',
    actors: ['p-davide'],
    text: '🏆 Davide vince Brass Birmingham 178–142',
  },
  {
    id: 'd10',
    time: '23:00',
    gameId: null,
    kind: 'system',
    icon: '↻',
    actors: [],
    text: 'Setup Spirit Island · 4 spiriti scelti random',
  },
  {
    id: 'd14',
    time: '23:23',
    gameId: 'gs-spirit-1',
    kind: 'score',
    icon: '📊',
    actors: ['p-davide'],
    text: 'Blight su Powerwala — Davide perde 1 presenza',
  },
];

// Transition data (last game = Brass completed, next game = Spirit Island)
const LAST_GAME: TransitionLastGame = {
  id: 'gs-brass-1',
  title: 'Brass: Birmingham',
  publisher: 'Roxley',
  emoji: '🏭',
  cover: ['hsl(220 35% 28%)', 'hsl(28 60% 38%)'],
  duration: '2h 45m',
  endedAt: '22:55',
  winner: { id: 'p-davide', name: 'Davide', initials: 'DC', color: 200, score: 178 },
  topThree: [
    {
      id: 'p-davide',
      name: 'Davide',
      initials: 'DC',
      color: 200,
      score: 178,
      delta: '+50 industria',
    },
    { id: 'p-marco', name: 'Marco', initials: 'MR', color: 262, score: 142, delta: '+12 network' },
    {
      id: 'p-giulia',
      name: 'Giulia',
      initials: 'GM',
      color: 10,
      score: 128,
      delta: '+8 carbone',
    },
  ],
};

const NEXT_GAME: TransitionNextGame = {
  id: 'gs-spirit-1',
  title: 'Spirit Island',
  publisher: 'GMT · co-op',
  emoji: '🌋',
  cover: ['hsl(210 50% 30%)', 'hsl(150 50% 38%)'],
  estimated: '90m',
  weight: 4.08,
  rules: [
    { icon: '🔁', text: 'Phase order · Growth → Fast → Slow', src: '§ 4.2' },
    { icon: '😱', text: 'Fear deck · 9 carte iniziali (level 1/2/3)', src: '§ 5.1' },
    { icon: '⚡', text: 'Power growth · 1 minor + 1 major per round', src: '§ 6.3' },
  ],
  setup: [
    { icon: '🗺️', text: 'Tabellone · 4 isole', done: true },
    { icon: '🧝', text: 'Spirit panels (4 spiriti scelti)', done: true },
    { icon: '🃏', text: 'Invader deck shuffled · stage I', done: false },
    { icon: '💀', text: 'Fear pool · 9 token', done: false },
  ],
};

// ─── View component ─────────────────────────────────────────────────────

export interface NightLiveClientViewProps {
  readonly nightId: string;
}

export function NightLiveClientView({ nightId: _nightId }: NightLiveClientViewProps) {
  const router = useRouter();
  const [status, setStatus] = useState<NightLiveStatus>('live');
  const [transitionOpen, setTransitionOpen] = useState(false);
  const [submodal, setSubmodal] = useState<TransitionSubmodal>(null);

  const handlePauseToggle = useCallback(() => {
    setStatus(current => (current === 'paused' ? 'live' : 'paused'));
  }, []);

  const handleTransitionOpen = useCallback(() => {
    setStatus('transition');
    setTransitionOpen(true);
  }, []);

  const handleTransitionClose = useCallback(() => {
    setTransitionOpen(false);
    setSubmodal(null);
    setStatus('live');
  }, []);

  const handlePlayNext = useCallback(() => {
    setTransitionOpen(false);
    setSubmodal(null);
    setStatus('live');
  }, []);

  const handleSkipConfirmed = useCallback(() => {
    setSubmodal(null);
    setTransitionOpen(false);
    setStatus('live');
  }, []);

  const handleEndNight = useCallback(() => {
    router.push(`/game-nights/${_nightId}/summary`);
  }, [router, _nightId]);

  const handleOpenSubmodal = useCallback((which: 'skip' | 'end') => {
    setSubmodal(which);
  }, []);

  const handleConfirmSubmodal = useCallback(() => {
    if (submodal === 'skip') {
      handleSkipConfirmed();
    } else if (submodal === 'end') {
      handleEndNight();
    }
  }, [submodal, handleSkipConfirmed, handleEndNight]);

  const handleCancelSubmodal = useCallback(() => {
    setSubmodal(null);
  }, []);

  const handleJumpToSession = useCallback(
    (sessionId: string) => {
      router.push(`/sessions/${sessionId}`);
    },
    [router]
  );

  const handleBack = useCallback(() => {
    router.push(`/game-nights/${_nightId}`);
  }, [router, _nightId]);

  return (
    <>
      <NightLiveHub
        night={NIGHT}
        status={status}
        current={2}
        total={3}
        elapsed="2h 35m"
        confirmedPlayers={6}
        totalPlayers={6}
        plannedGames={PLANNED_GAMES}
        currentGame={status === 'transition' ? null : CURRENT_GAME}
        diaryEvents={DIARY_EVENTS}
        diaryGames={DIARY_GAMES}
        diaryPlayers={DIARY_PLAYERS}
        onBack={handleBack}
        onPauseToggle={handlePauseToggle}
        onTransition={handleTransitionOpen}
        onEnd={handleEndNight}
        onJumpToSession={handleJumpToSession}
      />

      {transitionOpen ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4 backdrop-blur-sm"
          style={{ background: 'rgba(0,0,0,0.45)' }}
          onClick={handleTransitionClose}
          onKeyDown={e => {
            if (e.key === 'Escape') handleTransitionClose();
          }}
          role="presentation"
        >
          <div onClick={e => e.stopPropagation()} role="presentation">
            <GameTransitionDialog
              open
              lastGame={LAST_GAME}
              nextGame={NEXT_GAME}
              nightCode={NIGHT.nightCode}
              submodal={submodal}
              onClose={handleTransitionClose}
              onPlayNext={handlePlayNext}
              onOpenSubmodal={handleOpenSubmodal}
              onConfirmSubmodal={handleConfirmSubmodal}
              onCancelSubmodal={handleCancelSubmodal}
            />
          </div>
        </div>
      ) : null}
    </>
  );
}
