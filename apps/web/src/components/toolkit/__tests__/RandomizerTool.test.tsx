import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { Randomizer } from '../Randomizer';
import { useStandaloneToolkitStore } from '@/lib/stores/standalone-toolkit-store';

beforeEach(() => {
  useStandaloneToolkitStore.setState({
    randomizer: { originalItems: [], remainingItems: [], lastExtracted: null },
  });
});

describe('Randomizer', () => {
  it('mostra il campo di input per aggiungere voci', () => {
    render(<Randomizer />);
    expect(screen.getByPlaceholderText(/aggiungi/i)).toBeInTheDocument();
  });

  it('estrae un elemento dal pool', () => {
    useStandaloneToolkitStore.getState().setRandomizerItems(['Alice', 'Bob', 'Carlo']);
    render(<Randomizer />);
    fireEvent.click(screen.getByRole('button', { name: /estrai/i }));
    const extracted = screen.getByTestId('randomizer-result').textContent;
    expect(['Alice', 'Bob', 'Carlo']).toContain(extracted);
  });

  it('riduce il pool dopo estrazione (senza rimpiazzo)', () => {
    useStandaloneToolkitStore.getState().setRandomizerItems(['Alice', 'Bob']);
    render(<Randomizer />);
    fireEvent.click(screen.getByRole('button', { name: /estrai/i }));
    expect(screen.getByTestId('pool-count')).toHaveTextContent('1');
  });

  it('disabilita estrai quando pool è vuoto', () => {
    useStandaloneToolkitStore.getState().setRandomizerItems(['Solo']);
    render(<Randomizer />);
    fireEvent.click(screen.getByRole('button', { name: /estrai/i }));
    expect(screen.getByRole('button', { name: /estrai/i })).toBeDisabled();
  });

  it('reset ripristina il pool originale', () => {
    useStandaloneToolkitStore.getState().setRandomizerItems(['Alice', 'Bob']);
    render(<Randomizer />);
    fireEvent.click(screen.getByRole('button', { name: /estrai/i }));
    fireEvent.click(screen.getByRole('button', { name: /reset/i }));
    expect(screen.getByTestId('pool-count')).toHaveTextContent('2');
  });

  it('non accetta più di 50 voci', () => {
    const items = Array.from({ length: 50 }, (_, i) => `item${i}`);
    useStandaloneToolkitStore.getState().setRandomizerItems(items);
    render(<Randomizer />);
    const input = screen.getByPlaceholderText(/aggiungi/i);
    fireEvent.change(input, { target: { value: 'item51' } });
    fireEvent.keyDown(input, { key: 'Enter' });
    expect(screen.getByTestId('pool-count')).toHaveTextContent('50');
  });
});
