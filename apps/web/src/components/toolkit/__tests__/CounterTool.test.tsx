import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { CounterTool } from '../CounterTool';
import { useStandaloneToolkitStore } from '@/lib/stores/standalone-toolkit-store';

beforeEach(() => {
  useStandaloneToolkitStore.setState({ counters: [] });
});

describe('CounterTool', () => {
  it('mostra il valore iniziale', () => {
    render(<CounterTool id="c1" name="Punti" initialValue={0} />);
    expect(screen.getByTestId('counter-value')).toHaveTextContent('0');
  });

  it('incrementa di 1', () => {
    render(<CounterTool id="c1" name="Punti" initialValue={0} />);
    fireEvent.click(screen.getByRole('button', { name: /\+/i }));
    expect(screen.getByTestId('counter-value')).toHaveTextContent('1');
  });

  it('decrementa di 1', () => {
    render(<CounterTool id="c1" name="Punti" initialValue={5} />);
    fireEvent.click(screen.getByRole('button', { name: /-/i }));
    expect(screen.getByTestId('counter-value')).toHaveTextContent('4');
  });

  it('non va sotto il minimo', () => {
    render(<CounterTool id="c1" name="Punti" initialValue={0} min={0} />);
    fireEvent.click(screen.getByRole('button', { name: /-/i }));
    expect(screen.getByTestId('counter-value')).toHaveTextContent('0');
  });

  it('non supera il massimo', () => {
    render(<CounterTool id="c1" name="Punti" initialValue={10} max={10} />);
    fireEvent.click(screen.getByRole('button', { name: /\+/i }));
    expect(screen.getByTestId('counter-value')).toHaveTextContent('10');
  });

  it('reset riporta al valore iniziale', () => {
    render(<CounterTool id="c1" name="Punti" initialValue={3} />);
    fireEvent.click(screen.getByRole('button', { name: /\+/i }));
    fireEvent.click(screen.getByRole('button', { name: /reset/i }));
    expect(screen.getByTestId('counter-value')).toHaveTextContent('3');
  });
});
