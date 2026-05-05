import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { FirstSessionStep } from '../FirstSessionStep';

describe('FirstSessionStep', () => {
  it('renders 3 action cards with entity data attribute', () => {
    render(<FirstSessionStep onChoose={vi.fn()} />);
    const cards = screen.getAllByRole('button');
    expect(cards).toHaveLength(3);
    const entities = cards.map(c => c.getAttribute('data-entity'));
    expect(entities).toEqual(['event', 'game', 'agent']);
  });

  it('fires onChoose with action id and href', async () => {
    const onChoose = vi.fn();
    const user = userEvent.setup();
    render(<FirstSessionStep onChoose={onChoose} />);
    await user.click(screen.getByRole('button', { name: /esplora la library/i }));
    expect(onChoose).toHaveBeenCalledWith({ id: 'library', href: '/library' });
  });
});
