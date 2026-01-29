/**
 * EditorApprovalQueue Component Tests (Issue #2896)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { EditorApprovalQueue } from '../EditorApprovalQueue';
import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';
import { api } from '@/lib/api';

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
      approvePublication: vi.fn(),
      rejectPublication: vi.fn(),
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
  {
    id: 'game-3',
    bggId: 3,
    title: 'Game Three',
    yearPublished: 2024,
    description: 'Third test game',
    minPlayers: 3,
    maxPlayers: 6,
    playingTimeMinutes: 90,
    minAge: 12,
    complexityRating: 3.5,
    averageRating: 8.5,
    imageUrl: 'https://example.com/image3.jpg',
    thumbnailUrl: 'https://example.com/thumb3.jpg',
    status: 'PendingApproval',
    createdAt: '2026-01-26T10:00:00Z',
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

  it('should render all queue items', () => {
    render(
      <EditorApprovalQueue
        games={mockGames}
        onReview={mockOnReview}
        onApprove={mockOnApprove}
        onReject={mockOnReject}
      />
    );

    expect(screen.getAllByTestId(/editor-approval-queue-item/)).toHaveLength(3);
    expect(screen.getByText('Game One')).toBeInTheDocument();
    expect(screen.getByText('Game Two')).toBeInTheDocument();
    expect(screen.getByText('Game Three')).toBeInTheDocument();
  });

  it('should render checkboxes for each item', () => {
    render(
      <EditorApprovalQueue
        games={mockGames}
        onReview={mockOnReview}
        onApprove={mockOnApprove}
        onReject={mockOnReject}
      />
    );

    const checkboxes = screen.getAllByRole('checkbox');
    expect(checkboxes).toHaveLength(3);
  });

  it('should NOT show BulkActionBar when nothing selected', () => {
    render(
      <EditorApprovalQueue
        games={mockGames}
        onReview={mockOnReview}
        onApprove={mockOnApprove}
        onReject={mockOnReject}
      />
    );

    expect(screen.queryByTestId('editor-approval-queue-bulk-action-bar')).not.toBeInTheDocument();
  });

  describe('Selection Management', () => {
    it('should toggle individual item selection', async () => {
      const user = userEvent.setup();

      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[0]);

      // BulkActionBar should now appear
      await waitFor(() => {
        expect(screen.getByTestId('editor-approval-queue-bulk-action-bar')).toBeInTheDocument();
      });
    });

    it('should select multiple items', async () => {
      const user = userEvent.setup();

      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[0]);
      await user.click(checkboxes[1]);

      await waitFor(() => {
        const actionBar = screen.getByTestId('editor-approval-queue-bulk-action-bar');
        expect(actionBar).toBeInTheDocument();
        expect(actionBar).toHaveTextContent('2');
      });
    });

    it('should deselect item when clicked again', async () => {
      const user = userEvent.setup();

      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const checkboxes = screen.getAllByRole('checkbox');

      // Select then deselect
      await user.click(checkboxes[0]);
      await user.click(checkboxes[0]);

      // BulkActionBar should disappear
      expect(screen.queryByTestId('editor-approval-queue-bulk-action-bar')).not.toBeInTheDocument();
    });

    it('should clear all selections via BulkActionBar', async () => {
      const user = userEvent.setup();

      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      // Select 2 items
      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[0]);
      await user.click(checkboxes[1]);

      // Click clear button in action bar
      const clearButton = await screen.findByRole('button', { name: /clear/i });
      await user.click(clearButton);

      // Action bar should disappear
      await waitFor(() => {
        expect(screen.queryByTestId('editor-approval-queue-bulk-action-bar')).not.toBeInTheDocument();
      });
    });
  });

  describe('Bulk Approve', () => {
    it('should call API for all selected items', async () => {
      const user = userEvent.setup();
      const mockApprove = vi.mocked(api.sharedGames.approvePublication);
      mockApprove.mockResolvedValue(undefined);

      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
          onBulkComplete={mockOnBulkComplete}
        />
      );

      // Select 2 items
      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[0]);
      await user.click(checkboxes[1]);

      // Click Bulk Approve
      const approveButton = await screen.findByRole('button', { name: /Approva/i });
      await user.click(approveButton);

      await waitFor(() => {
        expect(mockApprove).toHaveBeenCalledTimes(2);
        expect(mockApprove).toHaveBeenCalledWith('game-1');
        expect(mockApprove).toHaveBeenCalledWith('game-2');
      });
    });

    it('should clear selection after successful bulk approve', async () => {
      const user = userEvent.setup();
      vi.mocked(api.sharedGames.approvePublication).mockResolvedValue(undefined);

      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
          onBulkComplete={mockOnBulkComplete}
        />
      );

      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[0]);

      const approveButton = await screen.findByRole('button', { name: /Approva/i });
      await user.click(approveButton);

      await waitFor(() => {
        expect(screen.queryByTestId('editor-approval-queue-bulk-action-bar')).not.toBeInTheDocument();
      });
    });

    it('should call onBulkComplete after approve', async () => {
      const user = userEvent.setup();
      vi.mocked(api.sharedGames.approvePublication).mockResolvedValue(undefined);

      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
          onBulkComplete={mockOnBulkComplete}
        />
      );

      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[0]);

      const approveButton = await screen.findByRole('button', { name: /Approva/i });
      await user.click(approveButton);

      await waitFor(() => {
        expect(mockOnBulkComplete).toHaveBeenCalledTimes(1);
      });
    });

    it('should handle partial failures in bulk approve', async () => {
      const user = userEvent.setup();
      const mockApprove = vi.mocked(api.sharedGames.approvePublication);

      // First succeeds, second fails
      mockApprove
        .mockResolvedValueOnce(undefined)
        .mockRejectedValueOnce(new Error('API error'));

      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[0]);
      await user.click(checkboxes[1]);

      const approveButton = await screen.findByRole('button', { name: /Approva/i });
      await user.click(approveButton);

      await waitFor(() => {
        expect(mockApprove).toHaveBeenCalledTimes(2);
        // Both success and error toasts should be shown (via sonner mock)
      });
    });
  });

  describe('Bulk Reject', () => {
    it('should open reject dialog when Bulk Reject clicked', async () => {
      const user = userEvent.setup();

      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      // Select an item
      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[0]);

      // Click Bulk Reject
      const rejectButton = await screen.findByRole('button', { name: /Rifiuta/i });
      await user.click(rejectButton);

      // Dialog should open
      await waitFor(() => {
        expect(screen.getByTestId('bulk-reject-dialog')).toBeInTheDocument();
      });
    });

    it('should call API with reason after dialog confirm', async () => {
      const user = userEvent.setup();
      const mockReject = vi.mocked(api.sharedGames.rejectPublication);
      mockReject.mockResolvedValue(undefined);

      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
          onBulkComplete={mockOnBulkComplete}
        />
      );

      // Select item
      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[0]);

      // Open reject dialog
      const rejectButton = await screen.findByRole('button', { name: /Rifiuta/i });
      await user.click(rejectButton);

      // Enter rejection reason
      const reasonInput = await screen.findByTestId('bulk-reject-reason');
      await user.type(reasonInput, 'Not suitable for catalog');

      // Confirm rejection
      const confirmButton = await screen.findByTestId('bulk-reject-confirm');
      await user.click(confirmButton);

      await waitFor(() => {
        expect(mockReject).toHaveBeenCalledWith('game-1', 'Not suitable for catalog');
      });
    });

    it('should clear selection after successful bulk reject', async () => {
      const user = userEvent.setup();
      vi.mocked(api.sharedGames.rejectPublication).mockResolvedValue(undefined);

      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[0]);

      const rejectButton = await screen.findByRole('button', { name: /Rifiuta/i });
      await user.click(rejectButton);

      const reasonInput = await screen.findByTestId('bulk-reject-reason');
      await user.type(reasonInput, 'Not suitable');

      const confirmButton = await screen.findByTestId('bulk-reject-confirm');
      await user.click(confirmButton);

      await waitFor(() => {
        expect(screen.queryByTestId('editor-approval-queue-bulk-action-bar')).not.toBeInTheDocument();
      });
    });
  });

  describe('Empty State', () => {
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
  });

  describe('Accessibility', () => {
    it('should have accessible checkbox labels', async () => {
      render(
        <EditorApprovalQueue
          games={mockGames}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const checkbox = screen.getAllByRole('checkbox')[0];
      expect(checkbox).toHaveAccessibleName(/Seleziona Game One/i);
    });
  });
});
