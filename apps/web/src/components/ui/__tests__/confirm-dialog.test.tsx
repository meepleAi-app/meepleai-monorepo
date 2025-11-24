/**
 * ConfirmDialog component tests
 * Issue #1435 - Replace window.confirm with custom dialog
 */

import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ConfirmDialog } from '../confirm-dialog';

describe('ConfirmDialog', () => {
  const defaultProps = {
    open: true,
    onOpenChange: vi.fn(),
    title: 'Confirm Action',
    message: 'Are you sure you want to proceed?',
    onConfirm: vi.fn(),
    onCancel: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render with title and message', () => {
    render(<ConfirmDialog {...defaultProps} />);

    expect(screen.getByText('Confirm Action')).toBeInTheDocument();
    expect(screen.getByText('Are you sure you want to proceed?')).toBeInTheDocument();
  });

  it('should render default buttons', () => {
    render(<ConfirmDialog {...defaultProps} />);

    expect(screen.getByRole('button', { name: /confirm/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
  });

  it('should render custom button text', () => {
    render(
      <ConfirmDialog
        {...defaultProps}
        confirmText="Delete"
        cancelText="Keep"
      />
    );

    expect(screen.getByRole('button', { name: /delete/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /keep/i })).toBeInTheDocument();
  });

  it('should call onConfirm and close dialog when confirm button is clicked', async () => {
    render(<ConfirmDialog {...defaultProps} />);

    const confirmButton = screen.getByRole('button', { name: /confirm/i });
    fireEvent.click(confirmButton);

    expect(defaultProps.onConfirm).toHaveBeenCalledTimes(1);
    expect(defaultProps.onOpenChange).toHaveBeenCalledWith(false);
  });

  it('should call onCancel and close dialog when cancel button is clicked', async () => {
    render(<ConfirmDialog {...defaultProps} />);

    const cancelButton = screen.getByRole('button', { name: /cancel/i });
    fireEvent.click(cancelButton);

    expect(defaultProps.onCancel).toHaveBeenCalledTimes(1);
    expect(defaultProps.onOpenChange).toHaveBeenCalledWith(false);
  });

  it('should show destructive variant with warning icon', () => {
    render(<ConfirmDialog {...defaultProps} variant="destructive" />);

    // Warning icon should be present for destructive variant
    const icon = screen.getByText('Confirm Action').parentElement?.querySelector('svg');
    expect(icon).toBeInTheDocument();
  });

  it('should not show warning icon for default variant', () => {
    render(<ConfirmDialog {...defaultProps} variant="default" />);

    // Warning icon should not be present for default variant
    const icon = screen.getByText('Confirm Action').parentElement?.querySelector('svg');
    expect(icon).toBeNull();
  });

  it('should not render when open is false', () => {
    render(<ConfirmDialog {...defaultProps} open={false} />);

    // Dialog content should not be visible
    expect(screen.queryByText('Confirm Action')).not.toBeInTheDocument();
  });

  it('should handle keyboard navigation (Escape key)', async () => {
    render(<ConfirmDialog {...defaultProps} />);

    // Simulate Escape key press on the dialog
    const dialog = screen.getByRole('dialog');
    fireEvent.keyDown(dialog, { key: 'Escape', code: 'Escape' });

    await waitFor(() => {
      expect(defaultProps.onOpenChange).toHaveBeenCalledWith(false);
    });
  });

  it('should work without onCancel callback', () => {
    const propsWithoutOnCancel = {
      ...defaultProps,
      onCancel: undefined,
    };

    render(<ConfirmDialog {...propsWithoutOnCancel} />);

    const cancelButton = screen.getByRole('button', { name: /cancel/i });
    fireEvent.click(cancelButton);

    // Should still close the dialog
    expect(defaultProps.onOpenChange).toHaveBeenCalledWith(false);
    // Should not throw error
  });

  it('should apply correct button variants', () => {
    const { rerender } = render(<ConfirmDialog {...defaultProps} variant="default" />);

    const confirmButton = screen.getByRole('button', { name: /confirm/i });
    expect(confirmButton).toHaveClass('bg-primary'); // Default variant

    rerender(<ConfirmDialog {...defaultProps} variant="destructive" />);

    const destructiveConfirmButton = screen.getByRole('button', { name: /confirm/i });
    expect(destructiveConfirmButton).toHaveClass('bg-destructive'); // Destructive variant
  });

  it('should be accessible with proper ARIA attributes', () => {
    render(<ConfirmDialog {...defaultProps} />);

    const dialog = screen.getByRole('dialog');
    expect(dialog).toBeInTheDocument();

    const title = screen.getByText('Confirm Action');
    expect(title).toBeInTheDocument();

    const description = screen.getByText('Are you sure you want to proceed?');
    expect(description).toBeInTheDocument();
  });
});
