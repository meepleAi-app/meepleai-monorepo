/**
 * Unit tests for SessionPhotoGallery component (Issue #5369)
 * Epic #5358 - Session Photo Attachments (Phase 0)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, cleanup, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import {
  SessionPhotoGallery,
  type SessionPhotoGalleryProps,
  type SessionAttachmentDetail,
} from '../SessionPhotoGallery';
import type { SessionAttachmentDto } from '../PhotoUploadModal';

function makeAttachment(overrides: Partial<SessionAttachmentDto> = {}): SessionAttachmentDto {
  return {
    id: `att-${Math.random().toString(36).slice(2, 8)}`,
    sessionId: 'session-123',
    playerId: 'player-456',
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
  makeAttachment({ id: 'att-1', caption: 'Board overview', attachmentType: 'BoardState' }),
  makeAttachment({
    id: 'att-2',
    caption: 'My area',
    attachmentType: 'PlayerArea',
    playerId: 'player-789',
    snapshotIndex: 1,
  }),
  makeAttachment({ id: 'att-3', attachmentType: 'CharacterSheet' }),
];

describe('SessionPhotoGallery', () => {
  const defaultProps: SessionPhotoGalleryProps = {
    sessionId: 'session-123',
    attachments: sampleAttachments,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  // ================================================================
  // Rendering
  // ================================================================

  it('renders photo grid with all attachments', () => {
    render(<SessionPhotoGallery {...defaultProps} />);
    expect(screen.getByTestId('session-photo-gallery')).toBeInTheDocument();
    expect(screen.getByTestId('photo-grid')).toBeInTheDocument();
    expect(screen.getByTestId('photo-card-att-1')).toBeInTheDocument();
    expect(screen.getByTestId('photo-card-att-2')).toBeInTheDocument();
    expect(screen.getByTestId('photo-card-att-3')).toBeInTheDocument();
  });

  it('shows photo count', () => {
    render(<SessionPhotoGallery {...defaultProps} />);
    expect(screen.getByText('3 photos')).toBeInTheDocument();
  });

  it('shows singular "photo" for one item', () => {
    render(<SessionPhotoGallery {...defaultProps} attachments={[sampleAttachments[0]]} />);
    expect(screen.getByText('1 photo')).toBeInTheDocument();
  });

  // ================================================================
  // Empty state
  // ================================================================

  it('shows empty state when no attachments', () => {
    render(<SessionPhotoGallery {...defaultProps} attachments={[]} />);
    expect(screen.getByTestId('empty-state')).toBeInTheDocument();
    expect(screen.getByText('No photos yet')).toBeInTheDocument();
  });

  // ================================================================
  // Loading skeleton
  // ================================================================

  it('shows loading skeleton when isLoading', () => {
    render(<SessionPhotoGallery {...defaultProps} isLoading={true} />);
    expect(screen.getByTestId('gallery-skeleton')).toBeInTheDocument();
  });

  // ================================================================
  // Type badges
  // ================================================================

  it('displays type badges on cards', () => {
    render(<SessionPhotoGallery {...defaultProps} />);
    const badges = screen.getAllByTestId('type-badge');
    expect(badges).toHaveLength(3);
    expect(badges[0]).toHaveTextContent('Board');
    expect(badges[1]).toHaveTextContent('Player Area');
    expect(badges[2]).toHaveTextContent('Character');
  });

  // ================================================================
  // Caption
  // ================================================================

  it('shows caption on cards that have one', () => {
    render(<SessionPhotoGallery {...defaultProps} />);
    expect(screen.getByText('Board overview')).toBeInTheDocument();
    expect(screen.getByText('My area')).toBeInTheDocument();
  });

  // ================================================================
  // Filtering
  // ================================================================

  it('filters by attachment type', async () => {
    const user = userEvent.setup();
    render(<SessionPhotoGallery {...defaultProps} />);

    const filter = screen.getByTestId('type-filter');
    await user.selectOptions(filter, 'PlayerArea');

    expect(screen.getByText('1 photo')).toBeInTheDocument();
    expect(screen.getByTestId('photo-card-att-2')).toBeInTheDocument();
    expect(screen.queryByTestId('photo-card-att-1')).not.toBeInTheDocument();
  });

  it('shows filter empty message when no results', async () => {
    const user = userEvent.setup();
    render(<SessionPhotoGallery {...defaultProps} />);

    const filter = screen.getByTestId('type-filter');
    await user.selectOptions(filter, 'Custom');

    expect(screen.getByTestId('filter-empty')).toBeInTheDocument();
  });

  // ================================================================
  // Delete
  // ================================================================

  it('does not show delete button when readOnly', () => {
    render(
      <SessionPhotoGallery
        {...defaultProps}
        readOnly
        currentPlayerId="player-456"
        onDelete={vi.fn()}
      />
    );
    expect(screen.queryAllByTestId('delete-button')).toHaveLength(0);
  });

  it('shows delete button for own photos when onDelete provided', () => {
    render(
      <SessionPhotoGallery {...defaultProps} currentPlayerId="player-456" onDelete={vi.fn()} />
    );
    // att-1 and att-3 belong to player-456, att-2 to player-789
    const deleteButtons = screen.getAllByTestId('delete-button');
    expect(deleteButtons.length).toBe(2);
  });

  it('shows delete button for all photos when isHost', () => {
    render(
      <SessionPhotoGallery
        {...defaultProps}
        currentPlayerId="player-456"
        isHost
        onDelete={vi.fn()}
      />
    );
    const deleteButtons = screen.getAllByTestId('delete-button');
    expect(deleteButtons.length).toBe(3);
  });

  it('shows confirmation dialog on delete click', async () => {
    const user = userEvent.setup();
    render(
      <SessionPhotoGallery {...defaultProps} currentPlayerId="player-456" onDelete={vi.fn()} />
    );

    const deleteButtons = screen.getAllByTestId('delete-button');
    await user.click(deleteButtons[0]);

    expect(screen.getByText('Delete photo?')).toBeInTheDocument();
  });

  it('calls onDelete after confirmation', async () => {
    const user = userEvent.setup();
    const onDelete = vi.fn();
    render(
      <SessionPhotoGallery {...defaultProps} currentPlayerId="player-456" onDelete={onDelete} />
    );

    const deleteButtons = screen.getAllByTestId('delete-button');
    await user.click(deleteButtons[0]);

    await user.click(screen.getByTestId('delete-confirm'));

    expect(onDelete).toHaveBeenCalledWith('att-1');
  });

  it('cancels delete on cancel click', async () => {
    const user = userEvent.setup();
    const onDelete = vi.fn();
    render(
      <SessionPhotoGallery {...defaultProps} currentPlayerId="player-456" onDelete={onDelete} />
    );

    const deleteButtons = screen.getAllByTestId('delete-button');
    await user.click(deleteButtons[0]);

    await user.click(screen.getByTestId('delete-cancel'));

    expect(onDelete).not.toHaveBeenCalled();
  });

  // ================================================================
  // Lightbox
  // ================================================================

  it('opens lightbox on photo click', async () => {
    const user = userEvent.setup();
    render(<SessionPhotoGallery {...defaultProps} />);

    const card1 = screen.getByTestId('photo-card-att-1');
    const clickable = card1.querySelector('[role="button"]')!;
    await user.click(clickable);

    expect(screen.getByTestId('lightbox')).toBeInTheDocument();
    expect(screen.getByTestId('lightbox-image')).toBeInTheDocument();
  });

  it('closes lightbox on close button click', async () => {
    const user = userEvent.setup();
    render(<SessionPhotoGallery {...defaultProps} />);

    // Open lightbox
    const card1 = screen.getByTestId('photo-card-att-1');
    const clickable = card1.querySelector('[role="button"]')!;
    await user.click(clickable);

    expect(screen.getByTestId('lightbox')).toBeInTheDocument();

    // Close
    await user.click(screen.getByTestId('lightbox-close'));

    await waitFor(() => {
      expect(screen.queryByTestId('lightbox')).not.toBeInTheDocument();
    });
  });

  it('shows navigation arrows in lightbox', async () => {
    const user = userEvent.setup();
    render(<SessionPhotoGallery {...defaultProps} />);

    // Open first photo
    const card1 = screen.getByTestId('photo-card-att-1');
    const clickable = card1.querySelector('[role="button"]')!;
    await user.click(clickable);

    // First photo: no prev, has next
    expect(screen.queryByTestId('lightbox-prev')).not.toBeInTheDocument();
    expect(screen.getByTestId('lightbox-next')).toBeInTheDocument();
  });

  it('fetches detail on lightbox open when onFetchDetail provided', async () => {
    const user = userEvent.setup();
    const mockDetail: SessionAttachmentDetail = {
      ...sampleAttachments[0],
      downloadUrl: 'https://presigned-url.example.com/photo.jpg',
      playerDisplayName: 'Player One',
    };
    const onFetchDetail = vi.fn().mockResolvedValue(mockDetail);

    render(<SessionPhotoGallery {...defaultProps} onFetchDetail={onFetchDetail} />);

    const card1 = screen.getByTestId('photo-card-att-1');
    const clickable = card1.querySelector('[role="button"]')!;
    await user.click(clickable);

    await waitFor(() => {
      expect(onFetchDetail).toHaveBeenCalledWith('att-1');
    });

    await waitFor(() => {
      expect(screen.getByTestId('download-link')).toBeInTheDocument();
    });
  });

  // ================================================================
  // Snapshot index display
  // ================================================================

  it('shows snapshot index on card when present', () => {
    render(<SessionPhotoGallery {...defaultProps} />);
    expect(screen.getByText('Snap #1')).toBeInTheDocument();
  });
});
