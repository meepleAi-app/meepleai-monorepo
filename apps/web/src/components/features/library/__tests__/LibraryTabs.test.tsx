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
  { key: 'all', label: 'Tutti', count: 12 },
  { key: 'kb', label: 'Con KB', count: 5 },
  { key: 'loaned', label: 'In prestito', count: 2 },
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
});
