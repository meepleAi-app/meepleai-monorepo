/**
 * Unit tests for CameraToolContent component (Issue #5371)
 * Epic #5358 - Session Photo Attachments (Phase 0)
 */

import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CameraToolContent, type CameraToolContentProps } from '../CameraToolContent';
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

describe('CameraToolContent', () => {
  const defaultProps: CameraToolContentProps = {
    sessionId: 'session-123',
    playerId: 'player-456',
    attachments: [],
  };

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  // ================================================================
  // Rendering
  // ================================================================

  it('renders with test id', () => {
    render(<CameraToolContent {...defaultProps} />);
    expect(screen.getByTestId('camera-tool-content')).toBeInTheDocument();
  });

  it('renders header with title', () => {
    render(<CameraToolContent {...defaultProps} />);
    expect(screen.getByText('Session Photos')).toBeInTheDocument();
  });

  it('renders upload button', () => {
    render(<CameraToolContent {...defaultProps} />);
    expect(screen.getByTestId('upload-photo-button')).toBeInTheDocument();
    expect(screen.getByText('Upload')).toBeInTheDocument();
  });

  // ================================================================
  // Gallery integration
  // ================================================================

  it('shows empty state when no attachments', () => {
    render(<CameraToolContent {...defaultProps} />);
    expect(screen.getByTestId('empty-state')).toBeInTheDocument();
  });

  it('shows photo gallery with attachments', () => {
    const attachments = [makeAttachment({ id: 'att-1' }), makeAttachment({ id: 'att-2' })];
    render(<CameraToolContent {...defaultProps} attachments={attachments} />);
    expect(screen.getByTestId('session-photo-gallery')).toBeInTheDocument();
    expect(screen.getByTestId('photo-card-att-1')).toBeInTheDocument();
    expect(screen.getByTestId('photo-card-att-2')).toBeInTheDocument();
  });

  it('shows loading skeleton when isLoading', () => {
    render(<CameraToolContent {...defaultProps} isLoading />);
    expect(screen.getByTestId('gallery-skeleton')).toBeInTheDocument();
  });

  // ================================================================
  // Upload modal
  // ================================================================

  it('opens upload modal when upload button clicked', async () => {
    const user = userEvent.setup();
    render(<CameraToolContent {...defaultProps} />);

    await user.click(screen.getByTestId('upload-photo-button'));

    expect(screen.getByTestId('photo-upload-modal')).toBeInTheDocument();
  });

  it('does not show upload modal initially', () => {
    render(<CameraToolContent {...defaultProps} />);
    expect(screen.queryByTestId('photo-upload-modal')).not.toBeInTheDocument();
  });

  // ================================================================
  // Delete integration
  // ================================================================

  it('passes onDelete to gallery', () => {
    const onDelete = vi.fn();
    const attachments = [makeAttachment({ id: 'att-1' })];
    render(<CameraToolContent {...defaultProps} attachments={attachments} onDelete={onDelete} />);
    // When onDelete is provided, delete buttons should appear for own photos
    expect(screen.getByTestId('delete-button')).toBeInTheDocument();
  });

  // ================================================================
  // Host mode
  // ================================================================

  it('allows host to delete any photo', () => {
    const onDelete = vi.fn();
    const attachments = [makeAttachment({ id: 'att-1', playerId: 'other-player' })];
    render(
      <CameraToolContent {...defaultProps} attachments={attachments} isHost onDelete={onDelete} />
    );
    expect(screen.getByTestId('delete-button')).toBeInTheDocument();
  });
});
