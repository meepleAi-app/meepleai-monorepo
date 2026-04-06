import { render, screen } from '@testing-library/react';
import { vi } from 'vitest';

import { StatsRow } from '../StatsRow';

const mockStats = {
  totalGames: 42,
  monthlyPlays: 7,
  monthlyPlaysChange: 15,
  weeklyPlayTime: '06:20:00',
  monthlyFavorites: 3,
};

vi.mock('@/lib/stores/dashboard-store', () => ({
  useDashboardStore: (selector: (s: { stats: typeof mockStats | null }) => unknown) =>
    selector({ stats: mockStats }),
}));

describe('StatsRow', () => {
  it('renders total games', () => {
    render(<StatsRow />);
    expect(screen.getByText('42')).toBeInTheDocument();
  });

  it('renders monthly plays', () => {
    render(<StatsRow />);
    expect(screen.getByText('7')).toBeInTheDocument();
  });

  it('parses weeklyPlayTime hours and minutes', () => {
    render(<StatsRow />);
    expect(screen.getByText('6h 20m')).toBeInTheDocument();
  });

  it('renders positive change with + prefix', () => {
    render(<StatsRow />);
    expect(screen.getByText('+15%')).toBeInTheDocument();
  });

  it('renders 4 stat cards', () => {
    render(<StatsRow />);
    expect(screen.getByText('giochi in libreria')).toBeInTheDocument();
    expect(screen.getByText('partite questo mese')).toBeInTheDocument();
    expect(screen.getByText('ore questa settimana')).toBeInTheDocument();
    expect(screen.getByText('vs mese scorso')).toBeInTheDocument();
  });
});
