/**
 * Wave B.3 (Issue #574) — BulkSelectionBar v2 component tests.
 *
 * Spec §3.2 + AC-6:
 *   - Mounted iff parent decides (selectionMode === 'select'). Component itself
 *     is unconditionally rendered when JSX-mounted; mount/unmount lifecycle
 *     ownership lives in `LibraryHub` orchestrator (Commit 3).
 *   - selectedCount=0 still mounts (no count-conditional unmount, avoids
 *     layout flash on Annulla click).
 *   - Annulla button → onExitSelectMode().
 *   - Esc keyboard at root (when dialog NOT open) → onExitSelectMode().
 *   - Archivia button → Radix `<AlertDialog>` opens (role="alertdialog").
 *   - Dialog title via `labels.confirmTitle` (pre-interpolated by parent with
 *     count). Confirm button → `await onArchive()` resolves + dialog closes.
 *     Cancel button → dialog closes no-op.
 *   - ARIA: `role="region"` + `aria-label` + `aria-live="polite"` +
 *     `aria-atomic="true"` su root.
 *   - data-slot="library-bulk-selection-bar" + scoped sub-slots.
 *   - Slide-in animation gated by `motion-safe:transition` Tailwind class
 *     (collapse a 0.01ms sotto `prefers-reduced-motion: reduce`).
 *
 * Pure component: labels resolved via prop (mirror Wave B.1 GamesEmptyState +
 * B.2 EmptyAgents). Parent (LibraryHub) owns `useTranslation()`.
 */

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { BulkSelectionBar, type BulkSelectionBarLabels } from '../BulkSelectionBar';

const baseLabels: BulkSelectionBarLabels = {
  regionLabel: '3 selezionati',
  counter: '3 selezionati',
  cancel: 'Annulla',
  archive: 'Archivia',
  confirmTitle: 'Confermi rimozione di 3 giochi dalla libreria?',
  confirmCta: 'Conferma',
  cancelCta: 'Annulla',
};

describe('BulkSelectionBar (Wave B.3)', () => {
  describe('rendering + ARIA', () => {
    it('renders even when selectedCount=0 (no count-conditional unmount)', () => {
      const labels: BulkSelectionBarLabels = {
        ...baseLabels,
        regionLabel: '0 selezionati',
        counter: '0 selezionati',
      };
      const { container } = render(
        <BulkSelectionBar
          selectedCount={0}
          labels={labels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      expect(container.querySelector('[data-slot="library-bulk-selection-bar"]')).not.toBeNull();
    });

    it('exposes role="region" + aria-live + aria-atomic + aria-label on root', () => {
      const { container } = render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      const region = container.querySelector('[data-slot="library-bulk-selection-bar"]');
      expect(region).toHaveAttribute('role', 'region');
      expect(region).toHaveAttribute('aria-live', 'polite');
      expect(region).toHaveAttribute('aria-atomic', 'true');
      expect(region).toHaveAttribute('aria-label', '3 selezionati');
    });

    it('renders the counter label', () => {
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      expect(screen.getByText('3 selezionati')).toBeInTheDocument();
    });

    it('renders cancel + archive buttons with resolved labels', () => {
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      expect(screen.getByRole('button', { name: 'Annulla' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Archivia' })).toBeInTheDocument();
    });

    it('applies motion-safe transition class for prefers-reduced-motion gating', () => {
      const { container } = render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      const region = container.querySelector('[data-slot="library-bulk-selection-bar"]');
      // Motion-safe Tailwind class collapses to 0.01ms under prefers-reduced-motion;
      // E2E asserts computed style. Here only class presence.
      expect(region?.className).toMatch(/motion-safe:transition/);
    });
  });

  describe('exit interactions', () => {
    it('Annulla click → onExitSelectMode()', () => {
      const onExitSelectMode = vi.fn();
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={onExitSelectMode}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      fireEvent.click(screen.getByRole('button', { name: 'Annulla' }));
      expect(onExitSelectMode).toHaveBeenCalledTimes(1);
    });

    it('Esc key on bar (dialog NOT open) → onExitSelectMode()', () => {
      const onExitSelectMode = vi.fn();
      const { container } = render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={onExitSelectMode}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      const region = container.querySelector('[data-slot="library-bulk-selection-bar"]')!;
      fireEvent.keyDown(region, { key: 'Escape' });
      expect(onExitSelectMode).toHaveBeenCalledTimes(1);
    });

    it('non-Esc keys are no-op (no false-positive exits)', () => {
      const onExitSelectMode = vi.fn();
      const { container } = render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={onExitSelectMode}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      const region = container.querySelector('[data-slot="library-bulk-selection-bar"]')!;
      fireEvent.keyDown(region, { key: 'Enter' });
      fireEvent.keyDown(region, { key: ' ' });
      fireEvent.keyDown(region, { key: 'a' });
      expect(onExitSelectMode).not.toHaveBeenCalled();
    });
  });

  describe('Archivia confirm flow (Radix AlertDialog)', () => {
    it('Archivia click → AlertDialog opens with confirmTitle', async () => {
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );

      // Dialog NOT open initially
      expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();

      fireEvent.click(screen.getByRole('button', { name: 'Archivia' }));

      // Dialog opens (Radix uses Portal → check globally via screen)
      const dialog = await screen.findByRole('alertdialog');
      expect(dialog).toBeInTheDocument();
      // Title rendered as heading (Radix AlertDialogTitle = h2). The same copy
      // also appears in `sr-only` AlertDialogDescription fallback (a11y), so
      // pin via heading role to avoid duplicate-match.
      expect(
        screen.getByRole('heading', {
          name: 'Confermi rimozione di 3 giochi dalla libreria?',
        })
      ).toBeInTheDocument();
    });

    it('Confirm action in dialog → await onArchive() resolves', async () => {
      const onArchive = vi.fn().mockResolvedValue(undefined);
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={onArchive}
        />
      );

      fireEvent.click(screen.getByRole('button', { name: 'Archivia' }));
      await screen.findByRole('alertdialog');

      // Confirm button (uses confirmCta label "Conferma")
      const confirmButton = screen.getByRole('button', { name: 'Conferma' });
      fireEvent.click(confirmButton);

      await waitFor(() => {
        expect(onArchive).toHaveBeenCalledTimes(1);
      });
    });

    it('Cancel action in dialog → onArchive NOT called', async () => {
      const onArchive = vi.fn().mockResolvedValue(undefined);
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={onArchive}
        />
      );

      fireEvent.click(screen.getByRole('button', { name: 'Archivia' }));
      await screen.findByRole('alertdialog');

      // Cancel inside dialog uses cancelCta label. Two buttons share "Annulla":
      // (1) the bar exit button (already in DOM), (2) dialog cancel.
      // Dialog cancel is the second one (rendered later). Use within(dialog).
      const dialog = screen.getByRole('alertdialog');
      const cancelInDialog = Array.from(dialog.querySelectorAll('button')).find(
        b => b.textContent === 'Annulla'
      );
      expect(cancelInDialog).toBeDefined();
      fireEvent.click(cancelInDialog!);

      // Dialog closes
      await waitFor(() => {
        expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
      });
      expect(onArchive).not.toHaveBeenCalled();
    });
  });

  describe('disabled state', () => {
    it('disables both cancel + archive buttons when disabled=true', () => {
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
          disabled
        />
      );
      expect(screen.getByRole('button', { name: 'Annulla' })).toBeDisabled();
      expect(screen.getByRole('button', { name: 'Archivia' })).toBeDisabled();
    });

    it('disabled cancel does NOT trigger onExitSelectMode on click', () => {
      const onExitSelectMode = vi.fn();
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={onExitSelectMode}
          onArchive={vi.fn().mockResolvedValue(undefined)}
          disabled
        />
      );
      fireEvent.click(screen.getByRole('button', { name: 'Annulla' }));
      expect(onExitSelectMode).not.toHaveBeenCalled();
    });
  });
});
