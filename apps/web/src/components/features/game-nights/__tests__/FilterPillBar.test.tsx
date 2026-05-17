/**
 * Tests for FilterPillBar (SP4 #1170 commit 2).
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { FilterPillBar, type FilterPillBarLabels } from '../FilterPillBar';

const labels: FilterPillBarLabels = {
  ariaLabel: 'Filtri serate',
  all: 'Tutte',
  organizing: 'Sto organizzando',
  invited: 'Invitato',
  completed: 'Concluse',
};

describe('FilterPillBar', () => {
  it('renders four pills with provided labels', () => {
    render(<FilterPillBar value="all" onChange={() => {}} labels={labels} />);
    expect(screen.getByRole('group', { name: 'Filtri serate' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Tutte' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sto organizzando' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Invitato' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Concluse' })).toBeInTheDocument();
  });

  it('marks the active pill via aria-pressed=true', () => {
    render(<FilterPillBar value="organizing" onChange={() => {}} labels={labels} />);
    expect(
      screen.getByRole('button', { name: 'Sto organizzando' }).getAttribute('aria-pressed')
    ).toBe('true');
    expect(screen.getByRole('button', { name: 'Tutte' }).getAttribute('aria-pressed')).toBe(
      'false'
    );
  });

  it('invokes onChange with the clicked key', () => {
    const onChange = vi.fn();
    render(<FilterPillBar value="all" onChange={onChange} labels={labels} />);
    fireEvent.click(screen.getByRole('button', { name: 'Concluse' }));
    expect(onChange).toHaveBeenCalledWith('completed');
  });
});
