import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { NavbarMiniCards } from '../NavbarMiniCards';

describe('NavbarMiniCards', () => {
  const cards = [
    {
      id: 'g1',
      entity: 'game' as const,
      title: 'Catan',
      href: '/library/g1',
      imageUrl: 'https://example.com/catan.jpg',
    },
    {
      id: 'g2',
      entity: 'game' as const,
      title: 'Azul',
      href: '/library/g2',
    },
  ];

  it('renders mini icons for each card', () => {
    render(<NavbarMiniCards cards={cards} onExpand={vi.fn()} />);
    expect(screen.getByLabelText('Catan')).toBeInTheDocument();
    expect(screen.getByLabelText('Azul')).toBeInTheDocument();
  });

  it('each mini icon has minimum 44px tap target', () => {
    render(<NavbarMiniCards cards={cards} onExpand={vi.fn()} />);
    const btn = screen.getByLabelText('Catan');
    // Check the button has minimum tap target sizing
    expect(btn.className).toMatch(/min-w-\[44px\]|min-h-\[44px\]|p-3|p-\[12px\]/);
  });

  it('calls onExpand with card id when icon is tapped', async () => {
    const onExpand = vi.fn();
    render(<NavbarMiniCards cards={cards} onExpand={onExpand} />);
    await userEvent.click(screen.getByLabelText('Catan'));
    expect(onExpand).toHaveBeenCalledWith('g1');
  });
});
