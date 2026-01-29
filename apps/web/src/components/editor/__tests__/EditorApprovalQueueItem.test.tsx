/**
 * EditorApprovalQueueItem Component Tests (Issue #2895)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { EditorApprovalQueueItem } from '../EditorApprovalQueueItem';
import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';

// Mock Next.js Image component
vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: any) => <img src={src} alt={alt} {...props} />,
}));

const mockGame: SharedGame = {
  id: '123e4567-e89b-12d3-a456-426614174000',
  bggId: 12345,
  title: 'Test Game Title',
  yearPublished: 2024,
  description: 'A great game for testing',
  minPlayers: 2,
  maxPlayers: 4,
  playingTimeMinutes: 60,
  minAge: 10,
  complexityRating: 3.5,
  averageRating: 8.2,
  imageUrl: 'https://example.com/image.jpg',
  thumbnailUrl: 'https://example.com/thumbnail.jpg',
  status: 'PendingApproval',
  createdAt: '2026-01-29T10:00:00Z',
  modifiedAt: null,
};

describe('EditorApprovalQueueItem', () => {
  const mockOnReview = vi.fn();
  const mockOnApprove = vi.fn();
  const mockOnReject = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    // Mock current time to 2026-01-29 12:00:00 UTC
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-01-29T12:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should render game cover image', () => {
    render(
      <EditorApprovalQueueItem
        game={mockGame}
        onReview={mockOnReview}
        onApprove={mockOnApprove}
        onReject={mockOnReject}
      />
    );

    const cover = screen.getByTestId('editor-approval-queue-item-cover');
    expect(cover).toBeInTheDocument();
    expect(cover).toHaveAttribute('src', mockGame.thumbnailUrl);
    expect(cover).toHaveAttribute('alt', `${mockGame.title} cover`);
  });

  it('should render placeholder when no thumbnail URL', () => {
    const gameWithoutCover = { ...mockGame, thumbnailUrl: '' };

    render(
      <EditorApprovalQueueItem
        game={gameWithoutCover}
        onReview={mockOnReview}
        onApprove={mockOnApprove}
        onReject={mockOnReject}
      />
    );

    const placeholder = screen.getByTestId('editor-approval-queue-item-cover-placeholder');
    expect(placeholder).toBeInTheDocument();
    expect(placeholder).toHaveTextContent('No cover');
  });

  it('should render game title', () => {
    render(
      <EditorApprovalQueueItem
        game={mockGame}
        onReview={mockOnReview}
        onApprove={mockOnApprove}
        onReject={mockOnReject}
      />
    );

    const title = screen.getByTestId('editor-approval-queue-item-title');
    expect(title).toBeInTheDocument();
    expect(title).toHaveTextContent(mockGame.title);
  });

  it('should display submission time in Italian relative format', () => {
    render(
      <EditorApprovalQueueItem
        game={mockGame}
        onReview={mockOnReview}
        onApprove={mockOnApprove}
        onReject={mockOnReject}
      />
    );

    const submittedTime = screen.getByTestId('editor-approval-queue-item-submitted-time');
    expect(submittedTime).toBeInTheDocument();
    expect(submittedTime.textContent).toMatch(/Inviato/);
  });

  it('should display changes badge', () => {
    render(
      <EditorApprovalQueueItem
        game={mockGame}
        onReview={mockOnReview}
        onApprove={mockOnApprove}
        onReject={mockOnReject}
      />
    );

    const changesBadge = screen.getByTestId('editor-approval-queue-item-changes-badge');
    expect(changesBadge).toBeInTheDocument();
    expect(changesBadge).toHaveTextContent('Vedi modifiche');
  });

  describe('Priority Badges', () => {
    it('should NOT show priority badge for low priority (< 3 days)', () => {
      const recentGame = { ...mockGame, createdAt: '2026-01-28T12:00:00Z' }; // 1 day ago

      render(
        <EditorApprovalQueueItem
          game={recentGame}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const priorityBadge = screen.queryByTestId('editor-approval-queue-item-priority-badge');
      expect(priorityBadge).not.toBeInTheDocument();
    });

    it('should show yellow badge for medium priority (3-7 days)', () => {
      const mediumPriorityGame = { ...mockGame, createdAt: '2026-01-26T12:00:00Z' }; // 3 days ago

      render(
        <EditorApprovalQueueItem
          game={mediumPriorityGame}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const priorityBadge = screen.getByTestId('editor-approval-queue-item-priority-badge');
      expect(priorityBadge).toBeInTheDocument();
      expect(priorityBadge).toHaveTextContent('Media priorità');
    });

    it('should show red badge for high priority (> 7 days)', () => {
      const highPriorityGame = { ...mockGame, createdAt: '2026-01-21T12:00:00Z' }; // 8 days ago

      render(
        <EditorApprovalQueueItem
          game={highPriorityGame}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const priorityBadge = screen.getByTestId('editor-approval-queue-item-priority-badge');
      expect(priorityBadge).toBeInTheDocument();
      expect(priorityBadge).toHaveTextContent('Alta priorità');
    });
  });

  describe('Action Buttons', () => {
    it('should render Review button with correct text', () => {
      render(
        <EditorApprovalQueueItem
          game={mockGame}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const reviewButton = screen.getByTestId('editor-approval-queue-item-review-button');
      expect(reviewButton).toBeInTheDocument();
      expect(reviewButton).toHaveTextContent('Revisiona');
    });

    it('should render Approve button with correct text', () => {
      render(
        <EditorApprovalQueueItem
          game={mockGame}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const approveButton = screen.getByTestId('editor-approval-queue-item-approve-button');
      expect(approveButton).toBeInTheDocument();
      expect(approveButton).toHaveTextContent('Approva');
    });

    it('should render Reject button with correct text', () => {
      render(
        <EditorApprovalQueueItem
          game={mockGame}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const rejectButton = screen.getByTestId('editor-approval-queue-item-reject-button');
      expect(rejectButton).toBeInTheDocument();
      expect(rejectButton).toHaveTextContent('Rifiuta');
    });

    it('should call onReview when Review button is clicked', async () => {
      const user = userEvent.setup({ delay: null });

      render(
        <EditorApprovalQueueItem
          game={mockGame}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const reviewButton = screen.getByTestId('editor-approval-queue-item-review-button');
      await user.click(reviewButton);

      expect(mockOnReview).toHaveBeenCalledTimes(1);
      expect(mockOnReview).toHaveBeenCalledWith(mockGame.id);
    });

    it('should call onApprove when Approve button is clicked', async () => {
      const user = userEvent.setup({ delay: null });

      render(
        <EditorApprovalQueueItem
          game={mockGame}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const approveButton = screen.getByTestId('editor-approval-queue-item-approve-button');
      await user.click(approveButton);

      expect(mockOnApprove).toHaveBeenCalledTimes(1);
      expect(mockOnApprove).toHaveBeenCalledWith(mockGame.id);
    });

    it('should call onReject when Reject button is clicked', async () => {
      const user = userEvent.setup({ delay: null });

      render(
        <EditorApprovalQueueItem
          game={mockGame}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const rejectButton = screen.getByTestId('editor-approval-queue-item-reject-button');
      await user.click(rejectButton);

      expect(mockOnReject).toHaveBeenCalledTimes(1);
      expect(mockOnReject).toHaveBeenCalledWith(mockGame.id);
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA labels for cover image', () => {
      render(
        <EditorApprovalQueueItem
          game={mockGame}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const cover = screen.getByAltText(`${mockGame.title} cover`);
      expect(cover).toBeInTheDocument();
    });

    it('should be keyboard navigable - Review button', async () => {
      const user = userEvent.setup({ delay: null });

      render(
        <EditorApprovalQueueItem
          game={mockGame}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const reviewButton = screen.getByTestId('editor-approval-queue-item-review-button');
      reviewButton.focus();

      await user.keyboard('{Enter}');
      expect(mockOnReview).toHaveBeenCalledWith(mockGame.id);
    });

    it('should support custom data-testid', () => {
      render(
        <EditorApprovalQueueItem
          game={mockGame}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
          data-testid="custom-test-id"
        />
      );

      expect(screen.getByTestId('custom-test-id')).toBeInTheDocument();
      expect(screen.getByTestId('custom-test-id-title')).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle very old submissions (30+ days)', () => {
      const veryOldGame = { ...mockGame, createdAt: '2025-12-30T12:00:00Z' }; // 30 days ago

      render(
        <EditorApprovalQueueItem
          game={veryOldGame}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const priorityBadge = screen.getByTestId('editor-approval-queue-item-priority-badge');
      expect(priorityBadge).toHaveTextContent('Alta priorità');
    });

    it('should handle long game titles with line clamp', () => {
      const longTitleGame = {
        ...mockGame,
        title:
          'This is a very long game title that should be clamped to two lines to prevent layout issues in the approval queue',
      };

      render(
        <EditorApprovalQueueItem
          game={longTitleGame}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
        />
      );

      const title = screen.getByTestId('editor-approval-queue-item-title');
      expect(title).toHaveClass('line-clamp-2');
    });

    it('should apply custom className', () => {
      render(
        <EditorApprovalQueueItem
          game={mockGame}
          onReview={mockOnReview}
          onApprove={mockOnApprove}
          onReject={mockOnReject}
          className="custom-class"
        />
      );

      const container = screen.getByTestId('editor-approval-queue-item');
      expect(container).toHaveClass('custom-class');
    });
  });
});
