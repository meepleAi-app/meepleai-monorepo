/**
 * DashboardClient — Stage 3 cluster smoke tests (Issue #1164).
 *
 * Replaces the previous PR #309 test that targeted the old chat/session-centric
 * orchestrator. The new test exercises the REFACTOR-FORWARD shape:
 *   - Hero renders greeting + 4 KPI grid
 *   - All 5 sections render (Games / Players / Agents / Sessions / Events)
 *   - Empty-state path renders empty CTAs when all hooks return zero data
 *
 * Detailed per-section tests (variants, telemetry, live badge, derivation) are
 * deferred per spec AC11 (matches sibling Stage 3 FE PRs #1151/#1153/#1160/#1163).
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({
    user: { id: 'u1', displayName: 'Marco', email: 'marco@example.com' },
  }),
}));

const mockTranslate = (key: string): string => key;
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: mockTranslate }),
}));

vi.mock('@/hooks/queries/useGames', () => ({
  useGames: vi.fn(() => ({ data: { games: [], total: 0 }, isLoading: false })),
}));

vi.mock('@/hooks/queries/useActiveSessions', () => ({
  useActiveSessions: vi.fn(() => ({
    data: { sessions: [], total: 0, page: 1, pageSize: 10 },
    isLoading: false,
  })),
}));

vi.mock('@/hooks/queries/useAgents', () => ({
  useAgents: vi.fn(() => ({ data: [], isLoading: false })),
}));

vi.mock('@/hooks/queries/useGameNights', () => ({
  useUpcomingGameNights: vi.fn(() => ({ data: [], isLoading: false })),
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryStats: vi.fn(() => ({
    data: { totalGames: 0, favoriteGames: 0, privatePdfs: 0 },
    isLoading: false,
  })),
}));

vi.mock('@/lib/analytics/track-event', () => ({
  trackEvent: vi.fn(),
}));

import { DashboardClient } from '../DashboardClient';

describe('DashboardClient (Stage 3 cluster)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the dashboard hero with greeting', () => {
    render(<DashboardClient />);
    // Greeting key is rendered as the translation key string in mock; presence is sufficient.
    expect(screen.getByText(/Marco/i)).toBeInTheDocument();
  });

  it('renders all 5 sections via data-slot', () => {
    const { container } = render(<DashboardClient />);
    const sections = container.querySelectorAll('[data-slot="dashboard-section"]');
    expect(sections.length).toBe(5);

    const sectionIds = Array.from(sections).map(el => el.getAttribute('data-section-id'));
    expect(sectionIds).toEqual(
      expect.arrayContaining(['games', 'players', 'agents', 'sessions', 'events'])
    );
  });

  it('renders the hero KPI grid', () => {
    const { container } = render(<DashboardClient />);
    const kpiGrid = container.querySelector('[data-slot="dashboard-hero-kpi-grid"]');
    expect(kpiGrid).not.toBeNull();
    const kpiCards = container.querySelectorAll('[data-slot="dashboard-kpi-card"]');
    expect(kpiCards.length).toBe(4);
  });

  it('shows empty CTAs when all hooks return zero data', () => {
    const { container } = render(<DashboardClient />);
    // Each section should render its empty-state when no data; verifying via dashed border presence.
    // (Exhaustive empty-CTA assertions deferred to follow-up per spec AC11.)
    const dashedBorders = container.querySelectorAll('.border-dashed');
    expect(dashedBorders.length).toBeGreaterThanOrEqual(5);
  });
});
