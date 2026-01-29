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

  it('should display correct selected count in description', () => {
    render(
      <BulkRejectDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        selectedCount={5}
        onConfirm={mockOnConfirm}
      />
    );

    // Check DialogDescription for count
    expect(screen.getByText(/Stai per rifiutare/i)).toBeInTheDocument();
    // Verify the confirm button shows the count
    const confirmButton = screen.getByTestId('bulk-reject-confirm');
    expect(confirmButton).toHaveTextContent('Rifiuta 5 Giochi');
  });

  it('should singularize label for 1 item in confirm button', () => {
    render(
      <BulkRejectDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        selectedCount={1}
        onConfirm={mockOnConfirm}
      />
    );

    const confirmButton = screen.getByTestId('bulk-reject-confirm');
    expect(confirmButton).toHaveTextContent('Rifiuta 1 Giochi'); // Verify button text
  });

  it('should disable confirm button when reason < 10 characters', async () => {
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
    const confirmButton = screen.getByTestId('bulk-reject-confirm');

    // Initially disabled (empty)
    expect(confirmButton).toBeDisabled();

    // Type short text (5 characters)
    await user.type(textarea, 'Short');

    // Still disabled
    expect(confirmButton).toBeDisabled();
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

  it('should enable confirm button when reason >= 10 characters', async () => {
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
    const confirmButton = screen.getByTestId('bulk-reject-confirm');

    // Initially disabled
    expect(confirmButton).toBeDisabled();

    // Type enough text (15 characters)
    await user.type(textarea, 'Valid reason ok');

    // Now enabled
    expect(confirmButton).toBeEnabled();
  });

  it('should call onOpenChange when cancel button clicked', async () => {
    const user = userEvent.setup();
    render(
      <BulkRejectDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        selectedCount={2}
        onConfirm={mockOnConfirm}
      />
    );

    const cancelButton = screen.getByTestId('bulk-reject-cancel');
    await user.click(cancelButton);

    expect(mockOnOpenChange).toHaveBeenCalledWith(false);
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
