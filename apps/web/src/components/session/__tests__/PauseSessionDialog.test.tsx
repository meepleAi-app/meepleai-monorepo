/**
 * Unit tests for PauseSessionDialog component (Issue #5372)
 * Epic #5358 - Session Photo Attachments (Phase 0)
 */

import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, cleanup, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PauseSessionDialog, type PauseSessionDialogProps } from '../PauseSessionDialog';

describe('PauseSessionDialog', () => {
  const defaultProps: PauseSessionDialogProps = {
    open: true,
    onOpenChange: vi.fn(),
    sessionId: 'session-123',
    playerId: 'player-456',
    onPause: vi.fn(),
  };

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  // ================================================================
  // Rendering
  // ================================================================

  it('renders dialog when open', () => {
    render(<PauseSessionDialog {...defaultProps} />);
    expect(screen.getByTestId('pause-session-dialog')).toBeInTheDocument();
  });

  it('does not render dialog when closed', () => {
    render(<PauseSessionDialog {...defaultProps} open={false} />);
    expect(screen.queryByTestId('pause-session-dialog')).not.toBeInTheDocument();
  });

  it('shows title text', () => {
    render(<PauseSessionDialog {...defaultProps} />);
    expect(screen.getByTestId('pause-dialog-title')).toHaveTextContent(
      'Save board state before pausing?'
    );
  });

  it('shows description text', () => {
    render(<PauseSessionDialog {...defaultProps} />);
    expect(
      screen.getByText(/Upload photos of the game board and player areas/)
    ).toBeInTheDocument();
  });

  it('shows Upload Photos button', () => {
    render(<PauseSessionDialog {...defaultProps} />);
    expect(screen.getByTestId('upload-photos-button')).toBeInTheDocument();
    expect(screen.getByText('Upload Photos')).toBeInTheDocument();
  });

  it('shows Skip & Pause button', () => {
    render(<PauseSessionDialog {...defaultProps} />);
    expect(screen.getByTestId('skip-pause-button')).toBeInTheDocument();
  });

  // ================================================================
  // Skip & Pause
  // ================================================================

  it('calls onPause and closes dialog when Skip & Pause clicked', async () => {
    const user = userEvent.setup();
    const onPause = vi.fn();
    const onOpenChange = vi.fn();
    render(<PauseSessionDialog {...defaultProps} onPause={onPause} onOpenChange={onOpenChange} />);

    await user.click(screen.getByTestId('skip-pause-button'));

    expect(onOpenChange).toHaveBeenCalledWith(false);
    expect(onPause).toHaveBeenCalled();
  });

  // ================================================================
  // Upload Photos flow
  // ================================================================

  it('opens PhotoUploadModal when Upload Photos clicked', async () => {
    const user = userEvent.setup();
    render(<PauseSessionDialog {...defaultProps} />);

    await user.click(screen.getByTestId('upload-photos-button'));

    expect(screen.getByTestId('photo-upload-modal')).toBeInTheDocument();
  });

  it('hides pause dialog when upload modal opens', async () => {
    const user = userEvent.setup();
    render(<PauseSessionDialog {...defaultProps} />);

    await user.click(screen.getByTestId('upload-photos-button'));

    // Pause dialog should be hidden (showUpload = true → open && !showUpload = false)
    expect(screen.queryByTestId('pause-session-dialog')).not.toBeInTheDocument();
    // Upload modal should be visible
    expect(screen.getByTestId('photo-upload-modal')).toBeInTheDocument();
  });

  it('returns to pause dialog when upload modal cancelled', async () => {
    const user = userEvent.setup();
    render(<PauseSessionDialog {...defaultProps} />);

    // Open upload modal
    await user.click(screen.getByTestId('upload-photos-button'));
    expect(screen.getByTestId('photo-upload-modal')).toBeInTheDocument();

    // Cancel upload
    await user.click(screen.getByTestId('cancel-button'));

    await waitFor(() => {
      expect(screen.getByTestId('pause-session-dialog')).toBeInTheDocument();
    });
  });

  // ================================================================
  // Mobile responsive
  // ================================================================

  it('dialog has max-w-sm for mobile-friendly sizing', () => {
    render(<PauseSessionDialog {...defaultProps} />);
    const dialog = screen.getByTestId('pause-session-dialog');
    expect(dialog.className).toContain('max-w-sm');
  });
});
