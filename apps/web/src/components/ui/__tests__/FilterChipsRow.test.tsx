import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { FilterChipsRow } from '../FilterChipsRow';

describe('FilterChipsRow', () => {
  const chips = [
    { id: 'all', label: 'Tutti' },
    { id: 'recent', label: 'Recenti' },
    { id: 'rating', label: 'Rating \u2193' },
  ];

  it('renders all chips', () => {
    render(<FilterChipsRow chips={chips} activeId="all" onSelect={() => {}} />);
    expect(screen.getByText('Tutti')).toBeInTheDocument();
    expect(screen.getByText('Recenti')).toBeInTheDocument();
    expect(screen.getByText('Rating \u2193')).toBeInTheDocument();
  });

  it('marks active chip with aria-selected', () => {
    render(<FilterChipsRow chips={chips} activeId="all" onSelect={() => {}} />);
    expect(screen.getByText('Tutti').closest('button')).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText('Recenti').closest('button')).toHaveAttribute('aria-selected', 'false');
  });

  it('calls onSelect when chip clicked', async () => {
    const onSelect = vi.fn();
    render(<FilterChipsRow chips={chips} activeId="all" onSelect={onSelect} />);
    await userEvent.click(screen.getByText('Recenti'));
    expect(onSelect).toHaveBeenCalledWith('recent');
  });
});
