import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

import { DraftAutosaveIndicator } from '../DraftAutosaveIndicator';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) =>
      ({
        'playRecords.new.draft.saving': 'Salvataggio…',
        'playRecords.new.draft.saved': 'Bozza salvata {time}',
      })[key] ?? key,
  }),
}));

describe('DraftAutosaveIndicator', () => {
  it('renders nothing on the pristine state (not pending, never saved)', () => {
    const { container } = render(<DraftAutosaveIndicator isPending={false} lastSavedAt={null} />);
    expect(container.firstChild).toBeNull();
  });

  it('shows the saving label while a save is pending', () => {
    render(<DraftAutosaveIndicator isPending lastSavedAt={null} />);
    const el = screen.getByTestId('draft-autosave-indicator');
    expect(el).toHaveAttribute('role', 'status');
    expect(el).toHaveTextContent('Salvataggio…');
  });

  it('shows the saved label with the interpolated time when not pending', () => {
    const ts = new Date('2026-06-20T18:05:00.000Z').getTime();
    render(<DraftAutosaveIndicator isPending={false} lastSavedAt={ts} />);
    const el = screen.getByTestId('draft-autosave-indicator');
    expect(el).toHaveTextContent(/Bozza salvata \d{2}:\d{2}/);
    expect(el).not.toHaveTextContent('{time}');
  });
});
