import { render, screen } from '@testing-library/react';
import { StatsBlock } from '../blocks/StatsBlock';

describe('StatsBlock', () => {
  it('renders stats entries with labels and values', () => {
    render(
      <StatsBlock
        title="Statistiche"
        entityColor="25 95% 45%"
        data={{
          type: 'stats',
          entries: [
            { label: 'Partite', value: 12 },
            { label: 'Win Rate', value: '42%' },
          ],
        }}
      />
    );
    expect(screen.getByText('Statistiche')).toBeInTheDocument();
    expect(screen.getByText('Partite')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByText('Win Rate')).toBeInTheDocument();
    expect(screen.getByText('42%')).toBeInTheDocument();
  });

  it('renders icons when provided', () => {
    render(
      <StatsBlock
        title="Stats"
        entityColor="25 95% 45%"
        data={{
          type: 'stats',
          entries: [{ label: 'Games', value: 5, icon: '🎲' }],
        }}
      />
    );
    expect(screen.getByText('🎲')).toBeInTheDocument();
  });

  it('renders empty state when no entries', () => {
    render(
      <StatsBlock title="Stats" entityColor="25 95% 45%" data={{ type: 'stats', entries: [] }} />
    );
    expect(screen.getByText('No data yet')).toBeInTheDocument();
  });

  it('applies entity color to title', () => {
    render(
      <StatsBlock
        title="Stats"
        entityColor="25 95% 45%"
        data={{ type: 'stats', entries: [{ label: 'X', value: 1 }] }}
      />
    );
    const title = screen.getByText('Stats');
    // JSDOM normalizes hsl() to rgb(); verify the color style property is set
    expect(title.getAttribute('style')).toContain('color:');
    expect(title.style.color).not.toBe('');
  });
});
