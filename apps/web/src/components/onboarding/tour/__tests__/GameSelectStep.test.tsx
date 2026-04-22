import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { GameSelectStep } from '../GameSelectStep';

describe('GameSelectStep', () => {
  it('renders 8 games as aria-pressed buttons', () => {
    render(<GameSelectStep selected={[]} onToggle={vi.fn()} />);
    const buttons = screen.getAllByRole('button');
    expect(buttons).toHaveLength(8);
    for (const btn of buttons) expect(btn).toHaveAttribute('aria-pressed');
  });

  it('reflects selected state on aria-pressed', () => {
    render(<GameSelectStep selected={['catan', 'azul']} onToggle={vi.fn()} />);
    expect(screen.getByRole('button', { name: /catan/i })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /azul/i })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /wingspan/i })).toHaveAttribute(
      'aria-pressed',
      'false'
    );
  });

  it('calls onToggle with game id on click', async () => {
    const onToggle = vi.fn();
    const user = userEvent.setup();
    render(<GameSelectStep selected={[]} onToggle={onToggle} />);
    await user.click(screen.getByRole('button', { name: /catan/i }));
    expect(onToggle).toHaveBeenCalledWith('catan');
  });

  it('counter shows remaining needed when under MIN_SELECTED', () => {
    render(<GameSelectStep selected={['catan']} onToggle={vi.fn()} />);
    const counter = screen.getByText(/1 di 3 selezionati/i);
    expect(counter).toBeInTheDocument();
    expect(counter).toHaveAttribute('aria-live', 'polite');
  });

  it('counter shows ready state at MIN_SELECTED', () => {
    render(<GameSelectStep selected={['catan', 'azul', 'wingspan']} onToggle={vi.fn()} />);
    expect(screen.getByText(/3 giochi selezionati/i)).toBeInTheDocument();
  });
});
