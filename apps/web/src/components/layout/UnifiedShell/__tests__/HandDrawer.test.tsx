import { render, screen } from '@testing-library/react';
import { HandDrawer } from '../HandDrawer';
import { useCardHand } from '@/stores/use-card-hand';

vi.mock('@/stores/use-card-hand');

describe('HandDrawer', () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it('renders nothing when no cards in hand', () => {
    vi.mocked(useCardHand).mockReturnValue({
      cards: [],
      focusedIdx: -1,
      isHandCollapsed: false,
      focusCard: vi.fn(),
      maxHandSize: 10,
    } as any);
    const { container } = render(<HandDrawer />);
    expect(container.firstChild).toBeNull();
  });

  it('renders cards when hand has cards', () => {
    vi.mocked(useCardHand).mockReturnValue({
      cards: [
        { id: 'g1', entity: 'game', title: 'Catan', href: '/library/g1' },
        { id: 'g2', entity: 'game', title: 'Azul', href: '/library/g2' },
      ],
      focusedIdx: 0,
      isHandCollapsed: false,
      focusCard: vi.fn(),
      maxHandSize: 10,
    } as any);
    render(<HandDrawer />);
    expect(screen.getByLabelText('Catan')).toBeInTheDocument();
    expect(screen.getByLabelText('Azul')).toBeInTheDocument();
  });

  it('renders nothing when collapsed', () => {
    vi.mocked(useCardHand).mockReturnValue({
      cards: [{ id: 'g1', entity: 'game', title: 'Catan', href: '/library/g1' }],
      focusedIdx: 0,
      isHandCollapsed: true,
      focusCard: vi.fn(),
      maxHandSize: 10,
    } as any);
    const { container } = render(<HandDrawer />);
    expect(container.firstChild).toBeNull();
  });

  it('has navigation role and aria-label', () => {
    vi.mocked(useCardHand).mockReturnValue({
      cards: [{ id: 'g1', entity: 'game', title: 'Catan', href: '/library/g1' }],
      focusedIdx: 0,
      isHandCollapsed: false,
      focusCard: vi.fn(),
      maxHandSize: 10,
    } as any);
    render(<HandDrawer />);
    expect(screen.getByRole('navigation')).toHaveAttribute('aria-label', 'Carte in mano');
  });

  it('shows card count', () => {
    vi.mocked(useCardHand).mockReturnValue({
      cards: [{ id: 'g1', entity: 'game', title: 'Catan', href: '/library/g1' }],
      focusedIdx: 0,
      isHandCollapsed: false,
      focusCard: vi.fn(),
      maxHandSize: 10,
    } as any);
    render(<HandDrawer />);
    expect(screen.getByText('1/10')).toBeInTheDocument();
  });
});
