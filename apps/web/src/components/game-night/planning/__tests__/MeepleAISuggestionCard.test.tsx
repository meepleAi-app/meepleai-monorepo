/**
 * @vitest-environment jsdom
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { MeepleAISuggestionCard } from '../MeepleAISuggestionCard';

describe('MeepleAISuggestionCard', () => {
  it('renders empty state with correct entity type', () => {
    render(<MeepleAISuggestionCard suggestions={[]} />);
    const card = screen.getByTestId('ai-suggestion-card');
    expect(card).toHaveAttribute('data-entity', 'agent');
  });

  // The featured variant is a fixed 400px wide; the planning layout puts this card in a 320px column. jsdom has no
  // layout, so the contract is the cap that keeps the card inside whatever column holds it.
  it('caps the card width to its column in the empty state', () => {
    render(<MeepleAISuggestionCard suggestions={[]} />);
    expect(screen.getByTestId('ai-suggestion-card')).toHaveClass('max-w-full');
  });

  it('caps the card width to its column when suggestions are listed', () => {
    render(
      <MeepleAISuggestionCard suggestions={[{ gameTitle: 'Catan', reason: 'Good for groups' }]} />
    );
    const card = screen.getByTestId('ai-suggestion-card').querySelector('[data-entity="agent"]');
    expect(card).toHaveClass('max-w-full');
  });

  it('displays suggestion titles when provided', () => {
    const suggestions = [
      { gameTitle: 'Catan', reason: 'Good for groups' },
      { gameTitle: 'Ticket to Ride', reason: 'Easy to learn' },
    ];
    render(<MeepleAISuggestionCard suggestions={suggestions} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
  });
});
