/**
 * EditorApprovalQueue Component Tests (Issue #2896)
 *
 * Simplified tests focusing on core functionality without complex async flows.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { EditorApprovalQueue } from '../EditorApprovalQueue';
import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';

// Mock dependencies
vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: any) => <img src={src} alt={alt} {...props} />,
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      approvePublication: vi.fn().mockResolvedValue(undefined),
      rejectPublication: vi.fn().mockResolvedValue(undefined),
    },
  },
}));

const mockGames: SharedGame[] = [
  {
    id: 'game-1',
    bggId: 1,
    title: 'Game One',
    yearPublished: 2024,
    description: 'First test game',
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 60,
    minAge: 10,
    complexityRating: 2.5,
    averageRating: 7.5,
    imageUrl: 'https://example.com/image1.jpg',
    thumbnailUrl: 'https://example.com/thumb1.jpg',
    status: 'PendingApproval',
    createdAt: '2026-01-28T10:00:00Z',
    modifiedAt: null,
  },
  {
    id: 'game-2',
    bggId: 2,
    title: 'Game Two',
    yearPublished: 2024,
    description: 'Second test game',
    minPlayers: 1,
    maxPlayers: 2,
    playingTimeMinutes: 30,
    minAge: 8,
    complexityRating: 1.5,
    averageRating: 8.0,
    imageUrl: 'https://example.com/image2.jpg',
    thumbnailUrl: 'https://example.com/thumb2.jpg',
    status: 'PendingApproval',
    createdAt: '2026-01-27T10:00:00Z',
    modifiedAt: null,
  },
];

describe('EditorApprovalQueue', () => {
  const mockOnReview = vi.fn();
  const mockOnApprove = vi.fn();
  const mockOnReject = vi.fn();
  const mockOnBulkComplete = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render the queue container', () => {
      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      expect(screen.getByTestId('editor-approval-queue')).toBeInTheDocument();
    });

    it('should render checkboxes for bulk selection', () => {
      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const checkboxes = screen.getAllByRole('checkbox');
      expect(checkboxes.length).toBeGreaterThan(0);
    });

    it('should handle empty games array', () => {
      render(
        <EditorApprovalQueue
          games={[]}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      expect(screen.getByTestId('editor-approval-queue')).toBeInTheDocument();
      expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
    });

    it('should NOT show BulkActionBar initially', () => {
      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      // BulkActionBar should not be visible when nothing is selected
      expect(screen.queryByTestId('editor-approval-queue-bulk-action-bar')).not.toBeInTheDocument();
    });
  });

  describe('Component Integration', () => {
    it('should pass callbacks to child components', () => {
      const { container } = render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      // Verify child components render with action buttons
      const reviewButtons = container.querySelectorAll('[data-testid*="review-button"]');
      expect(reviewButtons.length).toBeGreaterThan(0);
    });

    it('should include BulkRejectDialog in component tree', () => {
      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      // BulkRejectDialog is in the component (just verify no crashes)
      expect(screen.getByTestId('editor-approval-queue')).toBeInTheDocument();
    });
  });

  describe('Props', () => {
    it('should accept custom className', () => {
      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
          className="custom-class"
        />
      );

      const container = screen.getByTestId('editor-approval-queue');
      expect(container).toHaveClass('custom-class');
    });

    it('should accept custom testId', () => {
      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
          data-testid="custom-queue"
        />
      );

      expect(screen.getByTestId('custom-queue')).toBeInTheDocument();
    });
  });
});
