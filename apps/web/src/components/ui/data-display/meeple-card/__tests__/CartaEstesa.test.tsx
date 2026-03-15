import { render, screen } from '@testing-library/react';
import { CartaEstesa } from '../CartaEstesa';

describe('CartaEstesa', () => {
  const props = {
    title: 'Catan',
    subtitle: 'Kosmos • Klaus Teuber • 1995',
    imageUrl: 'https://example.com/catan.jpg',
    rating: 7.1,
    entityColor: '25 95% 45%',
    stats: [
      { label: 'Giocatori', value: '3-4', color: '25 95% 45%' },
      { label: 'Sessioni', value: '12', color: '240 60% 55%' },
    ],
    tags: ['Strategy', 'Trading'],
    description: 'Build settlements and trade resources...',
  };

  it('renders cover with title and rating', () => {
    render(<CartaEstesa {...props} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('7.1')).toBeInTheDocument();
  });

  it('renders stats chips', () => {
    render(<CartaEstesa {...props} />);
    expect(screen.getByText('3-4')).toBeInTheDocument();
    expect(screen.getByText('Giocatori')).toBeInTheDocument();
  });

  it('renders tags', () => {
    render(<CartaEstesa {...props} />);
    expect(screen.getByText('Strategy')).toBeInTheDocument();
    expect(screen.getByText('Trading')).toBeInTheDocument();
  });

  it('renders description', () => {
    render(<CartaEstesa {...props} />);
    expect(screen.getByText(/Build settlements/)).toBeInTheDocument();
  });

  it('renders children (for icon bar slot)', () => {
    render(
      <CartaEstesa {...props}>
        <div data-testid="icon-bar">bar</div>
      </CartaEstesa>
    );
    expect(screen.getByTestId('icon-bar')).toBeInTheDocument();
  });

  it('applies card styling with rounded corners', () => {
    render(<CartaEstesa {...props} />);
    const card = screen.getByTestId('carta-estesa');
    expect(card).toHaveClass('rounded-[20px]');
  });
});
