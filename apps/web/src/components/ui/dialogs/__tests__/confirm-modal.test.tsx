import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ConfirmModal } from '../confirm-modal';

const baseProps = {
  title: 'Conferma',
  message: 'Sei sicuro?',
  confirmLabel: 'Sì',
  cancelLabel: 'Annulla',
  onConfirm: () => {},
  onCancel: () => {},
};

describe('ConfirmModal (Issue #1464)', () => {
  it('renders title, message and both CTA labels when open', () => {
    render(<ConfirmModal {...baseProps} open />);
    expect(screen.getByText('Conferma')).toBeInTheDocument();
    expect(screen.getByText('Sei sicuro?')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sì' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Annulla' })).toBeInTheDocument();
  });

  it('fires onConfirm when the confirm button is clicked', () => {
    const onConfirm = vi.fn();
    render(<ConfirmModal {...baseProps} open onConfirm={onConfirm} />);
    fireEvent.click(screen.getByRole('button', { name: 'Sì' }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('fires onCancel when the cancel button is clicked', () => {
    const onCancel = vi.fn();
    render(<ConfirmModal {...baseProps} open onCancel={onCancel} />);
    fireEvent.click(screen.getByRole('button', { name: 'Annulla' }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });
});
