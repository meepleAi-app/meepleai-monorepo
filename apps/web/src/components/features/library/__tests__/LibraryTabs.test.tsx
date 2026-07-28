/**
 * Wave B.3 (Issue #574) — LibraryTabs v2 component tests.
 *
 * Spec §3.2 + AC-2 + AC-4:
 *   - 3 entity tabs scope ridotto: `all` / `kb` / `loaned`
 *     (tab `game` droppato YAGNI; `archived` rinominato `loaned` —
 *      §3.3 mapping `currentState='InPrestito'`)
 *   - WAI-ARIA APG horizontal tablist via `useTablistKeyboardNav<LibraryEntityKey>`
 *     (PR #623). Arrow Left/Right wrap, Home/End jump.
 *   - Roving tabindex automatic activation (focus = onChange same tick).
 *   - Animated underline (CSS transition gated da `prefers-reduced-motion`,
 *     verified in E2E reduced-motion contract — here only class presence
 *     check, see AC-8).
 *
 * Pure component (mirror Wave B.2): tab labels passed via `tabs` prop with
 * resolved i18n strings — no `useTranslation` import.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';

import { LibraryTabs, type LibraryEntityKey, type LibraryTabConfig } from '../LibraryTabs';

const baseTabs: readonly LibraryTabConfig[] = [
  { key: 'all', label: 'Tutti', count: 12, icon: '⌗' },
  { key: 'kb', label: 'Con KB', count: 5, icon: '📚', entity: 'kb' },
  { key: 'loaned', label: 'In prestito', count: 2, icon: '📦' },
];

/**
 * Stateful wrapper mirroring real page-client usage:
 * onChange flips `active`, re-renders, roving tabindex updates → next keydown
 * lands on the new active tab. Without this, single-render tests can't observe
 * focus migration after Arrow keys.
 */
function ControlledLibraryTabs({
  initial = 'all',
  onChangeSpy,
}: {
  initial?: LibraryEntityKey;
  onChangeSpy?: (key: LibraryEntityKey) => void;
}) {
  const [active, setActive] = useState<LibraryEntityKey>(initial);
  return (
    <LibraryTabs
      tabs={baseTabs}
      active={active}
      onChange={next => {
        setActive(next);
        onChangeSpy?.(next);
      }}
    />
  );
}

describe('LibraryTabs (Wave B.3)', () => {
  describe('rendering — scope ridotto 3 tabs', () => {
    it('renders exactly 3 tabs (all/kb/loaned) — drops game/archived/agent/session/chat', () => {
      render(<ControlledLibraryTabs />);
      const tabs = screen.getAllByRole('tab');
      expect(tabs).toHaveLength(3);
    });

    it('renders each tab label resolved via prop', () => {
      render(<ControlledLibraryTabs />);
      expect(screen.getByRole('tab', { name: /tutti/i })).toBeInTheDocument();
      expect(screen.getByRole('tab', { name: /con kb/i })).toBeInTheDocument();
      expect(screen.getByRole('tab', { name: /in prestito/i })).toBeInTheDocument();
    });

    it('renders tab counts as suffix (Phase 2 readiness for live counts)', () => {
      const { container } = render(<ControlledLibraryTabs />);
      const counts = container.querySelectorAll('[data-slot="library-tab-count"]');
      expect(counts).toHaveLength(3);
      expect(counts[0].textContent).toMatch(/12/);
      expect(counts[1].textContent).toMatch(/5/);
      expect(counts[2].textContent).toMatch(/2/);
    });

    it('exposes data-slot="library-tabs" + role="tablist" on container', () => {
      const { container } = render(<ControlledLibraryTabs />);
      const tablist = container.querySelector('[data-slot="library-tabs"]');
      expect(tablist).not.toBeNull();
      expect(tablist).toHaveAttribute('role', 'tablist');
    });
  });

  describe('active state + aria-selected + roving tabindex', () => {
    it('marks the active tab with aria-selected="true" and others with "false"', () => {
      render(<ControlledLibraryTabs initial="kb" />);
      expect(screen.getByRole('tab', { name: /tutti/i })).toHaveAttribute('aria-selected', 'false');
      expect(screen.getByRole('tab', { name: /con kb/i })).toHaveAttribute('aria-selected', 'true');
      expect(screen.getByRole('tab', { name: /in prestito/i })).toHaveAttribute(
        'aria-selected',
        'false'
      );
    });

    it('roving tabindex: active tab tabIndex=0, others tabIndex=-1', () => {
      render(<ControlledLibraryTabs initial="all" />);
      expect(screen.getByRole('tab', { name: /tutti/i })).toHaveAttribute('tabindex', '0');
      expect(screen.getByRole('tab', { name: /con kb/i })).toHaveAttribute('tabindex', '-1');
      expect(screen.getByRole('tab', { name: /in prestito/i })).toHaveAttribute('tabindex', '-1');
    });

    // #1094 Real-C-misc regression guard, evolved in PR1 Task 1.3:
    // The pre-fix `text-primary` snapshot would fail AA in dark theme.
    // SP4 mockup (jsx:175-176) replaces the entity-text token with
    // `bg-entity-{ent}` + `text-white` on the active count pill — AA-safe
    // in both themes because the entity orange/amber/etc. CSS vars are
    // tuned to pass against white (≥4.5:1, see `audits/2026-05-12-token-violations.md`
    // and design-tokens canonical inventory).
    it('active count badge uses bg-entity + text-white pair (AA contrast, mockup jsx:175-176)', () => {
      const { container } = render(<ControlledLibraryTabs initial="all" />);
      const counts = container.querySelectorAll('[data-slot="library-tab-count"]');
      // First tab (active "all" → 'game' accent) badge: bg-entity-game + text-white
      expect(counts[0].className).toMatch(/bg-entity-game\b/);
      expect(counts[0].className).toContain('text-white');
      // Inactive tabs keep the muted token pair
      expect(counts[1].className).toContain('bg-muted');
      expect(counts[1].className).toContain('text-muted-foreground');
    });
  });

  describe('click activation', () => {
    it('calls onChange with the clicked tab key', () => {
      const onChangeSpy = vi.fn();
      render(<ControlledLibraryTabs onChangeSpy={onChangeSpy} />);
      fireEvent.click(screen.getByRole('tab', { name: /con kb/i }));
      expect(onChangeSpy).toHaveBeenCalledWith('kb');
    });
  });

  describe('keyboard navigation (WAI-ARIA APG via useTablistKeyboardNav)', () => {
    it('ArrowRight from "all" → "kb"', () => {
      const onChangeSpy = vi.fn();
      render(<ControlledLibraryTabs onChangeSpy={onChangeSpy} />);
      fireEvent.keyDown(screen.getByRole('tab', { name: /tutti/i }), { key: 'ArrowRight' });
      expect(onChangeSpy).toHaveBeenLastCalledWith('kb');
    });

    it('ArrowRight wraps last → first ("loaned" → "all")', () => {
      const onChangeSpy = vi.fn();
      render(<ControlledLibraryTabs initial="loaned" onChangeSpy={onChangeSpy} />);
      fireEvent.keyDown(screen.getByRole('tab', { name: /in prestito/i }), { key: 'ArrowRight' });
      expect(onChangeSpy).toHaveBeenLastCalledWith('all');
    });

    it('ArrowLeft wraps first → last ("all" → "loaned")', () => {
      const onChangeSpy = vi.fn();
      render(<ControlledLibraryTabs initial="all" onChangeSpy={onChangeSpy} />);
      fireEvent.keyDown(screen.getByRole('tab', { name: /tutti/i }), { key: 'ArrowLeft' });
      expect(onChangeSpy).toHaveBeenLastCalledWith('loaned');
    });

    it('Home jumps to first tab ("all")', () => {
      const onChangeSpy = vi.fn();
      render(<ControlledLibraryTabs initial="loaned" onChangeSpy={onChangeSpy} />);
      fireEvent.keyDown(screen.getByRole('tab', { name: /in prestito/i }), { key: 'Home' });
      expect(onChangeSpy).toHaveBeenLastCalledWith('all');
    });

    it('End jumps to last tab ("loaned")', () => {
      const onChangeSpy = vi.fn();
      render(<ControlledLibraryTabs initial="all" onChangeSpy={onChangeSpy} />);
      fireEvent.keyDown(screen.getByRole('tab', { name: /tutti/i }), { key: 'End' });
      expect(onChangeSpy).toHaveBeenLastCalledWith('loaned');
    });

    it('off-axis keys (ArrowUp/ArrowDown/character) are no-ops in horizontal mode', () => {
      const onChangeSpy = vi.fn();
      render(<ControlledLibraryTabs onChangeSpy={onChangeSpy} />);
      const allTab = screen.getByRole('tab', { name: /tutti/i });
      fireEvent.keyDown(allTab, { key: 'ArrowUp' });
      fireEvent.keyDown(allTab, { key: 'ArrowDown' });
      fireEvent.keyDown(allTab, { key: 'a' });
      expect(onChangeSpy).not.toHaveBeenCalled();
    });
  });

  describe('animated underline + reduced-motion readiness', () => {
    it('renders an underline element with motion-safe transition class', () => {
      const { container } = render(<ControlledLibraryTabs />);
      const underline = container.querySelector('[data-slot="library-tabs-underline"]');
      expect(underline).not.toBeNull();
      // Motion-safe Tailwind class collapses to 0.01ms under prefers-reduced-motion;
      // E2E (a11y/library.spec.ts) assert the computed style. Here only presence.
      expect(underline?.className).toMatch(/motion-safe:transition|transition/);
    });
  });

  // ──────────────────────────────────────────────────────────────────────────
  // SP4 mockup conformance (PR1 Task 1.3) — Library SP4 Mockup Conformance.
  // Mockup ref: admin-mockups/design_files/sp4-library-desktop.jsx:134-191
  // ──────────────────────────────────────────────────────────────────────────
  describe('SP4 conformance — entity icons + entity-colored active state', () => {
    it('renders each tab with its entity icon (aria-hidden span)', () => {
      render(<ControlledLibraryTabs />);
      // Icons are wrapped in `aria-hidden="true"` per mockup jsx:172 (decorative,
      // excluded from accessible name). We assert presence via textContent on the
      // tab buttons themselves rather than role-by-name (which strips aria-hidden).
      const tabs = screen.getAllByRole('tab');
      expect(tabs[0].textContent).toMatch(/⌗.*Tutti/);
      expect(tabs[1].textContent).toMatch(/📚.*Con KB/);
      expect(tabs[2].textContent).toMatch(/📦.*In prestito/);
      // And the icon spans themselves carry aria-hidden so screen readers skip them.
      const iconSpans = tabs.map(t => t.querySelector('span[aria-hidden="true"]'));
      expect(iconSpans[0]?.textContent).toBe('⌗');
      expect(iconSpans[1]?.textContent).toBe('📚');
      expect(iconSpans[2]?.textContent).toBe('📦');
    });

    it('renders the animated indicator slot with data-slot="library-tabs-indicator"', () => {
      const { container } = render(<ControlledLibraryTabs />);
      expect(container.querySelector('[data-slot="library-tabs-indicator"]')).toBeInTheDocument();
    });

    it('applies bg-entity-kb/10 + text-entity-kb-text on the active "kb" tab', () => {
      render(<ControlledLibraryTabs initial="kb" />);
      const kbTab = screen.getByRole('tab', { name: /Con KB/, selected: true });
      expect(kbTab.className).toMatch(/bg-entity-kb\/10/);
      expect(kbTab.className).toMatch(/text-entity-kb-text\b/);
    });

    it('falls back to game accent (bg-entity-game/10 + text-entity-game-text) on active "all" tab', () => {
      render(<ControlledLibraryTabs initial="all" />);
      const allTab = screen.getByRole('tab', { name: /Tutti/, selected: true });
      expect(allTab.className).toMatch(/bg-entity-game\/10/);
      expect(allTab.className).toMatch(/text-entity-game-text\b/);
    });
  });
});
