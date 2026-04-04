import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { DiceRoller } from '../DiceRoller';
import type { DiceConfig } from '@/lib/types/standalone-toolkit';

describe('DiceRoller — standard dice', () => {
  it('renders with default D6 config', () => {
    render(<DiceRoller config={{ name: 'D6', sides: 6, count: 1 }} />);
    expect(screen.getByTestId('dice-roller')).toBeInTheDocument();
    expect(screen.getByText('D6')).toBeInTheDocument();
  });

  it('shows result between 1 and sides after roll', () => {
    render(<DiceRoller config={{ name: 'D6', sides: 6, count: 1 }} />);
    fireEvent.click(screen.getByRole('button', { name: /tira/i }));
    const result = screen.getByTestId('dice-result');
    const value = parseInt(result.textContent ?? '0', 10);
    expect(value).toBeGreaterThanOrEqual(1);
    expect(value).toBeLessThanOrEqual(6);
  });

  it('calls onRoll callback for standard dice', () => {
    const onRoll = vi.fn();
    render(<DiceRoller config={{ name: 'D6', sides: 6, count: 1 }} onRoll={onRoll} />);
    fireEvent.click(screen.getByRole('button', { name: /tira/i }));
    expect(onRoll).toHaveBeenCalledWith(
      expect.objectContaining({ faces: expect.any(Array), total: expect.any(String) })
    );
  });
});

describe('DiceRoller — custom faces', () => {
  const customConfig: DiceConfig = {
    name: 'Skill Die',
    customFaces: ['✅', '✅', '✅', '❌', '❌', '⭐'],
    count: 1,
    description: 'Arkham Horror skill die',
  };

  it('renders custom dice name', () => {
    render(<DiceRoller config={customConfig} />);
    expect(screen.getByText('Skill Die')).toBeInTheDocument();
  });

  it('shows one of the defined faces after roll', () => {
    render(<DiceRoller config={customConfig} />);
    fireEvent.click(screen.getByRole('button', { name: /tira/i }));
    const result = screen.getByTestId('dice-result');
    expect(['✅', '❌', '⭐']).toContain(result.textContent?.trim());
  });

  it('calls onRoll callback with result', () => {
    const onRoll = vi.fn();
    render(<DiceRoller config={customConfig} onRoll={onRoll} />);
    fireEvent.click(screen.getByRole('button', { name: /tira/i }));
    expect(onRoll).toHaveBeenCalledWith(
      expect.objectContaining({ faces: expect.any(Array), total: expect.any(String) })
    );
  });
});

describe('DiceRoller — multi-dice', () => {
  it('shows sum total for 2D6', () => {
    render(<DiceRoller config={{ name: '2D6', sides: 6, count: 2 }} />);
    fireEvent.click(screen.getByRole('button', { name: /tira/i }));
    const result = parseInt(screen.getByTestId('dice-total').textContent ?? '0', 10);
    expect(result).toBeGreaterThanOrEqual(2);
    expect(result).toBeLessThanOrEqual(12);
  });

  it('shows multiple dice-result elements for custom multi-dice without dice-total', () => {
    render(
      <DiceRoller config={{ name: '2 Skill Dice', customFaces: ['✅', '❌', '⭐'], count: 2 }} />
    );
    fireEvent.click(screen.getByRole('button', { name: /tira/i }));
    const results = screen.getAllByTestId('dice-result');
    expect(results).toHaveLength(2);
    expect(screen.queryByTestId('dice-total')).toBeNull();
  });
});
