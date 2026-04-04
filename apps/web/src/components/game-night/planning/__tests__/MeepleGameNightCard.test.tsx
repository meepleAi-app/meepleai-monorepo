/**
 * @vitest-environment jsdom
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { MeepleGameNightCard } from '../MeepleGameNightCard';

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

describe('MeepleGameNightCard', () => {
  const mockNight = {
    id: 'gn-1',
    title: 'Friday Fun',
    status: 'upcoming' as const,
    date: '2026-03-20T20:00:00Z',
    playerCount: 4,
    gameCount: 3,
    playerAvatars: [],
    gameThumbnails: [],
  };

  it('renders with correct entity type', () => {
    render(<MeepleGameNightCard night={mockNight} />);
    const card = screen.getByTestId('game-night-card');
    expect(card).toHaveAttribute('data-entity', 'event');
  });

  it('displays night title', () => {
    render(<MeepleGameNightCard night={mockNight} />);
    expect(screen.getByText('Friday Fun')).toBeInTheDocument();
  });
});
