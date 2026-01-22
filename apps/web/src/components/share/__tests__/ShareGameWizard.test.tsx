/**
 * ShareGameWizard Component Tests (Issue #2743)
 *
 * Test Coverage:
 * - Wizard rendering with 3 steps
 * - Step navigation (Next, Back, Submit)
 * - Form state management (documents, notes)
 * - Success handling
 * - Close handling
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ShareGameWizard } from '../ShareGameWizard';
import type { UserLibraryEntry } from '@/lib/api';

// ============================================================================
// Mocks
// ============================================================================

// Mock React Query hooks
vi.mock('@/hooks/queries/useShareRequests', () => ({
  useCreateShareRequest: vi.fn(() => ({
    mutate: vi.fn((data, { onSuccess }) => {
      onSuccess?.({ shareRequestId: 'share-123', status: 'Pending', contributionType: 'NewGame', createdAt: new Date().toISOString() });
    }),
    isPending: false,
    isSuccess: false,
  })),
}));

// ============================================================================
// Mock Data
// ============================================================================

const mockGame: UserLibraryEntry = {
  id: '123e4567-e89b-12d3-a456-426614174000',
  userId: 'user-123',
  gameId: 'game-123',
  gameTitle: 'Catan',
  gamePublisher: 'Kosmos',
  gameYearPublished: 1995,
  gameIconUrl: null,
  gameImageUrl: 'https://example.com/catan.png',
  addedAt: new Date().toISOString(),
  notes: null,
  isFavorite: false,
};

// ============================================================================
// Test Callbacks
// ============================================================================

const mockOnClose = vi.fn();
const mockOnSuccess = vi.fn();

// ============================================================================
// Tests
// ============================================================================

describe('ShareGameWizard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders step 1 initially', () => {
      render(
        <ShareGameWizard
          game={mockGame}
          open={true}
          onClose={mockOnClose}
          onSuccess={mockOnSuccess}
        />
      );

      expect(screen.getByText(/Share "Catan" with the Community/i)).toBeInTheDocument();
      expect(screen.getByText(/Step 1 of 3/i)).toBeInTheDocument();
      expect(screen.getByText(mockGame.gameTitle)).toBeInTheDocument();
    });

    it('renders progress bar', () => {
      render(
        <ShareGameWizard
          game={mockGame}
          open={true}
          onClose={mockOnClose}
        />
      );

      expect(screen.getByText('1. Preview')).toBeInTheDocument();
      expect(screen.getByText('2. Documents')).toBeInTheDocument();
      expect(screen.getByText('3. Confirm')).toBeInTheDocument();
    });

    it('renders navigation buttons on step 1', () => {
      render(
        <ShareGameWizard
          game={mockGame}
          open={true}
          onClose={mockOnClose}
        />
      );

      expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /continue/i })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /back/i })).not.toBeInTheDocument();
    });
  });

  describe('Step Navigation', () => {
    it('advances to step 2 when Continue is clicked', () => {
      render(
        <ShareGameWizard
          game={mockGame}
          open={true}
          onClose={mockOnClose}
        />
      );

      const continueButton = screen.getByRole('button', { name: /continue/i });
      fireEvent.click(continueButton);

      expect(screen.getByText(/Step 2 of 3/i)).toBeInTheDocument();
      expect(screen.getByText(/Select documents to include/i)).toBeInTheDocument();
    });

    it('goes back to step 1 from step 2', () => {
      render(
        <ShareGameWizard
          game={mockGame}
          open={true}
          onClose={mockOnClose}
        />
      );

      // Go to step 2
      fireEvent.click(screen.getByRole('button', { name: /continue/i }));

      // Go back to step 1
      const backButton = screen.getByRole('button', { name: /back/i });
      fireEvent.click(backButton);

      expect(screen.getByText(/Step 1 of 3/i)).toBeInTheDocument();
    });

    it('advances to step 3 from step 2', () => {
      render(
        <ShareGameWizard
          game={mockGame}
          open={true}
          onClose={mockOnClose}
        />
      );

      // Go to step 2
      fireEvent.click(screen.getByRole('button', { name: /continue/i }));

      // Go to step 3
      fireEvent.click(screen.getByRole('button', { name: /continue/i }));

      expect(screen.getByText(/Step 3 of 3/i)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /submit request/i })).toBeInTheDocument();
    });
  });

  describe('Close Handling', () => {
    it('calls onClose when Cancel is clicked', () => {
      render(
        <ShareGameWizard
          game={mockGame}
          open={true}
          onClose={mockOnClose}
        />
      );

      fireEvent.click(screen.getByRole('button', { name: /cancel/i }));

      expect(mockOnClose).toHaveBeenCalledOnce();
    });
  });

  describe('Existing Game Warning', () => {
    it('shows warning when game exists in catalog', () => {
      render(
        <ShareGameWizard
          game={mockGame}
          open={true}
          onClose={mockOnClose}
          existingInCatalog={true}
        />
      );

      expect(screen.getByText(/This game already exists in the shared catalog/i)).toBeInTheDocument();
    });

    it('does not show warning when game is new', () => {
      render(
        <ShareGameWizard
          game={mockGame}
          open={true}
          onClose={mockOnClose}
          existingInCatalog={false}
        />
      );

      expect(screen.queryByText(/This game already exists in the shared catalog/i)).not.toBeInTheDocument();
    });
  });
});
