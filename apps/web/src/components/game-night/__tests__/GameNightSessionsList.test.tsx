/**
 * GameNightSessionsList — #4156 (D7): the sessions of a night are SessionTracking sessions, with no
 * LiveGameSession and so no page under `/sessions/{id}`. Each one links to the night page: the live
 * page while the night runs, the summary once it is completed.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { GameNightActiveSession } from '@/stores/game-night/types';
import { backgroundClasses } from '@/test-utils/background-classes';

import { GameNightSessionsList } from '../GameNightSessionsList';

const SESSIONS: GameNightActiveSession[] = [
  {
    id: 'tracking-1',
    gameNightSessionId: 'gns-1',
    gameId: 'game-1',
    gameTitle: 'Brass',
    playOrder: 1,
    status: 'completed',
  },
  {
    id: 'tracking-2',
    gameNightSessionId: 'gns-2',
    gameId: 'game-2',
    gameTitle: 'Azul',
    playOrder: 2,
    status: 'in_progress',
  },
];

function hrefs(): (string | null)[] {
  return screen.getAllByRole('link').map(link => link.getAttribute('href'));
}

describe('GameNightSessionsList — #4156 (D7)', () => {
  it('links every session to the night live page while the night runs', () => {
    render(<GameNightSessionsList sessions={SESSIONS} gameNightId="night-1" />);
    expect(hrefs()).toEqual(['/game-nights/night-1/live', '/game-nights/night-1/live']);
  });

  it('links every session to the night summary once the night is completed', () => {
    render(<GameNightSessionsList sessions={SESSIONS} gameNightId="night-1" isCompleted />);
    expect(hrefs()).toEqual(['/game-nights/night-1/summary', '/game-nights/night-1/summary']);
  });

  it('never links to /sessions/{id}', () => {
    render(<GameNightSessionsList sessions={SESSIONS} gameNightId="night-1" />);
    expect(hrefs().some(href => href?.startsWith('/sessions/'))).toBe(false);
  });

  // #4752: the night page was reachable only once a session existed, and a night with none yet is
  // exactly where the organizer starts the first game (the "▶ Avvia" action lives on that page).
  it('links a night with no games yet to its live page while it runs', () => {
    render(<GameNightSessionsList sessions={[]} gameNightId="night-1" />);
    expect(screen.getByRole('link', { name: /apri la serata live/i })).toHaveAttribute(
      'href',
      '/game-nights/night-1/live'
    );
  });

  it('offers no live page for a completed night with no games', () => {
    render(<GameNightSessionsList sessions={[]} gameNightId="night-1" isCompleted />);
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  // #4513, #4539: until #4539 the canonical Badge variant carried `dark:bg-primary/90` (and hover
  // equivalents). tailwind-merge dropped `bg-primary` for the status colour but kept the classes
  // with another modifier, so with the dark theme every status turned orange. Each badge must carry
  // exactly its status's background classes, none left over from the variant.
  it('each status badge carries only its own background', () => {
    const sessions: GameNightActiveSession[] = [
      ...SESSIONS,
      { ...SESSIONS[0], id: 'tracking-3', status: 'pending' },
      { ...SESSIONS[0], id: 'tracking-4', status: 'skipped' },
      // A status the client does not know yet (newer API) falls back to «Sconosciuto».
      {
        ...SESSIONS[0],
        id: 'tracking-5',
        status: 'abandoned' as unknown as GameNightActiveSession['status'],
      },
    ];
    render(<GameNightSessionsList sessions={sessions} gameNightId="night-1" />);

    const expected: Record<string, string[]> = {
      Completata: ['bg-blue-100', 'dark:bg-blue-900/30'],
      'In corso': ['bg-emerald-100', 'dark:bg-emerald-900/30'],
      'In attesa': ['bg-muted'],
      Saltata: ['bg-amber-100', 'dark:bg-amber-900/30'],
      Sconosciuto: ['bg-muted'],
    };
    for (const [label, background] of Object.entries(expected)) {
      expect(backgroundClasses(screen.getByText(label))).toEqual(background);
    }
  });
});
