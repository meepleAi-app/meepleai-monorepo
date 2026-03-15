import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HandDrawerCard } from '../HandDrawerCard';

describe('HandDrawerCard', () => {
  const card = {
    id: 'g1',
    entity: 'game' as const,
    title: 'Catan',
    href: '/library/g1',
    imageUrl: 'https://example.com/catan.jpg',
  };

  it('renders card with image', () => {
    render(<HandDrawerCard card={card} isFocused={false} onClick={vi.fn()} />);
    expect(screen.getByAltText('Catan')).toBeInTheDocument();
  });

  it('renders entity emoji fallback when no imageUrl', () => {
    render(
      <HandDrawerCard card={{ ...card, imageUrl: undefined }} isFocused={false} onClick={vi.fn()} />
    );
    expect(screen.getByLabelText('Catan')).toBeInTheDocument();
  });

  it('applies focused styles when isFocused', () => {
    render(<HandDrawerCard card={card} isFocused={true} onClick={vi.fn()} />);
    const btn = screen.getByRole('link');
    expect(btn).toHaveAttribute('aria-current', 'page');
  });

  it('does not set aria-current when not focused', () => {
    render(<HandDrawerCard card={card} isFocused={false} onClick={vi.fn()} />);
    const btn = screen.getByRole('link');
    expect(btn).not.toHaveAttribute('aria-current');
  });

  it('calls onClick when clicked', async () => {
    const onClick = vi.fn();
    render(<HandDrawerCard card={card} isFocused={false} onClick={onClick} />);
    await userEvent.click(screen.getByRole('link'));
    expect(onClick).toHaveBeenCalledOnce();
  });
});
