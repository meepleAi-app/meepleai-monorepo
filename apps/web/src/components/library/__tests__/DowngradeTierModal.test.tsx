/**
 * DowngradeTierModal Component Tests (Task 12)
 *
 * Test Coverage:
 * - Loading state display
 * - Game list rendering when data available
 * - Confirm button disabled with no games to remove
 * - Confirm button enabled when gamesToRemove is non-empty
 *
 * Target: ≥90% coverage
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import { DowngradeTierModal } from '../DowngradeTierModal';

// ============================================================================
// Mock Setup
// ============================================================================

vi.mock('@/hooks/queries/useLibraryDowngrade', () => ({
  useLibraryDowngradePreview: vi.fn(() => ({
    data: null,
    isLoading: true,
    error: null,
  })),
  useBulkRemoveFromLibrary: vi.fn(() => ({
    mutate: vi.fn(),
    isPending: false,
  })),
}));

// ============================================================================
// Helper
// ============================================================================

const defaultProps = {
  newQuota: 10,
  open: true,
  onOpenChange: vi.fn(),
  onComplete: vi.fn(),
};

// ============================================================================
// Tests
// ============================================================================

describe('DowngradeTierModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('mostra lo stato di caricamento iniziale', () => {
    render(<DowngradeTierModal {...defaultProps} />);
    expect(screen.getByText(/caricamento/i)).toBeInTheDocument();
  });

  it('mostra i giochi da rimuovere quando i dati sono disponibili', async () => {
    const { useLibraryDowngradePreview } = await import('@/hooks/queries/useLibraryDowngrade');
    (useLibraryDowngradePreview as ReturnType<typeof vi.fn>).mockReturnValue({
      data: {
        gamesToKeep: [],
        gamesToRemove: [
          {
            entryId: '11111111-1111-1111-1111-111111111111',
            gameId: 'g1111111-1111-1111-1111-111111111111',
            gameTitle: 'Catan',
            gameImageUrl: null,
            isFavorite: false,
            timesPlayed: 0,
            addedAt: '2024-01-01T00:00:00Z',
            lastPlayedAt: null,
          },
        ],
      },
      isLoading: false,
      error: null,
    });

    render(<DowngradeTierModal {...defaultProps} newQuota={5} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('pulsante conferma disabilitato quando non ci sono giochi selezionati da rimuovere', async () => {
    const { useLibraryDowngradePreview } = await import('@/hooks/queries/useLibraryDowngrade');
    (useLibraryDowngradePreview as ReturnType<typeof vi.fn>).mockReturnValue({
      data: { gamesToKeep: [], gamesToRemove: [] },
      isLoading: false,
      error: null,
    });

    render(<DowngradeTierModal {...defaultProps} newQuota={5} />);
    const button = screen.getByRole('button', { name: /rimuovi giochi selezionati/i });
    expect(button).toBeDisabled();
  });

  it('non renderizza nulla quando open è false', async () => {
    const { useLibraryDowngradePreview } = await import('@/hooks/queries/useLibraryDowngrade');
    (useLibraryDowngradePreview as ReturnType<typeof vi.fn>).mockReturnValue({
      data: null,
      isLoading: false,
      error: null,
    });

    const { container } = render(<DowngradeTierModal {...defaultProps} open={false} />);
    expect(container.querySelector('[role="dialog"]')).toBeNull();
  });
});
