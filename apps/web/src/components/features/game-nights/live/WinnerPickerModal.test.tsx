import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { WinnerPickerModal } from './WinnerPickerModal';

const CANDIDATES = [
  { id: 'p1', displayName: 'Alice' },
  { id: 'p2', displayName: 'Guest Gina' },
];

describe('WinnerPickerModal', () => {
  it('renders nothing when closed', () => {
    render(
      <WinnerPickerModal
        open={false}
        candidates={CANDIDATES}
        onCancel={() => undefined}
        onConfirm={() => undefined}
      />
    );
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('lists the candidates + a "no winner" option', () => {
    render(
      <WinnerPickerModal
        open
        candidates={CANDIDATES}
        onCancel={() => undefined}
        onConfirm={() => undefined}
      />
    );
    expect(screen.getByRole('radio', { name: 'Alice' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Guest Gina' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: /Nessun vincitore/i })).toBeInTheDocument();
  });

  it('confirms with the selected participant id', async () => {
    const onConfirm = vi.fn();
    render(
      <WinnerPickerModal
        open
        candidates={CANDIDATES}
        onCancel={() => undefined}
        onConfirm={onConfirm}
      />
    );
    await userEvent.click(screen.getByRole('radio', { name: 'Guest Gina' }));
    await userEvent.click(screen.getByRole('button', { name: 'Completa' }));
    expect(onConfirm).toHaveBeenCalledWith('p2');
  });

  it('confirms with undefined for "no winner" (default selection)', async () => {
    const onConfirm = vi.fn();
    render(
      <WinnerPickerModal
        open
        candidates={CANDIDATES}
        onCancel={() => undefined}
        onConfirm={onConfirm}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: 'Completa' }));
    expect(onConfirm).toHaveBeenCalledWith(undefined);
  });

  it('surfaces an inline error message', () => {
    render(
      <WinnerPickerModal
        open
        candidates={CANDIDATES}
        errorMessage="Non è possibile completare ora."
        onCancel={() => undefined}
        onConfirm={() => undefined}
      />
    );
    expect(screen.getByRole('alert')).toHaveTextContent(/Non è possibile completare/i);
  });

  it('locks the buttons while pending', () => {
    render(
      <WinnerPickerModal
        open
        pending
        candidates={CANDIDATES}
        onCancel={() => undefined}
        onConfirm={() => undefined}
      />
    );
    expect(screen.getByRole('button', { name: /Completamento/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Annulla' })).toBeDisabled();
  });

  it('cancels via Annulla', async () => {
    const onCancel = vi.fn();
    render(
      <WinnerPickerModal
        open
        candidates={CANDIDATES}
        onCancel={onCancel}
        onConfirm={() => undefined}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: 'Annulla' }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });
});
