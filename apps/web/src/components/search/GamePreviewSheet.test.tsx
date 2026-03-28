import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryQuota: vi.fn(() => ({
    data: {
      currentCount: 5,
      maxAllowed: 50,
      userTier: 'free',
      remainingSlots: 45,
      percentageUsed: 10,
    },
  })),
}));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: vi.fn(() => ({ data: { id: 'user-1' } })),
}));

vi.mock('@/lib/api', () => ({
  api: {
    gameNightBgg: {
      importGame: vi.fn(),
    },
  },
}));

import { GamePreviewSheet } from './GamePreviewSheet';

const mockGame = {
  bggId: 123,
  title: 'Catan',
  yearPublished: 1995,
  thumbnailUrl: 'https://example.com/catan.jpg',
};

describe('GamePreviewSheet', () => {
  it('renders game title when open', () => {
    render(<GamePreviewSheet open game={mockGame} onOpenChange={() => {}} onAdded={() => {}} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('renders add to library button', () => {
    render(<GamePreviewSheet open game={mockGame} onOpenChange={() => {}} onAdded={() => {}} />);
    expect(screen.getByRole('button', { name: /aggiungi alla libreria/i })).toBeInTheDocument();
  });

  it('does not render when no game', () => {
    render(<GamePreviewSheet open game={null} onOpenChange={() => {}} onAdded={() => {}} />);
    expect(screen.queryByText('Catan')).not.toBeInTheDocument();
  });
});
