/**
 * SessionKpiGrid unit tests — Wave D.3 (Issue #756).
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { SessionKpiGrid } from '../SessionKpiGrid';
import type { KpiEntry, SessionKpiGridProps } from '../SessionKpiGrid';

const KPIS: KpiEntry[] = [
  { key: 'duration', label: 'Durata', value: '1h 24min', emoji: '⏱', entity: 'session' },
  { key: 'turns', label: 'Turni', value: '18', emoji: '🔄', entity: 'session' },
  { key: 'mvp', label: 'MVP', value: 'Marco', emoji: '🏆', entity: 'toolkit' },
  { key: 'avg', label: 'Score medio', value: '71.75', emoji: '📊', entity: 'session' },
];

const DEFAULT_PROPS: SessionKpiGridProps = {
  kpis: KPIS,
  gridAriaLabel: 'Statistiche partita',
};

describe('SessionKpiGrid', () => {
  it('renders data-slot="session-kpi-grid"', () => {
    render(<SessionKpiGrid {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="session-kpi-grid"]')).not.toBeNull();
  });

  it('renders <dl> with aria-label from gridAriaLabel', () => {
    render(<SessionKpiGrid {...DEFAULT_PROPS} />);
    const grid = document.querySelector('[data-slot="session-kpi-grid"]')!;
    expect(grid.tagName).toBe('DL');
    expect(grid.getAttribute('aria-label')).toBe('Statistiche partita');
  });

  it('renders one tile per KPI entry', () => {
    render(<SessionKpiGrid {...DEFAULT_PROPS} />);
    expect(document.querySelectorAll('[data-slot="session-kpi-tile"]').length).toBe(4);
  });

  it('renders dt label and dd value per KPI', () => {
    render(<SessionKpiGrid {...DEFAULT_PROPS} />);
    expect(screen.getByText('Durata')).toBeTruthy();
    expect(screen.getByText('1h 24min')).toBeTruthy();
    expect(screen.getByText('MVP')).toBeTruthy();
    expect(screen.getByText('Marco')).toBeTruthy();
  });

  it('marks each tile with data-kpi-key for stable selection', () => {
    render(<SessionKpiGrid {...DEFAULT_PROPS} />);
    const tiles = document.querySelectorAll('[data-slot="session-kpi-tile"]');
    expect(tiles[0].getAttribute('data-kpi-key')).toBe('duration');
    expect(tiles[2].getAttribute('data-kpi-key')).toBe('mvp');
  });

  it('uses 2-column mobile / 4-column desktop responsive grid', () => {
    render(<SessionKpiGrid {...DEFAULT_PROPS} />);
    const grid = document.querySelector('[data-slot="session-kpi-grid"]')!;
    expect(grid.className).toMatch(/grid-cols-2/);
    expect(grid.className).toMatch(/sm:grid-cols-4/);
  });

  it('handles empty KPI list gracefully', () => {
    render(<SessionKpiGrid {...DEFAULT_PROPS} kpis={[]} />);
    expect(document.querySelectorAll('[data-slot="session-kpi-tile"]').length).toBe(0);
  });

  it('applies className when provided', () => {
    render(<SessionKpiGrid {...DEFAULT_PROPS} className="custom-grid" />);
    const grid = document.querySelector('[data-slot="session-kpi-grid"]')!;
    expect(grid.classList.contains('custom-grid')).toBe(true);
  });

  it('renders dt/dd semantic markup', () => {
    render(<SessionKpiGrid {...DEFAULT_PROPS} />);
    const tile = document.querySelector('[data-slot="session-kpi-tile"]')!;
    expect(tile.querySelector('dt')).not.toBeNull();
    expect(tile.querySelector('dd')).not.toBeNull();
  });
});
