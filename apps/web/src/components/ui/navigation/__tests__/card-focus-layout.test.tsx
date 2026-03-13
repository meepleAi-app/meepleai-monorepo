import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { CardFocusLayout } from '../card-focus-layout';

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: React.forwardRef(
      (
        { children, ...props }: React.PropsWithChildren<Record<string, unknown>>,
        ref: React.Ref<HTMLDivElement>,
      ) => (
        <div ref={ref} {...props}>
          {children}
        </div>
      ),
    ),
  },
  useAnimation: () => ({ start: vi.fn() }),
}));

// Mock navigation-icons to return simple spans
vi.mock('@/components/ui/data-display/meeple-card-features/navigation-icons', () => ({
  ENTITY_NAV_ICONS: new Proxy({}, {
    get: () => (props: Record<string, unknown>) => <span data-testid="icon" {...props} />,
  }),
}));

// Mock the useHandContext store
const mockStore = {
  cards: [
    { id: '1', entity: 'game' as const, title: 'Catan', href: '/library/1' },
    { id: '2', entity: 'session' as const, title: 'Session', href: '/sessions/2' },
  ],
  focusedIdx: 0,
  handContext: 'library' as const,
  addCard: vi.fn(),
  removeCard: vi.fn(),
  focusCard: vi.fn(),
  swipeNext: vi.fn(),
  swipePrev: vi.fn(),
  setHandContext: vi.fn(),
  clear: vi.fn(),
};

vi.mock('@/hooks/use-hand-context', () => ({
  useHandContext: () => mockStore,
}));

describe('CardFocusLayout', () => {
  it('should render the layout with hand stack and focused area', () => {
    render(
      <CardFocusLayout>
        <div data-testid="card">Focused Card</div>
      </CardFocusLayout>
    );
    expect(screen.getByTestId('card-focus-layout')).toBeInTheDocument();
    expect(screen.getByTestId('hand-stack')).toBeInTheDocument();
    expect(screen.getByTestId('focused-card-area')).toBeInTheDocument();
  });

  it('should show context label', () => {
    render(
      <CardFocusLayout>
        <div>Card</div>
      </CardFocusLayout>
    );
    expect(screen.getByText(/la tua mano/i)).toBeInTheDocument();
  });
});
