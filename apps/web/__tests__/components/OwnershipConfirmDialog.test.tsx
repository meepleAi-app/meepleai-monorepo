/**
 * Tests for OwnershipConfirmDialog
 *
 * Verifies copyright-aware ownership confirmation before adding a game
 * to the user's library and enabling Knowledge Base access.
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { OwnershipConfirmDialog } from '@/components/dialogs/OwnershipConfirmDialog';

describe('OwnershipConfirmDialog', () => {
  const defaultProps = {
    open: true,
    onOpenChange: vi.fn(),
    gameTitle: 'Wingspan',
    onConfirm: vi.fn(),
    confirming: false,
  };

  it('renders game title when open', () => {
    render(<OwnershipConfirmDialog {...defaultProps} />);

    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('shows copyright and knowledge base motivation text', () => {
    render(<OwnershipConfirmDialog {...defaultProps} />);

    const description = screen.getByRole('alertdialog') ? document.body : document.body;

    // Check for key terms in the description (case-insensitive)
    const bodyText = document.body.textContent?.toLowerCase() ?? '';
    expect(bodyText).toContain('knowledge base');
    expect(bodyText).toContain('copyright');
  });

  it('calls onConfirm when confirm button is clicked', async () => {
    const user = userEvent.setup();
    const mockConfirm = vi.fn();

    render(<OwnershipConfirmDialog {...defaultProps} onConfirm={mockConfirm} />);

    await user.click(screen.getByRole('button', { name: /possiedo il gioco/i }));

    expect(mockConfirm).toHaveBeenCalledTimes(1);
  });

  it('disables confirm button while confirming', () => {
    render(<OwnershipConfirmDialog {...defaultProps} confirming={true} />);

    const confirmButton = screen.getByRole('button', { name: /aggiunta in corso/i });
    expect(confirmButton).toBeDisabled();
  });

  it('shows loading text on confirm button when confirming', () => {
    render(<OwnershipConfirmDialog {...defaultProps} confirming={true} />);

    expect(screen.getByText('Aggiunta in corso...')).toBeInTheDocument();
    expect(screen.queryByText('Possiedo il gioco')).not.toBeInTheDocument();
  });

  it('shows default confirm button text when not confirming', () => {
    render(<OwnershipConfirmDialog {...defaultProps} confirming={false} />);

    expect(screen.getByText('Possiedo il gioco')).toBeInTheDocument();
  });

  it('has cancel button', () => {
    render(<OwnershipConfirmDialog {...defaultProps} />);

    expect(screen.getByRole('button', { name: /annulla/i })).toBeInTheDocument();
  });

  it('does not render content when closed', () => {
    render(<OwnershipConfirmDialog {...defaultProps} open={false} />);

    expect(screen.queryByText('Wingspan')).not.toBeInTheDocument();
  });
});
