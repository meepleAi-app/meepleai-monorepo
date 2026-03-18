import { render, screen, fireEvent } from '@testing-library/react';
import { vi, describe, it, expect } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(() => '/library/games/catan-123'),
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

vi.mock('@/components/layout/LayoutProvider', () => ({
  useLayout: vi.fn(() => ({ context: 'game_detail', setContext: vi.fn() })),
}));

vi.mock('@/stores/use-card-hand', () => ({
  useCardHand: vi.fn(() => ({
    cards: [
      { id: 'catan-123', entity: 'game', title: 'Catan', href: '/library/games/catan-123' },
      { id: 'risk-456', entity: 'game', title: 'Risk', href: '/library/games/risk-456' },
    ],
    focusedIdx: 0,
  })),
}));

vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => {
      const { initial, animate, exit, transition, drag, dragConstraints, onDragEnd, ...rest } =
        props;
      void initial;
      void animate;
      void exit;
      void transition;
      void drag;
      void dragConstraints;
      void onDragEnd;
      return <div {...rest}>{children}</div>;
    },
  },
  AnimatePresence: ({ children }: React.PropsWithChildren) => <>{children}</>,
}));

vi.mock('focus-trap-react', () => ({
  default: ({ children }: React.PropsWithChildren) => <>{children}</>,
}));

import { ContextualBottomSheet } from '../ContextualBottomSheet';

describe('ContextualBottomSheet', () => {
  it('renders nothing when closed', () => {
    render(<ContextualBottomSheet isOpen={false} onClose={() => {}} />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders dialog when open', () => {
    render(<ContextualBottomSheet isOpen={true} onClose={() => {}} />);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('shows entity header with title', () => {
    render(<ContextualBottomSheet isOpen={true} onClose={() => {}} />);
    expect(screen.getAllByText('Catan').length).toBeGreaterThan(0);
  });

  it('shows open cards section', () => {
    render(<ContextualBottomSheet isOpen={true} onClose={() => {}} />);
    expect(screen.getByText('Risk')).toBeInTheDocument();
  });

  it('shows contextual actions', () => {
    render(<ContextualBottomSheet isOpen={true} onClose={() => {}} />);
    expect(screen.getByText('FAQ')).toBeInTheDocument();
    expect(screen.getByText('Rules')).toBeInTheDocument();
  });

  it('calls onClose when backdrop clicked', () => {
    const onClose = vi.fn();
    render(<ContextualBottomSheet isOpen={true} onClose={onClose} />);
    fireEvent.click(screen.getByTestId('bottom-sheet-backdrop'));
    expect(onClose).toHaveBeenCalled();
  });

  it('has correct a11y attributes', () => {
    render(<ContextualBottomSheet isOpen={true} onClose={() => {}} />);
    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAttribute('aria-modal', 'true');
  });
});
