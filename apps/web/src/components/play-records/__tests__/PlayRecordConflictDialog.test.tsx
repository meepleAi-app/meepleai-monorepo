import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { PlayRecordConflictDialog } from '../PlayRecordConflictDialog';

const labels = {
  title: 'Modifica concorrente',
  description: 'Qualcuno ha modificato questa partita.',
  reload: 'Ricarica',
  overwrite: 'Sovrascrivi',
};

describe('PlayRecordConflictDialog', () => {
  it('fires onReload and onOverwrite', () => {
    const onReload = vi.fn();
    const onOverwrite = vi.fn();
    render(
      <PlayRecordConflictDialog
        open
        labels={labels}
        isOverwriting={false}
        onReload={onReload}
        onOverwrite={onOverwrite}
      />
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Ricarica' }));
    fireEvent.click(screen.getByRole('button', { name: 'Sovrascrivi' }));
    expect(onReload).toHaveBeenCalledOnce();
    expect(onOverwrite).toHaveBeenCalledOnce();
  });
});
