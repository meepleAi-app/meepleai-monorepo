import { render } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, it, expect } from 'vitest';

import { DashboardHero } from '@/components/dashboard/DashboardHero';
import { DashboardStatCard } from '@/components/dashboard/DashboardStatCard';
import { DashboardStatsRow } from '@/components/dashboard/DashboardStatsRow';
import { EmptyCTA } from '@/components/dashboard/EmptyCTA';
import { DiscoverCarousel } from '@/components/ui/data-display/discover-carousel';

expect.extend(toHaveNoViolations);

describe('dashboard a11y', () => {
  it('DashboardHero has no axe violations', async () => {
    const { container } = render(<DashboardHero displayName="Marco" />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('DashboardStatCard (loaded) has no axe violations', async () => {
    const { container } = render(
      <DashboardStatCard entity="game" value={5} label="Giochi" href="/library" />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('DashboardStatCard (loading) has no axe violations', async () => {
    const { container } = render(
      <DashboardStatCard
        entity="game"
        value={0}
        label="Giochi"
        href="/library"
        isLoading
      />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('DashboardStatCard (error) has no axe violations', async () => {
    const { container } = render(
      <DashboardStatCard
        entity="game"
        value={0}
        label="Giochi"
        href="/library"
        isError
        onRetry={() => {}}
      />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('DashboardStatsRow has no axe violations', async () => {
    const state = { value: 1, isLoading: false, isError: false, isFetching: false };
    const { container } = render(
      <DashboardStatsRow
        stats={{ games: state, sessions: state, agents: state, events: state }}
        onRetry={{}}
      />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('EmptyCTA has no axe violations', async () => {
    const { container } = render(
      <EmptyCTA
        entity="session"
        icon="🎯"
        title="Nessuna sessione"
        sub="Crea una nuova partita."
        actions={[{ label: 'Crea', href: '/sessions/new', primary: true }]}
      />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('DiscoverCarousel has no axe violations', async () => {
    const { container } = render(
      <DiscoverCarousel ariaLabel="Carosello test">
        <div>Item 1</div>
        <div>Item 2</div>
      </DiscoverCarousel>
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
