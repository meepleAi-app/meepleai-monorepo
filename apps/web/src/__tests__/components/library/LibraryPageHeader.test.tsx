import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { LibraryPageHeader } from '@/components/library/LibraryPageHeader';

describe('LibraryPageHeader', () => {
  it('renders title and count badge', () => {
    render(<LibraryPageHeader gameCount={24} onAddGame={vi.fn()} />);
    expect(screen.getByRole('heading', { name: /i miei giochi/i })).toBeInTheDocument();
    expect(screen.getByText('24 giochi')).toBeInTheDocument();
  });

  it('renders add game CTA button', () => {
    const onAddGame = vi.fn();
    render(<LibraryPageHeader gameCount={24} onAddGame={onAddGame} />);
    const btns = screen.getAllByLabelText(/aggiungi un gioco alla libreria/i);
    fireEvent.click(btns[0]);
    expect(onAddGame).toHaveBeenCalledOnce();
  });

  it('shows 0 giochi when empty', () => {
    render(<LibraryPageHeader gameCount={0} onAddGame={vi.fn()} />);
    expect(screen.getByText('0 giochi')).toBeInTheDocument();
  });

  it('has accessible CTA label', () => {
    render(<LibraryPageHeader gameCount={5} onAddGame={vi.fn()} />);
    const btns = screen.getAllByLabelText('Aggiungi un gioco alla libreria');
    expect(btns.length).toBeGreaterThanOrEqual(1);
  });
});
