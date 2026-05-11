/**
 * PauseOverlay unit tests — Wave D.2 Interactions sub-PR (Issue #750)
 *
 * Coverage:
 * - Render shape (data-slot, role="dialog", aria-modal)
 * - aria-labelledby links to title
 * - ESC key closes dialog (calls onClose)
 * - Close button calls onClose
 * - Resume CTA: visible for Host only
 * - onResume called when Host clicks Resume
 * - Focus trap: Tab cycles within dialog
 * - Shift+Tab wraps backward
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { PauseOverlayLabels, PauseOverlayProps } from '../PauseOverlay';
import { PauseOverlay } from '../PauseOverlay';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const LABELS: PauseOverlayLabels = {
  title: 'Sessione in pausa',
  resumeCta: 'Riprendi',
  closeCta: 'Chiudi',
  closeAriaLabel: 'Chiudi overlay pausa',
};

function renderOverlay(overrides: Partial<PauseOverlayProps> = {}) {
  const onClose = vi.fn();
  const onResume = vi.fn();
  const props: PauseOverlayProps = {
    pausedBy: 'Alice',
    pausedAt: '2026-05-06T10:00:00Z',
    viewerRole: 'Player',
    onClose,
    onResume,
    labels: LABELS,
    ...overrides,
  };
  const result = render(<PauseOverlay {...props} />);
  return { ...result, onClose, onResume };
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('PauseOverlay — render shape', () => {
  it('renders data-slot="pause-overlay"', () => {
    renderOverlay();
    expect(document.querySelector('[data-slot="pause-overlay"]')).toBeInTheDocument();
  });

  it('renders role="dialog" aria-modal="true"', () => {
    renderOverlay();
    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAttribute('aria-modal', 'true');
  });

  it('renders aria-labelledby pointing to title', () => {
    renderOverlay();
    const dialog = screen.getByRole('dialog');
    const labelId = dialog.getAttribute('aria-labelledby');
    expect(labelId).toBeTruthy();
    const titleEl = document.getElementById(labelId!);
    expect(titleEl?.textContent).toBe('Sessione in pausa');
  });

  it('renders dialog title', () => {
    renderOverlay();
    expect(screen.getByText('Sessione in pausa')).toBeInTheDocument();
  });

  it('renders close button', () => {
    renderOverlay();
    expect(screen.getByRole('button', { name: 'Chiudi overlay pausa' })).toBeInTheDocument();
  });

  it('renders pausedBy info', () => {
    renderOverlay({ pausedBy: 'Bob' });
    expect(screen.getByText(/Bob/)).toBeInTheDocument();
  });
});

describe('PauseOverlay — role variant matrix', () => {
  it('shows Resume CTA for Host', () => {
    renderOverlay({ viewerRole: 'Host' });
    expect(screen.getByRole('button', { name: 'Riprendi' })).toBeInTheDocument();
  });

  it('hides Resume CTA for Player', () => {
    renderOverlay({ viewerRole: 'Player' });
    expect(screen.queryByRole('button', { name: 'Riprendi' })).not.toBeInTheDocument();
  });

  it('hides Resume CTA for Spectator', () => {
    renderOverlay({ viewerRole: 'Spectator' });
    expect(screen.queryByRole('button', { name: 'Riprendi' })).not.toBeInTheDocument();
  });

  it('data-viewer-role attribute matches role', () => {
    renderOverlay({ viewerRole: 'Host' });
    expect(document.querySelector('[data-viewer-role="Host"]')).toBeInTheDocument();
  });
});

describe('PauseOverlay — ESC closes dialog', () => {
  it('calls onClose when ESC is pressed', async () => {
    const user = userEvent.setup();
    const { onClose } = renderOverlay();

    const dialog = screen.getByRole('dialog');
    dialog.focus();
    await user.keyboard('{Escape}');

    expect(onClose).toHaveBeenCalledOnce();
  });
});

describe('PauseOverlay — close button', () => {
  it('calls onClose when close button clicked', async () => {
    const user = userEvent.setup();
    const { onClose } = renderOverlay();

    await user.click(screen.getByRole('button', { name: 'Chiudi overlay pausa' }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('calls onClose when Chiudi button clicked', async () => {
    const user = userEvent.setup();
    const { onClose } = renderOverlay();

    await user.click(screen.getByRole('button', { name: 'Chiudi' }));
    expect(onClose).toHaveBeenCalledOnce();
  });
});

describe('PauseOverlay — onResume', () => {
  it('calls onResume when Host clicks Resume CTA', async () => {
    const user = userEvent.setup();
    const { onResume } = renderOverlay({ viewerRole: 'Host' });

    await user.click(screen.getByRole('button', { name: 'Riprendi' }));
    expect(onResume).toHaveBeenCalledOnce();
  });
});

describe('PauseOverlay — focus trap', () => {
  it('focus trap: Tab cycles within dialog (wraps last → first)', async () => {
    const user = userEvent.setup();
    renderOverlay({ viewerRole: 'Host' });

    // Get all focusable elements in dialog
    const dialog = screen.getByRole('dialog');
    const focusables = Array.from(dialog.querySelectorAll<HTMLElement>('button:not([disabled])'));
    expect(focusables.length).toBeGreaterThanOrEqual(2);

    // Focus last button and Tab → should wrap to first
    focusables[focusables.length - 1].focus();
    expect(document.activeElement).toBe(focusables[focusables.length - 1]);

    await user.keyboard('{Tab}');
    // After wrap, focus should be on first focusable
    expect(document.activeElement).toBe(focusables[0]);
  });

  it('focus trap: Shift+Tab wraps first → last', async () => {
    const user = userEvent.setup();
    renderOverlay({ viewerRole: 'Host' });

    const dialog = screen.getByRole('dialog');
    const focusables = Array.from(dialog.querySelectorAll<HTMLElement>('button:not([disabled])'));
    expect(focusables.length).toBeGreaterThanOrEqual(2);

    // Focus first button and Shift+Tab → should wrap to last
    focusables[0].focus();
    await user.keyboard('{Shift>}{Tab}{/Shift}');
    expect(document.activeElement).toBe(focusables[focusables.length - 1]);
  });
});
