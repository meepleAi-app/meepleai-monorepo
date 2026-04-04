/**
 * @vitest-environment jsdom
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { MeeplePlaylistCard } from '../MeeplePlaylistCard';

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

describe('MeeplePlaylistCard', () => {
  const mockPlaylist = {
    id: '00000000-0000-0000-0000-000000000001',
    name: 'Friday Night Games',
    scheduledDate: null,
    creatorUserId: '00000000-0000-0000-0000-000000000002',
    isShared: false,
    shareToken: null,
    games: [
      { sharedGameId: '00000000-0000-0000-0000-000000000003', position: 0, addedAt: '2026-01-01' },
    ],
    createdAt: '2026-01-01',
    updatedAt: '2026-01-01',
  };

  it('renders with correct entity type', () => {
    render(<MeeplePlaylistCard playlist={mockPlaylist} />);
    const card = screen.getByTestId('playlist-card');
    expect(card).toHaveAttribute('data-entity', 'collection');
  });

  it('displays playlist name as title', () => {
    render(<MeeplePlaylistCard playlist={mockPlaylist} />);
    expect(screen.getByText('Friday Night Games')).toBeInTheDocument();
  });
});
