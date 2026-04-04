import React from 'react';

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ScoreNumpad } from '../ScoreNumpad';

describe('ScoreNumpad', () => {
  it('renders buttons for digits 0-9', () => {
    render(<ScoreNumpad playerName="Alice" onSubmit={vi.fn()} onClose={vi.fn()} />);

    for (let i = 0; i <= 9; i++) {
      expect(screen.getByRole('button', { name: String(i) })).toBeInTheDocument();
    }
  });

  it('displays the player name', () => {
    render(<ScoreNumpad playerName="Mario Rossi" onSubmit={vi.fn()} onClose={vi.fn()} />);

    expect(screen.getByText('Mario Rossi')).toBeInTheDocument();
  });

  it('builds a multi-digit number and submits the correct value', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();

    render(<ScoreNumpad playerName="Bob" onSubmit={onSubmit} onClose={vi.fn()} />);

    await user.click(screen.getByRole('button', { name: '4' }));
    await user.click(screen.getByRole('button', { name: '2' }));
    await user.click(screen.getByRole('button', { name: 'Conferma' }));

    expect(onSubmit).toHaveBeenCalledOnce();
    expect(onSubmit).toHaveBeenCalledWith(42);
  });
});
