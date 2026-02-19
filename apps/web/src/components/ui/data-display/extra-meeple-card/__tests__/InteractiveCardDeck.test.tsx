/**
 * Tests for InteractiveCardDeck component
 * Issue #4763 - Interactive Cards + Timer + Events Timeline UI + Phase 3 Tests
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { InteractiveCardDeck } from '../tabs/InteractiveCardDeck';
import type { CardDeckState, CardDeckActions } from '../types';

// ============================================================================
// Test Data
// ============================================================================

const mockDeckState: CardDeckState = {
  toolName: 'Resource Cards',
  deckType: 'Standard',
  cards: [
    { id: 'c1', name: 'Wood', zone: 'deck', faceUp: false, order: 0 },
    { id: 'c2', name: 'Brick', zone: 'deck', faceUp: false, order: 1 },
    { id: 'c3', name: 'Sheep', zone: 'hand', faceUp: true, order: 0 },
    { id: 'c4', name: 'Wheat', zone: 'hand', faceUp: true, order: 1 },
    { id: 'c5', name: 'Ore', zone: 'discard', faceUp: true, order: 0 },
  ],
  deckCount: 2,
  handCount: 2,
  discardCount: 1,
  shuffleable: true,
  allowDraw: true,
  allowDiscard: true,
  allowPeek: true,
  allowReturnToDeck: true,
};

const mockActions: CardDeckActions = {
  onDraw: vi.fn(),
  onDiscard: vi.fn(),
  onShuffle: vi.fn(),
  onPeek: vi.fn(),
  onReturnToDeck: vi.fn(),
};

// ============================================================================
// Tests
// ============================================================================

describe('InteractiveCardDeck', () => {
  it('renders empty state when no state provided', () => {
    render(<InteractiveCardDeck />);

    expect(screen.getByText('No card deck active')).toBeInTheDocument();
  });

  it('renders deck name and type', () => {
    render(<InteractiveCardDeck state={mockDeckState} />);

    expect(screen.getByText('Resource Cards')).toBeInTheDocument();
    expect(screen.getByText('Standard')).toBeInTheDocument();
  });

  it('renders deck and discard piles with counts', () => {
    render(<InteractiveCardDeck state={mockDeckState} />);

    expect(screen.getByTestId('card-pile-deck')).toBeInTheDocument();
    expect(screen.getByTestId('card-pile-discard')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument(); // deck count
    expect(screen.getByText('1')).toBeInTheDocument(); // discard count
  });

  it('renders hand cards', () => {
    render(<InteractiveCardDeck state={mockDeckState} />);

    expect(screen.getByTestId('hand-card-c3')).toBeInTheDocument();
    expect(screen.getByTestId('hand-card-c4')).toBeInTheDocument();
    expect(screen.getByText('Sheep')).toBeInTheDocument();
    expect(screen.getByText('Wheat')).toBeInTheDocument();
  });

  it('shows hand count', () => {
    render(<InteractiveCardDeck state={mockDeckState} />);

    expect(screen.getByText('Hand (2)')).toBeInTheDocument();
  });

  it('renders action buttons when actions provided', () => {
    render(<InteractiveCardDeck state={mockDeckState} actions={mockActions} />);

    expect(screen.getByTestId('card-action-draw')).toBeInTheDocument();
    expect(screen.getByTestId('card-action-shuffle')).toBeInTheDocument();
    expect(screen.getByTestId('card-action-peek')).toBeInTheDocument();
  });

  it('calls onDraw when draw button clicked', async () => {
    const user = userEvent.setup();
    const onDraw = vi.fn();
    render(
      <InteractiveCardDeck state={mockDeckState} actions={{ onDraw }} />
    );

    await user.click(screen.getByTestId('card-action-draw'));

    expect(onDraw).toHaveBeenCalledTimes(1);
  });

  it('calls onShuffle when shuffle button clicked', async () => {
    const user = userEvent.setup();
    const onShuffle = vi.fn();
    render(
      <InteractiveCardDeck state={mockDeckState} actions={{ onShuffle }} />
    );

    await user.click(screen.getByTestId('card-action-shuffle'));

    expect(onShuffle).toHaveBeenCalledTimes(1);
  });

  it('calls onPeek when peek button clicked', async () => {
    const user = userEvent.setup();
    const onPeek = vi.fn();
    render(
      <InteractiveCardDeck state={mockDeckState} actions={{ onPeek }} />
    );

    await user.click(screen.getByTestId('card-action-peek'));

    expect(onPeek).toHaveBeenCalledTimes(1);
  });

  it('hides draw button when deck is empty', () => {
    const emptyDeck = { ...mockDeckState, deckCount: 0 };
    render(<InteractiveCardDeck state={emptyDeck} actions={mockActions} />);

    expect(screen.queryByTestId('card-action-draw')).not.toBeInTheDocument();
  });

  it('hides shuffle button when only 1 card in deck', () => {
    const singleCard = { ...mockDeckState, deckCount: 1 };
    render(<InteractiveCardDeck state={singleCard} actions={mockActions} />);

    expect(screen.queryByTestId('card-action-shuffle')).not.toBeInTheDocument();
  });

  it('hides action buttons when not allowed', () => {
    const restricted = {
      ...mockDeckState,
      allowDraw: false,
      shuffleable: false,
      allowPeek: false,
    };
    render(<InteractiveCardDeck state={restricted} actions={mockActions} />);

    expect(screen.queryByTestId('card-action-draw')).not.toBeInTheDocument();
    expect(screen.queryByTestId('card-action-shuffle')).not.toBeInTheDocument();
    expect(screen.queryByTestId('card-action-peek')).not.toBeInTheDocument();
  });

  it('shows empty hand message when no cards in hand', () => {
    const noHand = {
      ...mockDeckState,
      cards: mockDeckState.cards.filter((c) => c.zone !== 'hand'),
      handCount: 0,
    };
    render(<InteractiveCardDeck state={noHand} />);

    expect(screen.getByText('No cards in hand')).toBeInTheDocument();
  });

  it('shows top discard card face up', () => {
    render(<InteractiveCardDeck state={mockDeckState} />);

    // Ore is the face-up top discard
    expect(screen.getByText('Ore')).toBeInTheDocument();
  });

  it('renders without actions (view-only mode)', () => {
    render(<InteractiveCardDeck state={mockDeckState} />);

    expect(screen.getByTestId('interactive-card-deck')).toBeInTheDocument();
    expect(screen.queryByTestId('card-action-draw')).not.toBeInTheDocument();
  });
});
