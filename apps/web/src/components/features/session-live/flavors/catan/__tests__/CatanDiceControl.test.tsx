import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CatanDiceControl } from '../CatanDiceControl';

const labels = {
  lastLabel: 'Ultimo tiro',
  historyLabel: 'Cronologia',
  rollAriaTemplate: 'Registra tiro {n}',
};

describe('CatanDiceControl', () => {
  it('shows the last roll', () => {
    render(<CatanDiceControl dice={{ last: 8, history: [8, 6] }} editable={false} {...labels} />);
    expect(screen.getByText('Ultimo tiro')).toBeInTheDocument();
    const diceLastSpan = screen.getByText('8', { selector: '[data-slot="catan-dice-last"]' });
    expect(diceLastSpan).toBeInTheDocument();
  });

  it('read-only mode has no roll buttons', () => {
    render(<CatanDiceControl dice={{ last: null, history: [] }} editable={false} {...labels} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host mode: tapping a value fires onRoll(sum)', async () => {
    const onRoll = vi.fn();
    render(
      <CatanDiceControl dice={{ last: null, history: [] }} editable onRoll={onRoll} {...labels} />
    );
    await userEvent.click(screen.getByRole('button', { name: 'Registra tiro 8' }));
    expect(onRoll).toHaveBeenCalledWith(8);
  });

  it('host mode renders quick-tap buttons 2..12', () => {
    render(
      <CatanDiceControl dice={{ last: null, history: [] }} editable onRoll={vi.fn()} {...labels} />
    );
    expect(screen.getAllByRole('button')).toHaveLength(11); // 2..12
  });
});
