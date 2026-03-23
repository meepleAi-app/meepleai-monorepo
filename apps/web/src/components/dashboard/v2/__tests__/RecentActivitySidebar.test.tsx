/**
 * RecentActivitySidebar — Unit Tests
 * Dashboard v2 "Il Tavolo"
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { RecentActivitySidebar } from '../RecentActivitySidebar';
import type { ActivityItem, ActivityType } from '../RecentActivitySidebar';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const makeActivity = (
  id: string,
  type: ActivityType,
  entityName: string,
  description = 'ha fatto qualcosa'
): ActivityItem => ({
  id,
  type,
  entityName,
  description,
  timestamp: new Date(Date.now() - 20 * 60000).toISOString(), // 20 min ago
});

const MOCK_ACTIVITIES: ActivityItem[] = [
  makeActivity('v1', 'game', 'Catan', 'aggiunto alla libreria'),
  makeActivity('v2', 'agent', 'Agente Regole', 'consultato'),
  makeActivity('v3', 'kb', 'Manuale Wingspan', 'caricato'),
  makeActivity('v4', 'session', 'Partita Catan', 'completata'),
  makeActivity('v5', 'game', 'Ticket to Ride', 'aggiunto ai preferiti'),
];

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('RecentActivitySidebar', () => {
  it('renders section title with correct text', () => {
    render(<RecentActivitySidebar activities={MOCK_ACTIVITIES} />);

    expect(screen.getByTestId('sidebar-activity')).toBeInTheDocument();
    expect(screen.getByText('📋 Attività Recenti')).toBeInTheDocument();
  });

  it('renders correct number of items (max 5)', () => {
    const sixActivities = [...MOCK_ACTIVITIES, makeActivity('v6', 'game', 'Agricola', 'esplorato')];
    render(<RecentActivitySidebar activities={sixActivities} />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Agente Regole')).toBeInTheDocument();
    expect(screen.getByText('Manuale Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Partita Catan')).toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
    expect(screen.queryByText('Agricola')).not.toBeInTheDocument();
  });

  it('renders all 5 items when exactly 5 activities provided', () => {
    render(<RecentActivitySidebar activities={MOCK_ACTIVITIES} />);

    MOCK_ACTIVITIES.forEach(a => {
      expect(screen.getByText(a.entityName)).toBeInTheDocument();
    });
  });

  it('hides section when activities array is empty', () => {
    const { container } = render(<RecentActivitySidebar activities={[]} />);

    expect(screen.queryByTestId('sidebar-activity')).not.toBeInTheDocument();
    expect(container.firstChild).toBeNull();
  });

  it('renders skeletons when loading', () => {
    render(<RecentActivitySidebar activities={[]} loading />);

    expect(screen.getByTestId('sidebar-activity')).toBeInTheDocument();
    const skeletons = screen
      .getAllByRole('generic')
      .filter(el => el.className.includes('animate-pulse'));
    expect(skeletons.length).toBeGreaterThanOrEqual(4);
    expect(screen.queryByText('Catan')).not.toBeInTheDocument();
  });

  it('shows section title even when loading', () => {
    render(<RecentActivitySidebar activities={[]} loading />);

    expect(screen.getByText('📋 Attività Recenti')).toBeInTheDocument();
  });

  it('renders correct icons for each activity type', () => {
    render(
      <RecentActivitySidebar
        activities={[
          makeActivity('i1', 'game', 'Gioco Test'),
          makeActivity('i2', 'agent', 'Agente Test'),
          makeActivity('i3', 'kb', 'KB Test'),
          makeActivity('i4', 'session', 'Sessione Test'),
        ]}
      />
    );

    const root = screen.getByTestId('sidebar-activity');
    expect(root.textContent).toContain('🎲');
    expect(root.textContent).toContain('⚡');
    expect(root.textContent).toContain('📜');
    expect(root.textContent).toContain('⏳');
  });

  it('renders description text for each activity', () => {
    render(
      <RecentActivitySidebar
        activities={[makeActivity('d1', 'game', 'Catan', 'aggiunto alla libreria')]}
      />
    );

    expect(screen.getByText(/aggiunto alla libreria/)).toBeInTheDocument();
  });

  it('renders relative time for each activity', () => {
    render(<RecentActivitySidebar activities={[makeActivity('r1', 'game', 'Catan')]} />);

    expect(screen.getByText(/20 min fa/)).toBeInTheDocument();
  });
});
