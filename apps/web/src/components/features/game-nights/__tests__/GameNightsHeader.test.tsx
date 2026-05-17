/**
 * Tests for GameNightsHeader (SP4 #1170 commit 2).
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { GameNightsHeader, type GameNightsHeaderLabels } from '../GameNightsHeader';

const labels: GameNightsHeaderLabels = {
  kicker: 'Game nights',
  title: 'Le tue serate',
  countLine: '8 serate questo mese · 5 confermate',
  ctaNew: '+ Nuova serata',
  viewTablistAriaLabel: 'Modalità vista',
  viewCalendar: 'Calendario',
  viewList: 'Lista',
  filter: {
    ariaLabel: 'Filtri',
    all: 'Tutte',
    organizing: 'Sto organizzando',
    invited: 'Invitato',
    completed: 'Concluse',
  },
};

function renderHeader(overrides: Partial<Parameters<typeof GameNightsHeader>[0]> = {}) {
  const onViewChange = vi.fn();
  const onFilterChange = vi.fn();
  const onCreate = vi.fn();
  render(
    <GameNightsHeader
      view="calendar"
      onViewChange={onViewChange}
      filter="all"
      onFilterChange={onFilterChange}
      onCreate={onCreate}
      labels={labels}
      {...overrides}
    />
  );
  return { onViewChange, onFilterChange, onCreate };
}

describe('GameNightsHeader', () => {
  it('renders kicker, title, count line, and CTA', () => {
    renderHeader();
    expect(screen.getByText('Game nights')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Le tue serate');
    expect(screen.getByText('8 serate questo mese · 5 confermate')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '+ Nuova serata' })).toBeInTheDocument();
  });

  it('renders calendar/list tablist with aria-selected on active', () => {
    renderHeader({ view: 'list' });
    const tablist = screen.getByRole('tablist', { name: 'Modalità vista' });
    expect(tablist).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Lista' }).getAttribute('aria-selected')).toBe('true');
    expect(screen.getByRole('tab', { name: 'Calendario' }).getAttribute('aria-selected')).toBe(
      'false'
    );
  });

  it('invokes onViewChange when clicking a tab', () => {
    const { onViewChange } = renderHeader();
    fireEvent.click(screen.getByRole('tab', { name: 'Lista' }));
    expect(onViewChange).toHaveBeenCalledWith('list');
  });

  it('invokes onCreate when clicking CTA', () => {
    const { onCreate } = renderHeader();
    fireEvent.click(screen.getByRole('button', { name: '+ Nuova serata' }));
    expect(onCreate).toHaveBeenCalledTimes(1);
  });

  it('forwards filter changes from the embedded FilterPillBar', () => {
    const { onFilterChange } = renderHeader();
    fireEvent.click(screen.getByRole('button', { name: 'Sto organizzando' }));
    expect(onFilterChange).toHaveBeenCalledWith('organizing');
  });
});
