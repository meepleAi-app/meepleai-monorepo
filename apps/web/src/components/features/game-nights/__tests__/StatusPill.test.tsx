/**
 * Tests for StatusPill (SP4 #1170 commit 2).
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { StatusKey } from '@/lib/game-nights/view-model';

import { StatusPill, type StatusPillLabels } from '../StatusPill';

const labels: StatusPillLabels = {
  confirmed: 'Confermata',
  planned: 'Programmata',
  cancelled: 'Annullata',
  completed: 'Completata',
};

describe('StatusPill', () => {
  it.each<[StatusKey, string]>([
    ['confirmed', 'Confermata'],
    ['planned', 'Programmata'],
    ['cancelled', 'Annullata'],
    ['completed', 'Completata'],
  ])('renders %s label', (statusKey, expected) => {
    render(<StatusPill statusKey={statusKey} labels={labels} />);
    expect(screen.getByText(expected)).toBeInTheDocument();
    const pill = screen.getByTestId('game-nights-status-pill');
    expect(pill.getAttribute('data-status')).toBe(statusKey);
  });

  it('renders an aria-hidden dot', () => {
    const { container } = render(<StatusPill statusKey="confirmed" labels={labels} />);
    const dot = container.querySelector('[aria-hidden="true"]');
    expect(dot).not.toBeNull();
  });

  it('forwards className', () => {
    render(<StatusPill statusKey="planned" labels={labels} className="custom-extra" />);
    expect(screen.getByTestId('game-nights-status-pill').className).toContain('custom-extra');
  });
});
