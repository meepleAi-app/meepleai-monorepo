/**
 * BulkRejectDialog Component Tests (Issue #2896)
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BulkRejectDialog } from '../BulkRejectDialog';

describe('BulkRejectDialog', () => {
  const mockOnOpenChange = vi.fn();
  const mockOnConfirm = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should not render when closed', () => {
    render(
      <BulkRejectDialog
        open={false}
        onOpenChange={mockOnOpenChange}
        selectedCount={3}
        onConfirm={mockOnConfirm}
      />
    );

    expect(screen.queryByTestId('bulk-reject-dialog')).not.toBeInTheDocument();
  });

  it('should render when open', () => {
    render(
      <BulkRejectDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        selectedCount={3}
        onConfirm={mockOnConfirm}
      />
    );

    expect(screen.getByTestId('bulk-reject-dialog')).toBeInTheDocument();
    expect(screen.getByText(/Conferma Rifiuto Multiplo/i)).toBeInTheDocument();
  });

  it('should display correct selected count', () => {
    render(
      <BulkRejectDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        selectedCount={5}
        onConfirm={mockOnConfirm}
      />
    );

    expect(screen.getByText(/5/)).toBeInTheDocument();
    expect(screen.getByText(/giochi/)).toBeInTheDocument();
  });

  it('should singularize label for 1 item', () => {
    render(
      <BulkRejectDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        selectedCount={1}
        onConfirm={mockOnConfirm}
      />
    );

    expect(screen.getByText(/1/)).toBeInTheDocument();
    expect(screen.getByText(/gioco/)).toBeInTheDocument();
  });

  it('should show validation error for reason < 10 characters', async () => {
    const user = userEvent.setup();

    render(
      <BulkRejectDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        selectedCount={2}
        onConfirm={mockOnConfirm}
      />
    );

    const textarea = screen.getByTestId('bulk-reject-reason');
    await user.type(textarea, 'Short'); // 5 characters

    const confirmButton = screen.getByTestId('bulk-reject-confirm');
    await user.click(confirmButton);

    await waitFor(() => {
      expect(screen.getByTestId('bulk-reject-error')).toBeInTheDocument();
      expect(screen.getByText(/almeno 10 caratteri/i)).toBeInTheDocument();
    });

    expect(mockOnConfirm).not.toHaveBeenCalled();
  });

  it('should accept reason >= 10 characters', async () => {
    const user = userEvent.setup();

    render(
      <BulkRejectDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        selectedCount={2}
        onConfirm={mockOnConfirm}
      />
    );

    const textarea = screen.getByTestId('bulk-reject-reason');
    await user.type(textarea, 'Valid reason with more than 10 chars');

    const confirmButton = screen.getByTestId('bulk-reject-confirm');
    await user.click(confirmButton);

    await waitFor(() => {
      expect(mockOnConfirm).toHaveBeenCalledWith('Valid reason with more than 10 chars');
    });
  });

  it('should disable confirm button while loading', () => {
    render(
      <BulkRejectDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        selectedCount={2}
        onConfirm={mockOnConfirm}
        isLoading={true}
      />
    );

    const confirmButton = screen.getByTestId('bulk-reject-confirm');
    expect(confirmButton).toBeDisabled();
    expect(confirmButton).toHaveTextContent(/in corso/i);
  });

  it('should clear error when user types', async () => {
    const user = userEvent.setup();

    render(
      <BulkRejectDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        selectedCount={2}
        onConfirm={mockOnConfirm}
      />
    );

    const textarea = screen.getByTestId('bulk-reject-reason');

    // Trigger error
    await user.type(textarea, 'Short');
    const confirmButton = screen.getByTestId('bulk-reject-confirm');
    await user.click(confirmButton);

    await waitFor(() => {
      expect(screen.getByTestId('bulk-reject-error')).toBeInTheDocument();
    });

    // Type more - error should clear
    await user.type(textarea, ' - now longer');

    expect(screen.queryByTestId('bulk-reject-error')).not.toBeInTheDocument();
  });

  it('should reset state when dialog closes', async () => {
    const user = userEvent.setup();
    const { rerender } = render(
      <BulkRejectDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        selectedCount={2}
        onConfirm={mockOnConfirm}
      />
    );

    const textarea = screen.getByTestId('bulk-reject-reason');
    await user.type(textarea, 'Some reason text');

    // Close dialog
    rerender(
      <BulkRejectDialog
        open={false}
        onOpenChange={mockOnOpenChange}
        selectedCount={2}
        onConfirm={mockOnConfirm}
      />
    );

    // Reopen dialog
    rerender(
      <BulkRejectDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        selectedCount={2}
        onConfirm={mockOnConfirm}
      />
    );

    // Textarea should be empty
    const newTextarea = screen.getByTestId('bulk-reject-reason');
    expect(newTextarea).toHaveValue('');
  });

  it('should show character counter', () => {
    render(
      <BulkRejectDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        selectedCount={2}
        onConfirm={mockOnConfirm}
      />
    );

    expect(screen.getByText(/0\/10 caratteri minimi/i)).toBeInTheDocument();
  });

  it('should update character counter as user types', async () => {
    const user = userEvent.setup();

    render(
      <BulkRejectDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        selectedCount={2}
        onConfirm={mockOnConfirm}
      />
    );

    const textarea = screen.getByTestId('bulk-reject-reason');
    await user.type(textarea, 'Hello'); // 5 characters

    expect(screen.getByText(/5\/10 caratteri minimi/i)).toBeInTheDocument();
  });
});
