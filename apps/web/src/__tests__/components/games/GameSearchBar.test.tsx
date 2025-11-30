import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '@testing-library/jest-dom';
import { GameSearchBar } from '@/components/games/GameSearchBar';

vi.useFakeTimers();

describe('GameSearchBar', () => {
  it('renders with placeholder text', () => {
    render(<GameSearchBar value="" onChange={vi.fn()} />);
    expect(screen.getByPlaceholderText('Search games by title or publisher...')).toBeInTheDocument();
  });

  it('renders with custom placeholder', () => {
    render(
      <GameSearchBar
        value=""
        onChange={vi.fn()}
        placeholder="Custom placeholder"
      />
    );
    expect(screen.getByPlaceholderText('Custom placeholder')).toBeInTheDocument();
  });

  it('displays current value', () => {
    render(<GameSearchBar value="Catan" onChange={vi.fn()} />);
    expect(screen.getByDisplayValue('Catan')).toBeInTheDocument();
  });

  it('calls onChange with debounce', async () => {
    const handleChange = vi.fn();
    render(<GameSearchBar value="" onChange={handleChange} />);

    const input = screen.getByRole('textbox');
    fireEvent.change(input, { target: { value: 'Catan' } });

    // Should not call immediately
    expect(handleChange).not.toHaveBeenCalled();

    // Fast-forward time by debounce delay (300ms)
    vi.advanceTimersByTime(300);

    // Now it should be called
    expect(handleChange).toHaveBeenCalledWith('Catan');
  });

  it('respects custom debounce delay', () => {
    const handleChange = vi.fn();
    render(<GameSearchBar value="" onChange={handleChange} debounceMs={500} />);

    const input = screen.getByRole('textbox');
    fireEvent.change(input, { target: { value: 'Test' } });

    // Should not call after 300ms
    vi.advanceTimersByTime(300);
    expect(handleChange).not.toHaveBeenCalled();

    // Should call after 500ms
    vi.advanceTimersByTime(200);
    expect(handleChange).toHaveBeenCalledWith('Test');
  });

  it('shows clear button when value exists', () => {
    render(<GameSearchBar value="Catan" onChange={vi.fn()} />);
    expect(screen.getByLabelText('Clear search')).toBeInTheDocument();
  });

  it('hides clear button when value is empty', () => {
    render(<GameSearchBar value="" onChange={vi.fn()} />);
    expect(screen.queryByLabelText('Clear search')).not.toBeInTheDocument();
  });

  it('clears value when clear button is clicked', () => {
    const handleChange = vi.fn();
    render(<GameSearchBar value="Catan" onChange={handleChange} />);

    const clearButton = screen.getByLabelText('Clear search');
    fireEvent.click(clearButton);

    expect(handleChange).toHaveBeenCalledWith('');
  });

  it('clears value when Escape key is pressed', () => {
    const handleChange = vi.fn();
    render(<GameSearchBar value="Catan" onChange={handleChange} />);

    const input = screen.getByRole('textbox');
    fireEvent.keyDown(input, { key: 'Escape', code: 'Escape' });

    expect(handleChange).toHaveBeenCalledWith('');
  });

  it('syncs local value with external value changes', () => {
    const { rerender } = render(<GameSearchBar value="Catan" onChange={vi.fn()} />);

    expect(screen.getByDisplayValue('Catan')).toBeInTheDocument();

    rerender(<GameSearchBar value="Pandemic" onChange={vi.fn()} />);

    expect(screen.getByDisplayValue('Pandemic')).toBeInTheDocument();
  });

  it('has proper accessibility attributes', () => {
    render(<GameSearchBar value="" onChange={vi.fn()} />);
    const input = screen.getByRole('textbox');
    expect(input).toHaveAttribute('aria-label', 'Search games');
  });
});
