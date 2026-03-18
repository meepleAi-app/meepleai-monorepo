import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { DealtGameCard } from '../DealtGameCard';

describe('DealtGameCard', () => {
  it('renders game title', () => {
    render(<DealtGameCard game={{ id: 'g1', title: 'Catan' }} onRemove={vi.fn()} rotation={-2} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('applies rotation transform', () => {
    render(<DealtGameCard game={{ id: 'g1', title: 'Catan' }} onRemove={vi.fn()} rotation={-2} />);
    const card = screen.getByTestId('dealt-card');
    expect(card.style.transform).toContain('rotate(-2deg)');
  });

  it('calls onRemove when remove button clicked', async () => {
    const onRemove = vi.fn();
    const user = userEvent.setup();
    render(<DealtGameCard game={{ id: 'g1', title: 'Catan' }} onRemove={onRemove} rotation={1} />);
    await user.click(screen.getByRole('button', { name: /rimuovi/i }));
    expect(onRemove).toHaveBeenCalledWith('g1');
  });
});
