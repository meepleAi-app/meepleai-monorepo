'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

import { useRouter } from 'next/navigation';

import {
  NightSummaryView,
  type NightSummaryMVP,
  type NightSummaryNight,
  type NightSummaryPhoto,
} from '@/components/features/game-nights/summary';
import type { PerGameRecapGame } from '@/components/features/game-nights/summary';

// ─── Fixture data (TODO: replace with backend hook `useGameNightSummary(id)`)
// Continuità con mockup M mergiato nel PR #1250 — issue #487
// ─────────────────────────────────────────────────────────────────────────

const NIGHT: NightSummaryNight = {
  title: 'Sabato boardgame con i Padovani',
  dateLine: 'sabato 17 maggio 2026',
  location: 'Casa Marco · Padova',
  startedAt: '21:00',
  endedAt: '03:15',
  duration: '6h 15m',
  nightCode: '#GN-042',
};

const MVP: NightSummaryMVP = {
  id: 'p-davide',
  name: 'Davide',
  initials: 'DC',
  color: 200,
  achievements: '1 vittoria · 11 eventi diary · top scorer Brass',
};

const GAMES: ReadonlyArray<PerGameRecapGame> = [
  {
    id: 'gs-brass-1',
    sessionId: 's-brass-may17',
    title: 'Brass: Birmingham',
    emoji: '🏭',
    cover: ['hsl(220 35% 28%)', 'hsl(28 60% 38%)'],
    order: 1,
    duration: '2h 45m',
    eventsCount: 11,
    winner: { id: 'p-davide', name: 'Davide', initials: 'DC', color: 200, score: 178 },
    topScores: [
      { id: 'p-marco', name: 'Marco', initials: 'MR', color: 262, score: 142 },
      { id: 'p-giulia', name: 'Giulia', initials: 'GM', color: 10, score: 128 },
    ],
  },
  {
    id: 'gs-spirit-1',
    sessionId: 's-spirit-may17',
    title: 'Spirit Island',
    emoji: '🌋',
    cover: ['hsl(210 50% 30%)', 'hsl(150 50% 38%)'],
    order: 2,
    duration: '1h 50m',
    eventsCount: 9,
    coopMode: true,
    coopOutcome: 'team coop vinto · round 5/6 · adversary England 1',
  },
  {
    id: 'gs-wing-1',
    sessionId: 's-wing-may17',
    title: 'Wingspan',
    emoji: '🦜',
    cover: ['hsl(85 40% 45%)', 'hsl(35 60% 50%)'],
    order: 3,
    duration: '1h 25m',
    eventsCount: 8,
    winner: { id: 'p-giulia', name: 'Giulia', initials: 'GM', color: 10, score: 92 },
    topScores: [
      { id: 'p-sara', name: 'Sara', initials: 'ST', color: 320, score: 88 },
      { id: 'p-aaron', name: 'Aaron', initials: 'AK', color: 140, score: 81 },
    ],
  },
];

const PHOTOS: ReadonlyArray<NightSummaryPhoto> = [
  { id: 'ph-1', label: 'Setup Brass', gradient: ['hsl(220 35% 30%)', 'hsl(28 60% 40%)'] },
  { id: 'ph-2', label: 'Davide vince', gradient: ['hsl(350 70% 55%)', 'hsl(28 60% 50%)'] },
  { id: 'ph-3', label: 'Tabellone Spirit', gradient: ['hsl(210 50% 30%)', 'hsl(150 50% 40%)'] },
  { id: 'ph-4', label: 'Foto di gruppo', gradient: ['hsl(262 50% 50%)', 'hsl(320 60% 55%)'] },
  { id: 'ph-5', label: 'Tableau Wingspan', gradient: ['hsl(85 50% 50%)', 'hsl(35 60% 55%)'] },
  { id: 'ph-6', label: 'Brindisi finale', gradient: ['hsl(38 80% 55%)', 'hsl(10 70% 50%)'] },
];

// ─── View component ─────────────────────────────────────────────────────

export interface NightSummaryClientViewProps {
  readonly nightId: string;
}

export function NightSummaryClientView({ nightId: _nightId }: NightSummaryClientViewProps) {
  const router = useRouter();
  const [archived, setArchived] = useState(false);
  const [shareSuccess, setShareSuccess] = useState<{
    visible: boolean;
    subline?: string;
  }>({ visible: false });
  const toastTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Cleanup pending toast timer on unmount to avoid setState on unmounted component
  useEffect(() => {
    return () => {
      if (toastTimerRef.current) {
        clearTimeout(toastTimerRef.current);
      }
    };
  }, []);

  const handleShare = useCallback(() => {
    const url = `meepleai.app/s/${_nightId.slice(0, 12)}`;
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      void navigator.clipboard.writeText(`https://${url}`).catch(() => undefined);
    }
    setShareSuccess({
      visible: true,
      subline: `${url} · sparisce in 3s`,
    });
    // Replace any previous pending timer so rapid re-shares don't race
    if (toastTimerRef.current) {
      clearTimeout(toastTimerRef.current);
    }
    toastTimerRef.current = setTimeout(() => {
      setShareSuccess({ visible: false });
      toastTimerRef.current = null;
    }, 3000);
  }, [_nightId]);

  const handleArchive = useCallback(() => {
    setArchived(true);
  }, []);

  const handleUnarchive = useCallback(() => {
    setArchived(false);
  }, []);

  const handleGoToList = useCallback(() => {
    router.push('/game-nights');
  }, [router]);

  const handleJumpToSession = useCallback(
    (sessionId: string) => {
      router.push(`/sessions/${sessionId}`);
    },
    [router]
  );

  return (
    <NightSummaryView
      night={NIGHT}
      mvp={MVP}
      games={GAMES}
      eventsCount={28}
      photos={PHOTOS}
      archived={archived}
      shareSuccess={shareSuccess}
      onShare={handleShare}
      onArchive={handleArchive}
      onUnarchive={handleUnarchive}
      onGoToList={handleGoToList}
      onJumpToSession={handleJumpToSession}
    />
  );
}
