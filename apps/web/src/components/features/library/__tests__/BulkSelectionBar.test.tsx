/**
 * Wave B.3 (Issue #574) — BulkSelectionBar v2 component tests.
 *
 * SP4 mockup conformance (Issue #1585-followup, plan
 * docs/superpowers/plans/2026-06-03-library-sp4-mockup-conformance.md Task 3.2):
 *   - Dark floating bar (`bg-foreground` + `text-background`) at `bottom-4` center.
 *   - Count chip (entity-game) separate from counter label (no number in counter).
 *   - 3 action buttons (Archivia / Tag / Esporta) — Archive preserves AlertDialog
 *     confirm flow; Tag + Export are optional callbacks (stub no-op default).
 *   - Close affordance: ✕ icon button with `closeAriaLabel` (no more "Annulla"
 *     pill — that role moved to ✕).
 *   - Compact mode: counterCompact label + icon-only action buttons.
 *
 * Wave B.3 invariants preserved:
 *   - Mounted iff parent decides (selectionMode === 'select'). Component itself
 *     is unconditionally rendered when JSX-mounted; mount/unmount lifecycle
 *     ownership lives in `LibraryHub` orchestrator (Commit 3).
 *   - selectedCount=0 still mounts (no count-conditional unmount, avoids
 *     layout flash on Annulla click).
 *   - Close (✕) button → onExitSelectMode().
 *   - Esc keyboard at root (when dialog NOT open) → onExitSelectMode().
 *   - Archivia button → Radix `<AlertDialog>` opens (role="alertdialog").
 *   - Dialog title via `labels.confirmTitle` (pre-interpolated by parent with
 *     count). Confirm button → `await onArchive()` resolves + dialog closes.
 *     Cancel button → dialog closes no-op.
 *   - ARIA: `role="region"` + `aria-label` + `aria-live="polite"` +
 *     `aria-atomic="true"` su root.
 *   - data-slot="library-bulk-selection-bar" + scoped sub-slots.
 *   - Slide-in animation gated by `motion-safe:` Tailwind class
 *     (collapse a 0.01ms sotto `prefers-reduced-motion: reduce`).
 *
 * Pure component: labels resolved via prop (mirror Wave B.1 GamesEmptyState +
 * B.2 EmptyAgents). Parent (LibraryHub) owns `useTranslation()`.
 */

import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { BulkSelectionBar, type BulkSelectionBarLabels } from '../BulkSelectionBar';

const baseLabels: BulkSelectionBarLabels = {
  regionLabel: '3 selezionati',
  counter: 'selezionati',
  counterCompact: 'sel.',
  archive: 'Archivia',
  tag: 'Tag',
  exportLabel: 'Esporta',
  closeAriaLabel: 'Annulla selezione',
  confirmTitle: 'Confermi rimozione di 3 giochi dalla libreria?',
  confirmCta: 'Conferma',
  cancelCta: 'Annulla',
};

describe('BulkSelectionBar (Wave B.3 + SP4 mockup conformance)', () => {
  describe('rendering + ARIA', () => {
    it('renders even when selectedCount=0 (no count-conditional unmount)', () => {
      const labels: BulkSelectionBarLabels = {
        ...baseLabels,
        regionLabel: '0 selezionati',
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

    it('renders counter label without numeric count (count is in chip)', () => {
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      const counter = screen.getByText('selezionati');
      expect(counter).toBeInTheDocument();
      // Count "3" is rendered in chip, not in the counter label
      expect(counter.textContent).not.toMatch(/\d/);
    });

    it('renders cancel (✕) + archive buttons with resolved labels', () => {
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      expect(screen.getByRole('button', { name: 'Annulla selezione' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Archivia' })).toBeInTheDocument();
    });

    it('applies motion-safe slide-in animation class for prefers-reduced-motion gating', () => {
      const { container } = render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      const region = container.querySelector('[data-slot="library-bulk-selection-bar"]');
      // Motion-safe Tailwind classes collapse to no-op under prefers-reduced-motion;
      // E2E asserts computed style. Here only class presence.
      expect(region?.className).toMatch(/motion-safe:/);
    });
  });

  describe('SP4 mockup conformance (sp4-library-desktop.jsx:895-944)', () => {
    it('renders the dark floating bar (bg-foreground + text-background)', () => {
      const { container } = render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      const bar = container.querySelector('[data-slot="library-bulk-selection-bar"]');
      expect(bar?.className).toMatch(/bg-foreground/);
      expect(bar?.className).toMatch(/text-background/);
    });

    it('renders the count chip with entity-game background and tabular-nums', () => {
      const { container } = render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      const chip = container.querySelector('[data-slot="library-bulk-selection-count-chip"]');
      expect(chip).not.toBeNull();
      expect(chip).toHaveTextContent('3');
      expect(chip?.className).toMatch(/bg-entity-game/);
      expect(chip?.className).toMatch(/tabular-nums/);
    });

    it('renders Archive + Tag + Export action buttons (3 actions)', () => {
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      expect(screen.getByRole('button', { name: /Archivia/ })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Tag/ })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Esporta/ })).toBeInTheDocument();
    });

    it('renders close (✕) button with closeAriaLabel', () => {
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      expect(screen.getByRole('button', { name: 'Annulla selezione' })).toBeInTheDocument();
    });

    it('positions the bar at fixed bottom-4 center via translate-x', () => {
      const { container } = render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      const bar = container.querySelector('[data-slot="library-bulk-selection-bar"]');
      expect(bar?.className).toMatch(/fixed/);
      expect(bar?.className).toMatch(/bottom-4/);
      expect(bar?.className).toMatch(/-translate-x-1\/2/);
    });
  });

  describe('exit interactions', () => {
    it('Close (✕) click → onExitSelectMode()', () => {
      const onExitSelectMode = vi.fn();
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={onExitSelectMode}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      fireEvent.click(screen.getByRole('button', { name: 'Annulla selezione' }));
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

  describe('Tag + Export action callbacks', () => {
    it('Tag click invokes onTag when provided', () => {
      const onTag = vi.fn();
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
          onTag={onTag}
        />
      );
      fireEvent.click(screen.getByRole('button', { name: /Tag/ }));
      expect(onTag).toHaveBeenCalledTimes(1);
    });

    it('Export click invokes onExport when provided', () => {
      const onExport = vi.fn();
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
          onExport={onExport}
        />
      );
      fireEvent.click(screen.getByRole('button', { name: /Esporta/ }));
      expect(onExport).toHaveBeenCalledTimes(1);
    });

    it('Tag/Export buttons are no-op (do not crash) when callbacks omitted', () => {
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
        />
      );
      expect(() => fireEvent.click(screen.getByRole('button', { name: /Tag/ }))).not.toThrow();
      expect(() => fireEvent.click(screen.getByRole('button', { name: /Esporta/ }))).not.toThrow();
    });
  });

  describe('compact mode', () => {
    it('uses counterCompact label instead of counter when compact=true', () => {
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
          compact
        />
      );
      expect(screen.getByText('sel.')).toBeInTheDocument();
      expect(screen.queryByText('selezionati')).not.toBeInTheDocument();
    });

    it('hides action labels in compact mode (icons remain via aria-label)', () => {
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
          compact
        />
      );
      // Action labels are no longer in visible text (only icon + aria-label)
      // The buttons must still be reachable by their accessible name (icon + aria-label fallback).
      // Archive button still resolves by name='Archivia' (aria-label fallback).
      expect(screen.getByRole('button', { name: 'Archivia' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Tag' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Esporta' })).toBeInTheDocument();
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

      fireEvent.click(screen.getByRole('button', { name: /Archivia/ }));

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

      fireEvent.click(screen.getByRole('button', { name: /Archivia/ }));
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

      fireEvent.click(screen.getByRole('button', { name: /Archivia/ }));
      await screen.findByRole('alertdialog');

      // Dialog cancel button (cancelCta label "Annulla" inside dialog). Scope
      // the search to the dialog to avoid matching the bar's close ✕ aria-label
      // "Annulla selezione".
      const dialog = screen.getByRole('alertdialog');
      const cancelInDialog = within(dialog).getByRole('button', { name: 'Annulla' });
      fireEvent.click(cancelInDialog);

      // Dialog closes
      await waitFor(() => {
        expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
      });
      expect(onArchive).not.toHaveBeenCalled();
    });
  });

  describe('disabled state', () => {
    it('disables close + archive + tag + export buttons when disabled=true', () => {
      render(
        <BulkSelectionBar
          selectedCount={3}
          labels={baseLabels}
          onExitSelectMode={vi.fn()}
          onArchive={vi.fn().mockResolvedValue(undefined)}
          disabled
        />
      );
      expect(screen.getByRole('button', { name: 'Annulla selezione' })).toBeDisabled();
      expect(screen.getByRole('button', { name: 'Archivia' })).toBeDisabled();
      expect(screen.getByRole('button', { name: 'Tag' })).toBeDisabled();
      expect(screen.getByRole('button', { name: 'Esporta' })).toBeDisabled();
    });

    it('disabled close ✕ does NOT trigger onExitSelectMode on click', () => {
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
      fireEvent.click(screen.getByRole('button', { name: 'Annulla selezione' }));
      expect(onExitSelectMode).not.toHaveBeenCalled();
    });
  });
});
