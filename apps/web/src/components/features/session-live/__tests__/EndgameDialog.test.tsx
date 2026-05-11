/**
 * EndgameDialog unit tests — Wave D.2 Interactions sub-PR (Issue #750)
 *
 * Coverage:
 * - Render shape (data-slot, role="dialog", aria-modal)
 * - Final scores list rendered correctly
 * - Winner indicator shown
 * - onAcknowledge fires when CTA clicked
 * - ESC DISABLED: pressing Escape does NOT call onAcknowledge (intentional deviation)
 * - Focus trap: Tab cycles within dialog
 * - Shift+Tab wraps backward
 * - aria-labelledby links to title
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { EndgameDialogLabels, EndgameDialogProps, FinalScoreEntry } from '../EndgameDialog';
import { EndgameDialog } from '../EndgameDialog';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const LABELS: EndgameDialogLabels = {
  title: 'Sessione terminata',
  winnerLabel: 'Vincitore',
  acknowledgeCta: 'Conferma',
  viewSummaryCta: 'Vedi riepilogo',
};

const SCORES: ReadonlyArray<FinalScoreEntry> = [
  { playerName: 'Alice', score: 42, isWinner: true },
  { playerName: 'Bob', score: 28, isWinner: false },
  { playerName: 'Charlie', score: 15, isWinner: false },
];

function renderDialog(overrides: Partial<EndgameDialogProps> = {}) {
  const onAcknowledge = vi.fn();
  const props: EndgameDialogProps = {
    finalScores: SCORES,
    endedAt: '2026-05-06T11:00:00Z',
    endedBy: 'Alice',
    onAcknowledge,
    labels: LABELS,
    ...overrides,
  };
  const result = render(<EndgameDialog {...props} />);
  return { ...result, onAcknowledge };
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('EndgameDialog — render shape', () => {
  it('renders data-slot="endgame-dialog"', () => {
    renderDialog();
    expect(document.querySelector('[data-slot="endgame-dialog"]')).toBeInTheDocument();
  });

  it('renders role="dialog" aria-modal="true"', () => {
    renderDialog();
    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAttribute('aria-modal', 'true');
  });

  it('renders aria-labelledby pointing to title', () => {
    renderDialog();
    const dialog = screen.getByRole('dialog');
    const labelId = dialog.getAttribute('aria-labelledby');
    expect(labelId).toBeTruthy();
    const titleEl = document.getElementById(labelId!);
    expect(titleEl?.textContent).toContain('Sessione terminata');
  });

  it('renders dialog title', () => {
    renderDialog();
    expect(screen.getByText('Sessione terminata')).toBeInTheDocument();
  });

  it('renders endedBy info', () => {
    renderDialog({ endedBy: 'GameMaster' });
    expect(screen.getByText(/GameMaster/)).toBeInTheDocument();
  });

  it('renders Acknowledge CTA button', () => {
    renderDialog();
    expect(screen.getByRole('button', { name: 'Conferma' })).toBeInTheDocument();
  });
});

describe('EndgameDialog — final scores', () => {
  it('renders all player names', () => {
    renderDialog();
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('Charlie')).toBeInTheDocument();
  });

  it('renders all scores', () => {
    renderDialog();
    expect(screen.getByText('42')).toBeInTheDocument();
    expect(screen.getByText('28')).toBeInTheDocument();
    expect(screen.getByText('15')).toBeInTheDocument();
  });

  it('renders winner label for winner', () => {
    renderDialog();
    expect(screen.getByText('Vincitore')).toBeInTheDocument();
  });

  it('does not render winner label for non-winner', () => {
    // Only 1 winner badge should exist
    renderDialog();
    const winnerBadges = screen.getAllByText('Vincitore');
    expect(winnerBadges).toHaveLength(1);
  });

  it('handles empty scores gracefully', () => {
    renderDialog({ finalScores: [] });
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Conferma' })).toBeInTheDocument();
  });
});

describe('EndgameDialog — onAcknowledge', () => {
  it('calls onAcknowledge when Conferma button clicked', async () => {
    const user = userEvent.setup();
    const { onAcknowledge } = renderDialog();

    await user.click(screen.getByRole('button', { name: 'Conferma' }));
    expect(onAcknowledge).toHaveBeenCalledOnce();
  });
});

describe('EndgameDialog — ESC DISABLED (intentional WCAG deviation)', () => {
  it('does NOT call onAcknowledge when Escape pressed', async () => {
    const user = userEvent.setup();
    const { onAcknowledge } = renderDialog();

    const dialog = screen.getByRole('dialog');
    dialog.focus();
    await user.keyboard('{Escape}');

    // Critical: ESC must NOT dismiss the endgame dialog
    expect(onAcknowledge).not.toHaveBeenCalled();
  });

  it('dialog remains mounted after Escape press', async () => {
    const user = userEvent.setup();
    renderDialog();

    const dialog = screen.getByRole('dialog');
    dialog.focus();
    await user.keyboard('{Escape}');

    // Dialog still present
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Conferma' })).toBeInTheDocument();
  });
});

describe('EndgameDialog — focus trap', () => {
  it('focus trap: Tab cycles within dialog (wraps last → first)', async () => {
    const user = userEvent.setup();
    renderDialog();

    const dialog = screen.getByRole('dialog');
    const focusables = Array.from(dialog.querySelectorAll<HTMLElement>('button:not([disabled])'));
    expect(focusables.length).toBeGreaterThanOrEqual(1);

    // Focus last button and Tab → should wrap to first
    focusables[focusables.length - 1].focus();
    await user.keyboard('{Tab}');
    expect(document.activeElement).toBe(focusables[0]);
  });

  it('focus trap: Shift+Tab wraps first → last', async () => {
    const user = userEvent.setup();
    renderDialog();

    const dialog = screen.getByRole('dialog');
    const focusables = Array.from(dialog.querySelectorAll<HTMLElement>('button:not([disabled])'));
    expect(focusables.length).toBeGreaterThanOrEqual(1);

    focusables[0].focus();
    await user.keyboard('{Shift>}{Tab}{/Shift}');
    expect(document.activeElement).toBe(focusables[focusables.length - 1]);
  });
});
