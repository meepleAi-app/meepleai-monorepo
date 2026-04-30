/**
 * Wave B.3 (Issue #574) — RecentActivityRail v2 component tests.
 *
 * Phase 1 contract: `items.length === 0` always (no backend endpoint
 * `GET /api/v1/library/activity` yet). Empty placeholder with copy
 * "Activity feed prossimamente" or skeleton 3 lines when `isLoading`.
 *
 * Phase 2 readiness: when `items.length > 0`, render list with per-kind icon.
 * This branch is exercised here against synthetic data so the component is
 * ready to be wired when the backend endpoint lands (no rewrite needed).
 *
 * Pure component (labels not yet i18n-bound — copy is Italian literal here
 * matching the empty-state UX copy locked in spec §3.2 / AC-7).
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { RecentActivityRail, type ActivityItem } from '../RecentActivityRail';

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
];

describe('RecentActivityRail (Wave B.3)', () => {
  describe('Phase 1 — empty placeholder', () => {
    it('renders the "Activity feed prossimamente" copy when items is empty', () => {
      render(<RecentActivityRail items={[]} />);
      expect(screen.getByText(/activity feed prossimamente/i)).toBeInTheDocument();
    });

    it('exposes data-slot="library-activity-rail" + data-state="empty" on root', () => {
      const { container } = render(<RecentActivityRail items={[]} />);
      const root = container.querySelector('[data-slot="library-activity-rail"]');
      expect(root).not.toBeNull();
      expect(root).toHaveAttribute('data-state', 'empty');
    });
  });

  describe('isLoading skeleton', () => {
    it('renders 3 skeleton lines when isLoading is true', () => {
      const { container } = render(<RecentActivityRail items={[]} isLoading />);
      const skeletons = container.querySelectorAll('[data-slot="library-activity-skeleton"]');
      expect(skeletons).toHaveLength(3);
    });

    it('exposes aria-busy="true" + data-state="loading" on root when loading', () => {
      const { container } = render(<RecentActivityRail items={[]} isLoading />);
      const root = container.querySelector('[data-slot="library-activity-rail"]');
      expect(root).toHaveAttribute('aria-busy', 'true');
      expect(root).toHaveAttribute('data-state', 'loading');
    });
  });

  describe('Phase 2 readiness — items list', () => {
    it('renders one list item per activity (length match)', () => {
      const { container } = render(<RecentActivityRail items={sampleItems} />);
      const items = container.querySelectorAll('[data-slot="library-activity-item"]');
      expect(items).toHaveLength(4);
    });

    it('renders entityTitle text for each activity item', () => {
      render(<RecentActivityRail items={sampleItems} />);
      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Wingspan')).toBeInTheDocument();
      expect(screen.getByText('Brass: Birmingham')).toBeInTheDocument();
      expect(screen.getByText('Root')).toBeInTheDocument();
    });

    it('exposes data-activity-kind on each item for per-kind styling', () => {
      const { container } = render(<RecentActivityRail items={sampleItems} />);
      const items = container.querySelectorAll('[data-slot="library-activity-item"]');
      const kinds = Array.from(items).map(node => node.getAttribute('data-activity-kind'));
      expect(kinds).toEqual(['play', 'add', 'kb-indexed', 'rating-changed']);
    });
  });
});
