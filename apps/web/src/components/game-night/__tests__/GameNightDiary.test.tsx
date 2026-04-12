import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { GameNightDiary } from '../GameNightDiary';
import type { DiaryEntry } from '@/stores/game-night/types';

const mockEntries: DiaryEntry[] = [
  {
    id: '1',
    sessionId: 's1',
    eventType: 'night_started',
    description: '🎮 Game Night iniziata (3 giocatori)',
    timestamp: '2026-04-07T19:30:00Z',
  },
  {
    id: '2',
    sessionId: 's1',
    eventType: 'game_started',
    description: '🎲 Catan: partita iniziata',
    timestamp: '2026-04-07T19:35:00Z',
  },
  {
    id: '3',
    sessionId: 's1',
    eventType: 'dice_roll',
    description: '🎲 Marco ha lanciato 2d6: 8',
    timestamp: '2026-04-07T19:40:00Z',
    actorId: 'marco-id',
  },
];

describe('GameNightDiary', () => {
  it('renders all diary entries', () => {
    render(<GameNightDiary entries={mockEntries} />);
    expect(screen.getByText(/Game Night iniziata/)).toBeInTheDocument();
    expect(screen.getByText(/Catan: partita iniziata/)).toBeInTheDocument();
    expect(screen.getByText(/Marco ha lanciato/)).toBeInTheDocument();
  });

  it('shows time for each entry (HH:MM format)', () => {
    render(<GameNightDiary entries={mockEntries} />);
    // Use regex to avoid locale-dependent exact match
    const timeElements = screen.getAllByText(/\d{2}:\d{2}/);
    expect(timeElements.length).toBeGreaterThanOrEqual(3);
  });

  it('renders empty state when no entries', () => {
    render(<GameNightDiary entries={[]} />);
    expect(screen.getByText(/nessun evento/i)).toBeInTheDocument();
  });
});
