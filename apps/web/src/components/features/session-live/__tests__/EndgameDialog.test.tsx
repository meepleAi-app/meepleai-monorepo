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
 * - #2501 SP4: photo upload section mounted above CTAs (Task 2)
 */

import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { EndgameDialogLabels, EndgameDialogProps, FinalScoreEntry } from '../EndgameDialog';
import { EndgameDialog } from '../EndgameDialog';

// ─── Mock EndgamePhotoUploadSection ───────────────────────────────────────────
// Mock so we can control onUploadingChange in isolation without needing
// real file inputs, hooks, or i18n setup.

let capturedOnUploadingChange: ((uploading: boolean) => void) | undefined;

vi.mock('../EndgamePhotoUploadSection', () => ({
  EndgamePhotoUploadSection: ({
    recordId,
    onUploadingChange,
  }: {
    recordId: string | null;
    onUploadingChange?: (uploading: boolean) => void;
    className?: string;
  }) => {
    capturedOnUploadingChange = onUploadingChange;
    return <div data-testid="endgame-photo-upload-section" data-record-id={recordId ?? 'null'} />;
  },
}));

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const LABELS: EndgameDialogLabels = {
  title: 'Sessione terminata',
  winnerLabel: 'Vincitore',
  acknowledgeCta: 'Conferma',
  viewSummaryCta: 'Vedi riepilogo',
  saveGameCta: 'Salva partita',
  savingLabel: 'Salvataggio...',
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

// ─── #2503 — Save game CTA ────────────────────────────────────────────────────

describe('EndgameDialog — #2503: save game CTA', () => {
  it('renders "Salva partita" button when onSave is passed', () => {
    renderDialog({ onSave: vi.fn() });
    expect(screen.getByRole('button', { name: 'Salva partita' })).toBeInTheDocument();
  });

  it('does NOT render save button when onSave is not passed', () => {
    renderDialog({ onSave: undefined });
    expect(screen.queryByRole('button', { name: 'Salva partita' })).not.toBeInTheDocument();
    expect(document.querySelector('[data-slot="endgame-save-cta"]')).not.toBeInTheDocument();
  });

  it('calls onSave when "Salva partita" is clicked', async () => {
    const user = userEvent.setup();
    const onSave = vi.fn();
    renderDialog({ onSave });
    await user.click(screen.getByRole('button', { name: 'Salva partita' }));
    expect(onSave).toHaveBeenCalledOnce();
  });

  it('button is disabled and aria-busy when saving=true', () => {
    renderDialog({ onSave: vi.fn(), saving: true });
    const saveBtn = document.querySelector('[data-slot="endgame-save-cta"]') as HTMLButtonElement;
    expect(saveBtn).toBeDisabled();
    expect(saveBtn).toHaveAttribute('aria-busy', 'true');
  });

  it('shows savingLabel text when saving=true', () => {
    renderDialog({ onSave: vi.fn(), saving: true });
    expect(screen.getByText('Salvataggio...')).toBeInTheDocument();
  });

  it('shows saveGameCta text when saving=false', () => {
    renderDialog({ onSave: vi.fn(), saving: false });
    expect(screen.getByRole('button', { name: 'Salva partita' })).toBeInTheDocument();
  });

  it('acknowledge CTA is still present alongside save CTA', () => {
    renderDialog({ onSave: vi.fn() });
    expect(screen.getByRole('button', { name: 'Conferma' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Salva partita' })).toBeInTheDocument();
  });
});

// ─── #2501 SP4 — Photo upload section (Task 2) ────────────────────────────────

describe('EndgameDialog — #2501 SP4: photo upload section', () => {
  beforeEach(() => {
    capturedOnUploadingChange = undefined;
  });

  it('renders_photo_section_above_ctas — EndgamePhotoUploadSection is rendered above the CTAs', () => {
    renderDialog({ onSave: vi.fn(), recordId: 'rec-123' });

    const photoSection = screen.getByTestId('endgame-photo-upload-section');
    expect(photoSection).toBeInTheDocument();

    // Verify ordering: photo section must appear before the save CTA in the DOM
    const saveBtn = screen.getByRole('button', { name: 'Salva partita' });
    const confirmBtn = screen.getByRole('button', { name: 'Conferma' });

    // compareDocumentPosition returns a bitmask; DOCUMENT_POSITION_FOLLOWING (4)
    // means saveBtn comes after photoSection in document order.
    expect(
      photoSection.compareDocumentPosition(saveBtn) & Node.DOCUMENT_POSITION_FOLLOWING
    ).toBeTruthy();
    expect(
      photoSection.compareDocumentPosition(confirmBtn) & Node.DOCUMENT_POSITION_FOLLOWING
    ).toBeTruthy();
  });

  it('disables_save_cta_while_photos_uploading — save CTA is disabled when onUploadingChange(true), re-enabled at (false)', async () => {
    renderDialog({ onSave: vi.fn(), recordId: 'rec-456' });

    const saveBtn = screen.getByRole('button', { name: 'Salva partita' });

    // Initially enabled
    expect(saveBtn).not.toBeDisabled();

    // Trigger uploading = true via the captured callback
    act(() => {
      capturedOnUploadingChange?.(true);
    });

    expect(saveBtn).toBeDisabled();

    // Trigger uploading = false
    act(() => {
      capturedOnUploadingChange?.(false);
    });

    expect(saveBtn).not.toBeDisabled();
  });

  it('save_cta_enabled_with_no_photos — save CTA remains enabled when no upload is in progress (AC-MEDIA-2)', () => {
    renderDialog({ onSave: vi.fn(), recordId: null });

    // Photo section present but no upload triggered → save CTA enabled
    expect(screen.getByTestId('endgame-photo-upload-section')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Salva partita' })).not.toBeDisabled();
  });
});
