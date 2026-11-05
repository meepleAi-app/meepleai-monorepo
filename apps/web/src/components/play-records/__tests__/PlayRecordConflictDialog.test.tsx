import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { PlayRecordConflictDialog } from '../PlayRecordConflictDialog';

const labels = {
  title: 'Modifica concorrente',
  description: 'Qualcuno ha modificato questa partita.',
  reload: 'Ricarica',
  overwrite: 'Sovrascrivi',
  continueEditing: 'Continua a modificare',
};

function renderDialog(props: Partial<React.ComponentProps<typeof PlayRecordConflictDialog>> = {}) {
  const handlers = { onReload: vi.fn(), onOverwrite: vi.fn(), onContinueEditing: vi.fn() };
  render(
    <PlayRecordConflictDialog open labels={labels} isOverwriting={false} {...handlers} {...props} />
  );
  return handlers;
}

describe('PlayRecordConflictDialog', () => {
  it('fires onReload, onOverwrite and onContinueEditing from their own buttons', () => {
    const { onReload, onOverwrite, onContinueEditing } = renderDialog();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Ricarica' }));
    fireEvent.click(screen.getByRole('button', { name: 'Sovrascrivi' }));
    fireEvent.click(screen.getByRole('button', { name: 'Continua a modificare' }));
    expect(onReload).toHaveBeenCalledOnce();
    expect(onOverwrite).toHaveBeenCalledOnce();
    expect(onContinueEditing).toHaveBeenCalledOnce();
  });

  it('treats ESC as «Continua a modificare», and nothing else', async () => {
    const { onReload, onOverwrite, onContinueEditing } = renderDialog();

    await userEvent.setup().keyboard('{Escape}');

    expect(onContinueEditing).toHaveBeenCalledOnce();
    expect(onReload).not.toHaveBeenCalled();
    expect(onOverwrite).not.toHaveBeenCalled();
  });

  it('turns every way out off while a read or a save is in flight', async () => {
    const { onContinueEditing } = renderDialog({ isOverwriting: true });

    await userEvent.setup().keyboard('{Escape}');

    expect(onContinueEditing).not.toHaveBeenCalled();
    for (const name of ['Ricarica', 'Sovrascrivi', 'Continua a modificare']) {
      expect(screen.getByRole('button', { name })).toBeDisabled();
    }
  });
});
