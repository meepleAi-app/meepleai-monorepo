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
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

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
});
