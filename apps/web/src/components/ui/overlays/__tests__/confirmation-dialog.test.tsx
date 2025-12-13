/**
 * ConfirmationDialog Tests - Issue #903 Enhancement
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ConfirmationDialog } from '../confirmation-dialog';

describe('ConfirmationDialog', () => {
  const onClose = vi.fn();
  const onConfirm = vi.fn();

  const defaultProps = {
    isOpen: true,
    onClose,
    onConfirm,
    title: 'Confirm Action',
    message: 'Are you sure?',
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render with title and message', () => {
    render(<ConfirmationDialog {...defaultProps} />);

    expect(screen.getByText('Confirm Action')).toBeInTheDocument();
    expect(screen.getByText('Are you sure?')).toBeInTheDocument();
  });

  it('should call onConfirm and onClose when confirm button is clicked', async () => {
    const user = userEvent.setup();
    render(<ConfirmationDialog {...defaultProps} />);

    const confirmButton = screen.getByRole('button', { name: /confirm/i });
    await user.click(confirmButton);

    expect(onConfirm).toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
  });

  it('should call onClose when cancel button is clicked', async () => {
    const user = userEvent.setup();
    render(<ConfirmationDialog {...defaultProps} />);

    const cancelButton = screen.getByRole('button', { name: /cancel/i });
    await user.click(cancelButton);

    expect(onClose).toHaveBeenCalled();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('should render destructive variant with alert icon', () => {
    render(<ConfirmationDialog {...defaultProps} variant="destructive" />);

    const confirmButton = screen.getByRole('button', { name: /confirm/i });
    // Check that it has destructive styling (bg-destructive class)
    expect(confirmButton.className).toContain('bg-destructive');
  });

  it('should render custom button text', () => {
    render(<ConfirmationDialog {...defaultProps} confirmText="Delete" cancelText="Go Back" />);

    expect(screen.getByRole('button', { name: /delete/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /go back/i })).toBeInTheDocument();
  });

  it('should show loading state', () => {
    render(<ConfirmationDialog {...defaultProps} isLoading />);

    expect(screen.getByText('Processing...')).toBeInTheDocument();
  });

  it('should not render when closed', () => {
    render(<ConfirmationDialog {...defaultProps} isOpen={false} />);

    expect(screen.queryByText('Confirm Action')).not.toBeInTheDocument();
  });
});
