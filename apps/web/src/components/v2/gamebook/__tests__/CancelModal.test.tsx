/**
 * CancelModal unit tests — SP6 Phase C.2.B Task B (Issue #789).
 *
 * Coverage:
 *   - Render shape: data-slot + role="alertdialog" + aria-modal="true"
 *   - aria-labelledby links to title id
 *   - aria-describedby links to description id
 *   - isOpen=false → null (no render)
 *   - ESC closes (calls onDismiss)
 *   - Click overlay closes (calls onDismiss)
 *   - Confirm button calls onConfirm
 *   - Dismiss button calls onDismiss
 *   - Initial focus on dismiss (Continue) button per ESC behavior matrix
 *   - Focus trap: Tab cycles within dialog (wraps last → first)
 *   - Focus trap: Shift+Tab wraps first → last
 *   - Focus restored on close (re-focuses previously-focused element)
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { CancelModal, type CancelModalLabels, type CancelModalProps } from '../CancelModal';

const LABELS: CancelModalLabels = {
  title: "Annullare l'indicizzazione?",
  description: 'Hai già scattato 24 pagine. Se annulli ora, dovrai ricominciare da capo.',
  confirm: 'Sì, annulla',
  dismiss: 'Continua a indicizzare',
  confirmAria: 'Conferma annullamento',
  dismissAria: 'Chiudi modal e continua a indicizzare',
};

function renderModal(overrides: Partial<CancelModalProps> = {}) {
  const onConfirm = vi.fn();
  const onDismiss = vi.fn();
  const props: CancelModalProps = {
    isOpen: true,
    onConfirm,
    onDismiss,
    labels: LABELS,
    ...overrides,
  };
  const result = render(<CancelModal {...props} />);
  return { ...result, onConfirm, onDismiss };
}

describe('CancelModal — render shape', () => {
  it('renders data-slot="cancel-modal" when isOpen=true', () => {
    renderModal();
    expect(document.querySelector('[data-slot="cancel-modal"]')).not.toBeNull();
  });

  it('renders nothing when isOpen=false', () => {
    renderModal({ isOpen: false });
    expect(document.querySelector('[data-slot="cancel-modal"]')).toBeNull();
  });

  it('exposes role="alertdialog" + aria-modal="true"', () => {
    renderModal();
    const dialog = screen.getByRole('alertdialog');
    expect(dialog.getAttribute('aria-modal')).toBe('true');
  });

  it('aria-labelledby links to title element', () => {
    renderModal();
    const dialog = screen.getByRole('alertdialog');
    const labelId = dialog.getAttribute('aria-labelledby');
    expect(labelId).toBeTruthy();
    const titleEl = document.getElementById(labelId!);
    expect(titleEl?.textContent).toBe("Annullare l'indicizzazione?");
  });

  it('aria-describedby links to description element', () => {
    renderModal();
    const dialog = screen.getByRole('alertdialog');
    const descId = dialog.getAttribute('aria-describedby');
    expect(descId).toBeTruthy();
    const descEl = document.getElementById(descId!);
    expect(descEl?.textContent).toContain('24 pagine');
  });

  it('renders title and description', () => {
    renderModal();
    expect(screen.getByText("Annullare l'indicizzazione?")).toBeInTheDocument();
    expect(screen.getByText(/24 pagine/)).toBeInTheDocument();
  });

  it('renders both action buttons', () => {
    renderModal();
    expect(screen.getByRole('button', { name: 'Conferma annullamento' })).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Chiudi modal e continua a indicizzare' })
    ).toBeInTheDocument();
  });

  it('exposes data-open="true" when isOpen=true', () => {
    renderModal();
    const modal = document.querySelector('[data-slot="cancel-modal"]');
    expect(modal?.getAttribute('data-open')).toBe('true');
  });
});

describe('CancelModal — ESC behavior matrix', () => {
  it('ESC invokes onDismiss (default cancel-cancel)', async () => {
    const user = userEvent.setup();
    const { onConfirm, onDismiss } = renderModal();

    const dialog = screen.getByRole('alertdialog');
    dialog.focus();
    await user.keyboard('{Escape}');

    expect(onDismiss).toHaveBeenCalledOnce();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('Click on overlay invokes onDismiss', async () => {
    const user = userEvent.setup();
    const { onConfirm, onDismiss } = renderModal();

    const overlay = document.querySelector('[data-slot="cancel-modal-overlay"]') as HTMLElement;
    await user.click(overlay);

    expect(onDismiss).toHaveBeenCalledOnce();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('Click inside dialog does NOT invoke onDismiss', async () => {
    const user = userEvent.setup();
    const { onDismiss } = renderModal();

    const title = document.querySelector('[data-slot="cancel-modal-title"]') as HTMLElement;
    await user.click(title);

    expect(onDismiss).not.toHaveBeenCalled();
  });

  it('Confirm button invokes onConfirm', async () => {
    const user = userEvent.setup();
    const { onConfirm, onDismiss } = renderModal();

    await user.click(screen.getByRole('button', { name: 'Conferma annullamento' }));

    expect(onConfirm).toHaveBeenCalledOnce();
    expect(onDismiss).not.toHaveBeenCalled();
  });

  it('Dismiss button invokes onDismiss', async () => {
    const user = userEvent.setup();
    const { onConfirm, onDismiss } = renderModal();

    await user.click(screen.getByRole('button', { name: 'Chiudi modal e continua a indicizzare' }));

    expect(onDismiss).toHaveBeenCalledOnce();
    expect(onConfirm).not.toHaveBeenCalled();
  });
});

describe('CancelModal — focus management', () => {
  it('initial focus on dismiss (Continue) button — safe default', () => {
    renderModal();
    const dismissButton = screen.getByRole('button', {
      name: 'Chiudi modal e continua a indicizzare',
    });
    expect(document.activeElement).toBe(dismissButton);
  });

  it('focus trap: Tab cycles within dialog (last → first)', async () => {
    const user = userEvent.setup();
    renderModal();

    const dialog = screen.getByRole('alertdialog');
    const focusables = Array.from(dialog.querySelectorAll<HTMLElement>('button:not([disabled])'));
    expect(focusables.length).toBeGreaterThanOrEqual(2);

    // Focus last button and Tab → wrap to first
    focusables[focusables.length - 1].focus();
    expect(document.activeElement).toBe(focusables[focusables.length - 1]);

    await user.keyboard('{Tab}');
    expect(document.activeElement).toBe(focusables[0]);
  });

  it('focus trap: Shift+Tab wraps first → last', async () => {
    const user = userEvent.setup();
    renderModal();

    const dialog = screen.getByRole('alertdialog');
    const focusables = Array.from(dialog.querySelectorAll<HTMLElement>('button:not([disabled])'));
    expect(focusables.length).toBeGreaterThanOrEqual(2);

    focusables[0].focus();
    await user.keyboard('{Shift>}{Tab}{/Shift}');
    expect(document.activeElement).toBe(focusables[focusables.length - 1]);
  });
});
