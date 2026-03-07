/**
 * Unit tests for ResumePhotoReview component (Issue #5370)
 * Epic #5358 - Session Photo Attachments (Phase 0)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, cleanup, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ResumePhotoReview, type ResumePhotoReviewProps } from '../ResumePhotoReview';
import type { SessionAttachmentDto } from '../PhotoUploadModal';

function makeAttachment(overrides: Partial<SessionAttachmentDto> = {}): SessionAttachmentDto {
  return {
    id: `att-${Math.random().toString(36).slice(2, 8)}`,
    sessionId: 'session-123',
    playerId: 'player-A',
    attachmentType: 'BoardState',
    blobUrl: 'https://storage.example.com/photo.jpg',
    thumbnailUrl: 'https://storage.example.com/thumb.jpg',
    caption: null,
    contentType: 'image/jpeg',
    fileSizeBytes: 500000,
    snapshotIndex: null,
    createdAt: '2026-03-07T10:00:00Z',
    ...overrides,
  };
}

const sampleAttachments: SessionAttachmentDto[] = [
  makeAttachment({ id: 'att-1', playerId: 'player-A', caption: 'Board overview' }),
  makeAttachment({ id: 'att-2', playerId: 'player-A', attachmentType: 'PlayerArea' }),
  makeAttachment({ id: 'att-3', playerId: 'player-B', caption: 'My character' }),
];

const playerNames: Record<string, string> = {
  'player-A': 'Alice',
  'player-B': 'Bob',
};

describe('ResumePhotoReview', () => {
  const defaultProps: ResumePhotoReviewProps = {
    sessionId: 'session-123',
    attachments: sampleAttachments,
    isHost: true,
    onAllReady: vi.fn(),
    onCancel: vi.fn(),
    playerNames,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  // ================================================================
  // Rendering
  // ================================================================

  it('renders the review overlay', () => {
    render(<ResumePhotoReview {...defaultProps} />);
    expect(screen.getByTestId('resume-photo-review')).toBeInTheDocument();
    expect(screen.getByTestId('review-title')).toHaveTextContent('Review Board State');
  });

  it('groups photos by player', () => {
    render(<ResumePhotoReview {...defaultProps} />);
    expect(screen.getByTestId('player-group-player-A')).toBeInTheDocument();
    expect(screen.getByTestId('player-group-player-B')).toBeInTheDocument();
  });

  it('shows player names', () => {
    render(<ResumePhotoReview {...defaultProps} />);
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('shows photo count per player', () => {
    render(<ResumePhotoReview {...defaultProps} />);
    expect(screen.getByText('2 photos')).toBeInTheDocument();
    expect(screen.getByText('1 photo')).toBeInTheDocument();
  });

  // ================================================================
  // Empty state
  // ================================================================

  it('shows empty state when no attachments', () => {
    render(<ResumePhotoReview {...defaultProps} attachments={[]} />);
    expect(screen.getByTestId('no-photos')).toBeInTheDocument();
    expect(screen.getByText('No photos to review')).toBeInTheDocument();
  });

  // ================================================================
  // Pause timestamp
  // ================================================================

  it('shows relative time when pausedAt provided', () => {
    const oneHourAgo = new Date(Date.now() - 3600000).toISOString();
    render(<ResumePhotoReview {...defaultProps} pausedAt={oneHourAgo} />);
    expect(screen.getByText(/Paused 1 hour ago/)).toBeInTheDocument();
  });

  // ================================================================
  // Expand / Collapse
  // ================================================================

  it('sections are collapsed by default', () => {
    render(<ResumePhotoReview {...defaultProps} />);
    expect(screen.queryByTestId('photo-grid-player-A')).not.toBeInTheDocument();
    expect(screen.queryByTestId('photo-grid-player-B')).not.toBeInTheDocument();
  });

  it('expands a player section on click', async () => {
    const user = userEvent.setup();
    render(<ResumePhotoReview {...defaultProps} />);

    await user.click(screen.getByTestId('player-toggle-player-A'));

    expect(screen.getByTestId('photo-grid-player-A')).toBeInTheDocument();
    expect(screen.getByTestId('review-photo-att-1')).toBeInTheDocument();
    expect(screen.getByTestId('review-photo-att-2')).toBeInTheDocument();
  });

  it('collapses a section on second click', async () => {
    const user = userEvent.setup();
    render(<ResumePhotoReview {...defaultProps} />);

    await user.click(screen.getByTestId('player-toggle-player-A'));
    expect(screen.getByTestId('photo-grid-player-A')).toBeInTheDocument();

    await user.click(screen.getByTestId('player-toggle-player-A'));
    expect(screen.queryByTestId('photo-grid-player-A')).not.toBeInTheDocument();
  });

  it('expand all button expands all sections', async () => {
    const user = userEvent.setup();
    render(<ResumePhotoReview {...defaultProps} />);

    await user.click(screen.getByTestId('expand-all'));

    expect(screen.getByTestId('photo-grid-player-A')).toBeInTheDocument();
    expect(screen.getByTestId('photo-grid-player-B')).toBeInTheDocument();
  });

  // ================================================================
  // Lightbox
  // ================================================================

  it('opens lightbox on photo click', async () => {
    const user = userEvent.setup();
    render(<ResumePhotoReview {...defaultProps} />);

    // Expand first
    await user.click(screen.getByTestId('player-toggle-player-A'));

    // Click photo
    await user.click(screen.getByTestId('review-photo-att-1'));

    expect(screen.getByTestId('review-lightbox')).toBeInTheDocument();
    expect(screen.getByTestId('review-lightbox-image')).toBeInTheDocument();
  });

  it('closes lightbox on close button', async () => {
    const user = userEvent.setup();
    render(<ResumePhotoReview {...defaultProps} />);

    await user.click(screen.getByTestId('player-toggle-player-A'));
    await user.click(screen.getByTestId('review-photo-att-1'));
    expect(screen.getByTestId('review-lightbox')).toBeInTheDocument();

    await user.click(screen.getByTestId('review-lightbox-close'));

    await waitFor(() => {
      expect(screen.queryByTestId('review-lightbox')).not.toBeInTheDocument();
    });
  });

  // ================================================================
  // Host actions
  // ================================================================

  it('shows Resume Game button for host', () => {
    render(<ResumePhotoReview {...defaultProps} isHost={true} />);
    expect(screen.getByTestId('resume-button')).toBeInTheDocument();
    expect(screen.getByText('Resume Game')).toBeInTheDocument();
  });

  it('hides Resume Game button for non-host', () => {
    render(<ResumePhotoReview {...defaultProps} isHost={false} />);
    expect(screen.queryByTestId('resume-button')).not.toBeInTheDocument();
  });

  it('calls onAllReady when resume is clicked', async () => {
    const user = userEvent.setup();
    const onAllReady = vi.fn();
    render(<ResumePhotoReview {...defaultProps} onAllReady={onAllReady} />);

    await user.click(screen.getByTestId('resume-button'));

    expect(onAllReady).toHaveBeenCalledOnce();
  });

  it('disables resume button when no photos', () => {
    render(<ResumePhotoReview {...defaultProps} attachments={[]} />);
    // resume-button won't be visible because empty state is shown, but if forced...
    // actually the button is still rendered in footer
    expect(screen.getByTestId('resume-button')).toBeDisabled();
  });

  // ================================================================
  // Cancel
  // ================================================================

  it('calls onCancel when cancel/back buttons clicked', async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn();
    render(<ResumePhotoReview {...defaultProps} onCancel={onCancel} />);

    await user.click(screen.getByTestId('cancel-button'));
    expect(onCancel).toHaveBeenCalledOnce();

    await user.click(screen.getByTestId('back-button'));
    expect(onCancel).toHaveBeenCalledTimes(2);
  });

  // ================================================================
  // Fallback player name
  // ================================================================

  it('shows fallback name when playerNames not provided', () => {
    render(<ResumePhotoReview {...defaultProps} playerNames={{}} />);
    const fallbackNames = screen.getAllByText(/^Player player/);
    expect(fallbackNames.length).toBeGreaterThanOrEqual(1);
  });

  // ================================================================
  // Caption in expanded grid
  // ================================================================

  it('shows caption overlay on photos', async () => {
    const user = userEvent.setup();
    render(<ResumePhotoReview {...defaultProps} />);

    await user.click(screen.getByTestId('player-toggle-player-A'));

    expect(screen.getByText('Board overview')).toBeInTheDocument();
  });
});
