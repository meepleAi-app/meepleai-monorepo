import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { FocusedCardArea } from '../focused-card-area';

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

describe('FocusedCardArea', () => {
  it('should render children', () => {
    render(
      <FocusedCardArea onSwipeLeft={vi.fn()} onSwipeRight={vi.fn()}>
        <div data-testid="card-content">Test Card</div>
      </FocusedCardArea>
    );
    expect(screen.getByTestId('card-content')).toBeInTheDocument();
  });

  it('should render with focused-card-area testid', () => {
    render(
      <FocusedCardArea onSwipeLeft={vi.fn()} onSwipeRight={vi.fn()}>
        <div>Card</div>
      </FocusedCardArea>
    );
    expect(screen.getByTestId('focused-card-area')).toBeInTheDocument();
  });

  it('should show swipe indicators when hasPrev/hasNext', () => {
    render(
      <FocusedCardArea
        onSwipeLeft={vi.fn()}
        onSwipeRight={vi.fn()}
        hasPrev
        hasNext
      >
        <div>Card</div>
      </FocusedCardArea>
    );
    expect(screen.getByLabelText('Previous card')).toBeInTheDocument();
    expect(screen.getByLabelText('Next card')).toBeInTheDocument();
  });

  it('should call onSwipeLeft when next button clicked', () => {
    const onSwipeLeft = vi.fn();
    render(
      <FocusedCardArea onSwipeLeft={onSwipeLeft} onSwipeRight={vi.fn()} hasNext>
        <div>Card</div>
      </FocusedCardArea>
    );
    fireEvent.click(screen.getByLabelText('Next card'));
    expect(onSwipeLeft).toHaveBeenCalledTimes(1);
  });

  it('should call onSwipeRight when prev button clicked', () => {
    const onSwipeRight = vi.fn();
    render(
      <FocusedCardArea onSwipeLeft={vi.fn()} onSwipeRight={onSwipeRight} hasPrev>
        <div>Card</div>
      </FocusedCardArea>
    );
    fireEvent.click(screen.getByLabelText('Previous card'));
    expect(onSwipeRight).toHaveBeenCalledTimes(1);
  });
});
