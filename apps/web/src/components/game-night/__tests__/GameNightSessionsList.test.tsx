/**
 * GameNightSessionsList — #4156 (D7): the sessions of a night are SessionTracking sessions, with no
 * LiveGameSession and so no page under `/sessions/{id}`. Each one links to the night page: the live
 * page while the night runs, the summary once it is completed.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { GameNightActiveSession } from '@/stores/game-night/types';

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

  // #4513: the canonical Badge variant carries `dark:bg-primary/90`. tailwind-merge drops
  // `bg-primary` for the status colour but keeps that class (another modifier), so with the dark
  // theme every status turned orange. Each status must bring its own dark background.
  it('each status badge keeps its own background with the dark theme', () => {
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

    for (const label of ['Completata', 'In corso', 'In attesa', 'Saltata', 'Sconosciuto']) {
      const badge = screen.getByText(label);
      expect(badge.className).not.toMatch(
        /(?:^|\s)dark:bg-(?:primary|secondary|destructive)\/90(?=\s|$)/
      );
      expect(badge.className).toMatch(/(?:^|\s)dark:bg-/);
    }
  });
});
