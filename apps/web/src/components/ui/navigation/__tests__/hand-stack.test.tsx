import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { HandStack } from '../hand-stack';

import type { HandCard } from '@/hooks/use-hand-context';

// Mock navigation-icons to return simple spans
vi.mock('@/components/ui/data-display/meeple-card-features/navigation-icons', () => ({
  ENTITY_NAV_ICONS: new Proxy({}, {
    get: () => (props: Record<string, unknown>) => <span data-testid="icon" {...props} />,
  }),
}));

const mockCards: HandCard[] = [
  { id: '1', entity: 'game', title: 'Catan', href: '/library/1' },
  { id: '2', entity: 'session', title: 'Session #1', href: '/sessions/2' },
  { id: '3', entity: 'agent', title: 'RAG Agent', href: '/agents/3' },
];

describe('HandStack', () => {
  it('should render all cards as mini items', () => {
    render(
      <HandStack cards={mockCards} focusedIdx={0} onCardClick={vi.fn()} />
    );
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Session #1')).toBeInTheDocument();
    expect(screen.getByText('RAG Agent')).toBeInTheDocument();
  });

  it('should render with hand-stack testid', () => {
    render(
      <HandStack cards={mockCards} focusedIdx={0} onCardClick={vi.fn()} />
    );
    expect(screen.getByTestId('hand-stack')).toBeInTheDocument();
  });

  it('should highlight the focused card', () => {
    render(
      <HandStack cards={mockCards} focusedIdx={1} onCardClick={vi.fn()} />
    );
    const items = screen.getAllByTestId(/^hand-stack-item-/);
    expect(items[1]).toHaveAttribute('data-focused', 'true');
  });

  it('should call onCardClick with index when clicked', () => {
    const onCardClick = vi.fn();
    render(
      <HandStack cards={mockCards} focusedIdx={0} onCardClick={onCardClick} />
    );
    fireEvent.click(screen.getByText('RAG Agent'));
    expect(onCardClick).toHaveBeenCalledWith(2);
  });

  it('should render nothing when cards is empty', () => {
    const { container } = render(
      <HandStack cards={[]} focusedIdx={-1} onCardClick={vi.fn()} />
    );
    expect(container.firstChild).toBeNull();
  });

  it('should set entity-colored CSS variable on items', () => {
    render(
      <HandStack cards={mockCards} focusedIdx={0} onCardClick={vi.fn()} />
    );
    const items = screen.getAllByTestId(/^hand-stack-item-/);
    // Game entity has hsl '25 95% 45%'
    expect(items[0].style.getPropertyValue('--hand-hsl')).toBe('25 95% 45%');
  });
});
