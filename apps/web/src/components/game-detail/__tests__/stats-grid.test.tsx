/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { StatsGrid } from '../stats-grid';

describe('StatsGrid', () => {
  it('renders all stat metrics', () => {
    render(
      <StatsGrid timesPlayed={5} winRate="60%" avgDuration="2h 30m" lastPlayed="2024-01-20" />
    );

    expect(screen.getByText('Times Played')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('60%')).toBeInTheDocument();
    expect(screen.getByText('2h 30m')).toBeInTheDocument();
  });

  it('shows N/A for missing stats', () => {
    render(<StatsGrid timesPlayed={0} winRate={null} avgDuration={null} lastPlayed={null} />);

    const naElements = screen.getAllByText('N/A');
    expect(naElements).toHaveLength(2); // winRate and avgDuration
    expect(screen.getByText('Never')).toBeInTheDocument(); // lastPlayed
  });
});
