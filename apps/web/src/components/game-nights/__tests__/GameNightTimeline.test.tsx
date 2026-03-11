import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { TimelineSlot } from '@/store/game-night';

import { GameNightTimeline } from '../GameNightTimeline';

const slots: TimelineSlot[] = [
  { id: '1', type: 'game', gameId: 'g1', durationMinutes: 60 },
  { id: '2', type: 'break', durationMinutes: 15 },
  { id: '3', type: 'free', durationMinutes: 30 },
];

describe('GameNightTimeline', () => {
  it('renders all timeline slots', () => {
    render(<GameNightTimeline slots={slots} />);
    expect(screen.getByTestId('timeline')).toBeInTheDocument();
    expect(screen.getAllByTestId('timeline-slot')).toHaveLength(3);
  });

  it('shows game slot with game label', () => {
    render(<GameNightTimeline slots={slots} gameNames={{ g1: 'Catan' }} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('shows break label', () => {
    render(<GameNightTimeline slots={slots} />);
    expect(screen.getByText(/pausa/i)).toBeInTheDocument();
  });

  it('shows duration for each slot', () => {
    render(<GameNightTimeline slots={slots} />);
    expect(screen.getByText(/60min/i)).toBeInTheDocument();
    expect(screen.getByText(/15min/i)).toBeInTheDocument();
  });
});
