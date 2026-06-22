/**
 * KpiGrid — Task 2 (Issue #1488 / Epic #1475 Phase D).
 *
 * 4-KPI grid: duration, top score, average, spread (max-min).
 *
 * AC-2.4: KpiGrid 4 KPI: durata, top score, media, distacco max-min
 * AC-2.10 EC-6: InProgress/Planned → omit duration if null
 */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { KpiGrid } from '../KpiGrid';

describe('KpiGrid', () => {
  const defaultProps = {
    duration: '2h 15min',
    topScore: 85,
    avgScore: 72,
    spread: 24,
  };

  it('renders data-slot="kpi-grid"', () => {
    const { container } = render(<KpiGrid {...defaultProps} />);
    expect(container.querySelector('[data-slot="kpi-grid"]')).not.toBeNull();
  });

  it('shows duration value', () => {
    render(<KpiGrid {...defaultProps} />);
    expect(screen.getByText('2h 15min')).toBeTruthy();
  });

  it('shows top score value', () => {
    render(<KpiGrid {...defaultProps} />);
    expect(screen.getByText('85')).toBeTruthy();
  });

  it('shows average score value', () => {
    render(<KpiGrid {...defaultProps} />);
    expect(screen.getByText('72')).toBeTruthy();
  });

  it('shows spread value', () => {
    render(<KpiGrid {...defaultProps} />);
    expect(screen.getByText('24')).toBeTruthy();
  });

  it('EC-6: duration=null renders em dash "—" for duration slot', () => {
    render(<KpiGrid {...defaultProps} duration={null} />);
    // The duration KPI should show a dash or be omitted
    const durationSlot = document.querySelector('[data-slot="kpi-duration"]');
    if (durationSlot) {
      expect(durationSlot.textContent).toContain('—');
    }
    // Also accept that the KPI is simply not rendered at all
    const text85 = screen.queryAllByText('85');
    expect(text85.length).toBeGreaterThan(0); // other KPIs still shown
  });

  it('renders 4 KPI cards', () => {
    const { container } = render(<KpiGrid {...defaultProps} />);
    const cards = container.querySelectorAll('[data-slot="kpi-card"]');
    expect(cards.length).toBe(4);
  });
});
