/**
 * Wave B.3 (Issue #574) — RecentActivityRail v2 component tests.
 *
 * Phase 1 contract: `items.length === 0` always (no backend endpoint
 * `GET /api/v1/library/activity` yet). Empty placeholder with i18n copy
 * or skeleton 3 lines when `isLoading`.
 *
 * Phase 2 readiness: when `items.length > 0`, render list with per-kind icon.
 * This branch is exercised here against synthetic data so the component is
 * ready to be wired when the backend endpoint lands (no rewrite needed).
 *
 * Task 6 (#1593): i18n connect via useTranslation + error state (R4).
 * IntlProvider wraps every render so useTranslation (react-intl) resolves
 * real messages. Pattern mirrors CrossEntityFilters.test.tsx.
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { describe, expect, it } from 'vitest';

import { RecentActivityRail, type ActivityItem } from '../RecentActivityRail';

// ── i18n messages needed by RecentActivityRail ────────────────────────────────
const messages: Record<string, string> = {
  'pages.library.activityRail.title': 'Ultime modifiche',
  'pages.library.activityRail.empty': 'Nessuna attività recente.',
  'pages.library.activityRail.error': "Impossibile caricare l'attività.",
  'pages.library.activityRail.collapseAriaLabel': 'Comprimi pannello',
  'pages.library.activityRail.shortcuts.heading': 'Shortcuts',
  'pages.library.activityRail.shortcuts.focusSearch': 'focus search',
  'pages.library.activityRail.shortcuts.advancedFilters': 'filtri avanzati',
  'pages.library.activityRail.shortcuts.allShortcuts': 'tutte le scorciatoie',
};

function renderWithIntl(ui: React.ReactElement) {
  return render(
    <IntlProvider locale="it" messages={messages}>
      {ui}
    </IntlProvider>
  );
}

// Convenience helper matching the renderWithIntl signature used throughout tests.
function renderRail(props: Partial<React.ComponentProps<typeof RecentActivityRail>> = {}) {
  return renderWithIntl(<RecentActivityRail items={[]} {...props} />);
}

const sampleItems: readonly ActivityItem[] = [
  { id: 'a1', kind: 'play', entityTitle: 'Catan', timestamp: '2026-04-30T08:00:00.000Z' },
  { id: 'a2', kind: 'add', entityTitle: 'Wingspan', timestamp: '2026-04-29T08:00:00.000Z' },
  {
    id: 'a3',
    kind: 'kb-indexed',
    entityTitle: 'Brass: Birmingham',
    timestamp: '2026-04-28T08:00:00.000Z',
  },
  { id: 'a4', kind: 'rating-changed', entityTitle: 'Root', timestamp: '2026-04-27T08:00:00.000Z' },
  // Issue #661 PR-B AC-9: 'removed' must be a first-class kind so the activity
  // feed can surface GameRemovedFromLibraryEvent rows now that PR-A's
  // DomainEventLog is persisting them and the query handler projects them.
  { id: 'a5', kind: 'removed', entityTitle: 'Azul', timestamp: '2026-04-26T08:00:00.000Z' },
];

describe('RecentActivityRail (Wave B.3)', () => {
  describe('Phase 1 — empty placeholder (i18n)', () => {
    it('renders the i18n empty copy when items is empty', () => {
      renderRail();
      expect(screen.getByTestId('library-activity-empty-text')).toHaveTextContent(
        /Nessuna attività recente/i
      );
    });

    it('renders the i18n heading', () => {
      renderRail();
      expect(screen.getByRole('heading', { level: 3 })).toHaveTextContent(/Ultime modifiche/i);
    });

    it('exposes data-slot="library-activity-rail" + data-state="empty" on root', () => {
      const { container } = renderRail();
      const root = container.querySelector('[data-slot="library-activity-rail"]');
      expect(root).not.toBeNull();
      expect(root).toHaveAttribute('data-state', 'empty');
    });
  });

  describe('isLoading skeleton', () => {
    it('renders 3 skeleton lines when isLoading is true', () => {
      const { container } = renderRail({ isLoading: true });
      const skeletons = container.querySelectorAll('[data-testid="library-activity-skeleton"]');
      expect(skeletons).toHaveLength(3);
    });

    it('exposes aria-busy="true" + data-state="loading" on root when loading', () => {
      const { container } = renderRail({ isLoading: true });
      const root = container.querySelector('[data-slot="library-activity-rail"]');
      expect(root).toHaveAttribute('aria-busy', 'true');
      expect(root).toHaveAttribute('data-state', 'loading');
    });
  });

  describe('error state (R4)', () => {
    it('shows i18n error message when error prop is set', () => {
      renderRail({ items: [], error: new Error('500') });
      expect(screen.getByTestId('library-activity-error')).toHaveTextContent(
        /Impossibile caricare l'attività/i
      );
      expect(screen.queryByTestId('library-activity-skeleton')).toBeNull();
      expect(screen.queryByRole('list')).toBeNull();
    });

    it('error state takes precedence over loading', () => {
      renderRail({ items: [], isLoading: true, error: new Error('e') });
      expect(screen.getByTestId('library-activity-error')).toBeInTheDocument();
      expect(screen.queryByTestId('library-activity-skeleton')).toBeNull();
    });

    it('exposes data-state="error" on root when error is set', () => {
      const { container } = renderRail({ error: new Error('oops') });
      const root = container.querySelector('[data-slot="library-activity-rail"]');
      expect(root).toHaveAttribute('data-state', 'error');
    });

    it('error element has role="alert"', () => {
      renderRail({ error: new Error('fail') });
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });

  describe('Phase 2 readiness — items list', () => {
    it('renders one list item per activity (length match)', () => {
      const { container } = renderWithIntl(<RecentActivityRail items={sampleItems} />);
      const items = container.querySelectorAll('[data-slot="library-activity-item"]');
      expect(items).toHaveLength(5);
    });

    it('renders entityTitle text for each activity item', () => {
      renderWithIntl(<RecentActivityRail items={sampleItems} />);
      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Wingspan')).toBeInTheDocument();
      expect(screen.getByText('Brass: Birmingham')).toBeInTheDocument();
      expect(screen.getByText('Root')).toBeInTheDocument();
      expect(screen.getByText('Azul')).toBeInTheDocument();
    });

    it('exposes data-activity-kind on each item for per-kind styling', () => {
      const { container } = renderWithIntl(<RecentActivityRail items={sampleItems} />);
      const items = container.querySelectorAll('[data-slot="library-activity-item"]');
      const kinds = Array.from(items).map(node => node.getAttribute('data-activity-kind'));
      expect(kinds).toEqual(['play', 'add', 'kb-indexed', 'rating-changed', 'removed']);
    });
  });

  // ─── PR2 Task 2.4 (#1585 follow-up): mockup conformance ────────────────────
  // Mockup ref: admin-mockups/design_files/sp4-library-desktop.jsx:949-1017
  describe('PR2 Task 2.4 — mockup conformance (timeline + shortcuts box)', () => {
    it('renders timeline circle for each item with entity color matching kind', () => {
      // 'add' → maps to 'game' entity slot (per ACTIVITY_KIND_ENTITY map)
      const items: readonly ActivityItem[] = [
        { id: '1', kind: 'add', entityTitle: 'Catan', timestamp: '5 min fa' },
      ];
      const { container } = renderWithIntl(<RecentActivityRail items={items} />);
      const circle = container.querySelector('[data-slot="library-activity-rail-circle"]');
      expect(circle).not.toBeNull();
      // The circle uses Tailwind entity utility for the 'game' slot (add → game)
      expect(circle?.className).toMatch(/entity-game/);
    });

    it('renders connecting line between items (n-1 connectors for n items)', () => {
      const items: readonly ActivityItem[] = [
        { id: '1', kind: 'play', entityTitle: 'A', timestamp: '1m' },
        { id: '2', kind: 'agent', entityTitle: 'B', timestamp: '2m' },
        { id: '3', kind: 'kb-indexed', entityTitle: 'C', timestamp: '3m' },
      ];
      const { container } = renderWithIntl(<RecentActivityRail items={items} />);
      const connectors = container.querySelectorAll(
        '[data-slot="library-activity-rail-connector"]'
      );
      // 3 items → 2 connectors (last item has no trailing line)
      expect(connectors).toHaveLength(2);
    });

    it('renders the keyboard shortcuts box at the bottom', () => {
      renderRail();
      expect(screen.getByText(/Shortcuts/i)).toBeInTheDocument();
      expect(screen.getByText(/focus search/i)).toBeInTheDocument();
      expect(screen.getByText(/filtri avanzati/i)).toBeInTheDocument();
      expect(screen.getByText(/tutte le scorciatoie/i)).toBeInTheDocument();
    });

    it('renders the panel header with collapse button (aria-label from i18n)', () => {
      renderRail();
      expect(
        screen.getByRole('heading', { level: 3, name: /Ultime modifiche/i })
      ).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Comprimi pannello/i })).toBeInTheDocument();
    });
  });
});
