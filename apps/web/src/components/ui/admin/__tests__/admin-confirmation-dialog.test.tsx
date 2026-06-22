/**
 * AdminConfirmationDialog Tests
 *
 * Tests covering:
 * 1. Level 1 dialog renders without typed-confirmation input
 * 2. Level 2 dialog requires typing "CONFIRM" by default (backward compat)
 * 3. Level 2 dialog with custom confirmPhrase enables confirm only when that phrase is typed
 * 4. On-screen label reflects the required phrase (custom or default)
 * 5. Dialog resets typed text when reopened
 */

import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, it, expect, vi } from 'vitest';

import { AdminConfirmationDialog, AdminConfirmationLevel } from '../admin-confirmation-dialog';

// ============================================================================
// Helpers
// ============================================================================

const noop = () => {};

function renderLevel2(props: Partial<React.ComponentProps<typeof AdminConfirmationDialog>> = {}) {
  return render(
    <AdminConfirmationDialog
      isOpen
      level={AdminConfirmationLevel.Level2}
      title="Delete"
      message="Sure?"
      onClose={noop}
      onConfirm={noop}
      {...props}
    />
  );
}

// ============================================================================
// Tests
// ============================================================================

describe('AdminConfirmationDialog', () => {
  describe('Level 1', () => {
    it('renders confirm button enabled without any typing', () => {
      render(
        <AdminConfirmationDialog
          isOpen
          level={AdminConfirmationLevel.Level1}
          title="Warning"
          message="Are you sure?"
          onClose={noop}
          onConfirm={noop}
        />
      );
      const btn = screen.getByRole('button', { name: /confirm|conferma/i });
      expect(btn).toBeEnabled();
    });

    it('does not render a textbox for Level 1', () => {
      render(
        <AdminConfirmationDialog
          isOpen
          level={AdminConfirmationLevel.Level1}
          title="Warning"
          message="Are you sure?"
          onClose={noop}
          onConfirm={noop}
        />
      );
      expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    });
  });

  describe('Level 2 — default phrase (backward compat)', () => {
    it('confirm button is disabled initially', () => {
      renderLevel2();
      const btn = screen.getByRole('button', { name: /confirm|conferma/i });
      expect(btn).toBeDisabled();
    });

    it('defaults to requiring "CONFIRM" when confirmPhrase is omitted', () => {
      renderLevel2();
      fireEvent.change(screen.getByRole('textbox'), { target: { value: 'CONFIRM' } });
      const btn = screen.getByRole('button', { name: /confirm|conferma/i });
      expect(btn).toBeEnabled();
    });

    it('remains disabled when a different word is typed', () => {
      renderLevel2();
      fireEvent.change(screen.getByRole('textbox'), { target: { value: 'confirm' } });
      const btn = screen.getByRole('button', { name: /confirm|conferma/i });
      expect(btn).toBeDisabled();
    });

    it('shows "CONFIRM" as the required phrase in the label', () => {
      renderLevel2();
      // The label should mention CONFIRM (default)
      expect(screen.getByText(/CONFIRM/)).toBeInTheDocument();
    });
  });

  describe('Level 2 — custom confirmPhrase', () => {
    it('enables confirm only when the custom confirmPhrase is typed', () => {
      renderLevel2({ confirmPhrase: 'Wingspan.pdf' });
      const confirmBtn = screen.getByRole('button', { name: /confirm|conferma|delete|elimina/i });
      expect(confirmBtn).toBeDisabled();
      fireEvent.change(screen.getByRole('textbox'), { target: { value: 'Wingspan.pdf' } });
      expect(confirmBtn).toBeEnabled();
    });

    it('remains disabled when the default "CONFIRM" word is typed instead of the custom phrase', () => {
      renderLevel2({ confirmPhrase: 'Wingspan.pdf' });
      fireEvent.change(screen.getByRole('textbox'), { target: { value: 'CONFIRM' } });
      const btn = screen.getByRole('button', { name: /confirm|conferma|delete|elimina/i });
      expect(btn).toBeDisabled();
    });

    it('shows the custom phrase in the prompt label', () => {
      renderLevel2({ confirmPhrase: 'Wingspan.pdf' });
      expect(screen.getByText('Wingspan.pdf')).toBeInTheDocument();
    });

    it('shows error hint with the custom phrase when wrong text is entered', () => {
      renderLevel2({ confirmPhrase: 'Wingspan.pdf' });
      fireEvent.change(screen.getByRole('textbox'), { target: { value: 'wrong' } });
      // The error hint paragraph (distinct from the label span) must contain the custom phrase
      expect(
        screen.getByText(/La parola deve corrispondere esattamente: Wingspan\.pdf/)
      ).toBeInTheDocument();
    });
  });

  describe('backward compatibility — omitting confirmPhrase', () => {
    it('defaults to requiring "CONFIRM" when confirmPhrase is omitted (backward compat)', () => {
      render(
        <AdminConfirmationDialog
          isOpen
          level={AdminConfirmationLevel.Level2}
          title="t"
          message="m"
          onClose={noop}
          onConfirm={noop}
        />
      );
      fireEvent.change(screen.getByRole('textbox'), { target: { value: 'CONFIRM' } });
      expect(
        screen.getByRole('button', { name: /confirm|conferma|delete|elimina/i })
      ).toBeEnabled();
    });
  });

  describe('onClose / onConfirm callbacks', () => {
    it('calls onClose when cancel button is clicked', () => {
      const onClose = vi.fn();
      renderLevel2({ onClose });
      fireEvent.click(screen.getByRole('button', { name: /annulla|cancel/i }));
      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('calls onConfirm when confirm button is clicked after typing the required phrase', async () => {
      const onConfirm = vi.fn().mockResolvedValue(undefined);
      const onClose = vi.fn();
      renderLevel2({ onConfirm, onClose });
      fireEvent.change(screen.getByRole('textbox'), { target: { value: 'CONFIRM' } });
      fireEvent.click(screen.getByRole('button', { name: /confirm|conferma|delete|elimina/i }));
      expect(onConfirm).toHaveBeenCalledTimes(1);
    });
  });

  // PR #2428 — Regression guard for "setState after unmount" race.
  //
  // Flow that used to surface as `ReferenceError: window is not defined`
  // (originating in React 19's `resolveUpdatePriority` when the test env had
  // already torn down `window`):
  //   1. user clicks Confirm
  //   2. handleConfirm flips isSubmitting=true, then `await onConfirm()`
  //   3. host (e.g. RestartAllPanel) responds to onConfirm by flipping isOpen
  //      to false → Radix unmounts the dialog before onConfirm resolves
  //   4. dialog finishes the await, calls onClose() (no-op), and the `finally`
  //      block fires `setIsSubmitting(false)` on the torn-down component
  //
  // The fix is a mount sentinel that short-circuits the late setState. This
  // suite re-creates the unmount-mid-await scenario and pins the absence of
  // both console errors AND unhandled rejections.
  describe('post-unmount race (PR #2428)', () => {
    let consoleErrorSpy: ReturnType<typeof vi.spyOn>;
    let unhandledRejections: unknown[];
    let unhandledListener: (event: PromiseRejectionEvent) => void;

    beforeEach(() => {
      consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
      unhandledRejections = [];
      unhandledListener = event => {
        unhandledRejections.push(event.reason);
      };
      window.addEventListener('unhandledrejection', unhandledListener);
    });

    afterEach(() => {
      window.removeEventListener('unhandledrejection', unhandledListener);
      consoleErrorSpy.mockRestore();
    });

    it('does not setState (or surface a window-undefined error) when the dialog unmounts mid-onConfirm', async () => {
      // Slow onConfirm so we can unmount the dialog while it's still in flight.
      let resolveConfirm: () => void = () => undefined;
      const onConfirm = vi.fn(
        () =>
          new Promise<void>(resolve => {
            resolveConfirm = resolve;
          })
      );
      const onClose = vi.fn();

      const { rerender } = render(
        <AdminConfirmationDialog
          isOpen
          level={AdminConfirmationLevel.Level2}
          title="Restart"
          message="Slow op?"
          confirmPhrase="GO"
          onClose={onClose}
          onConfirm={onConfirm}
        />
      );

      fireEvent.change(screen.getByRole('textbox'), { target: { value: 'GO' } });
      fireEvent.click(
        screen.getByRole('button', { name: /confirm|conferma|delete|elimina|restart/i })
      );

      // Sanity: handleConfirm started awaiting onConfirm.
      expect(onConfirm).toHaveBeenCalledTimes(1);

      // Unmount the dialog while onConfirm is still pending — this mimics the
      // RestartAllPanel pattern of synchronously flipping isOpen=false before
      // the host's restart loop resolves.
      rerender(<div data-testid="placeholder" />);
      await waitFor(() => {
        expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      });

      // Now resolve onConfirm — the `finally` block must NOT crash.
      resolveConfirm();
      await Promise.resolve();
      await Promise.resolve();

      expect(consoleErrorSpy).not.toHaveBeenCalled();
      expect(unhandledRejections).toHaveLength(0);
    });
  });
});
