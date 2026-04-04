/**
 * @vitest-environment jsdom
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { MeepleResumeSessionCard } from '../MeepleResumeSessionCard';

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

vi.mock('date-fns', () => ({
  formatDistanceToNow: () => '2 giorni fa',
}));

describe('MeepleResumeSessionCard', () => {
  const mockProps = {
    sessionId: 'session-1',
    gameName: 'Catan',
    lastActivityAt: '2026-01-01T10:00:00Z',
    playerCount: 4,
    sessionCode: 'ABC123',
  };

  it('renders with correct entity type', () => {
    render(<MeepleResumeSessionCard {...mockProps} />);
    const card = screen.getByTestId('meeple-card');
    expect(card).toHaveAttribute('data-entity', 'session');
  });

  it('displays game name as title', () => {
    render(<MeepleResumeSessionCard {...mockProps} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });
});
