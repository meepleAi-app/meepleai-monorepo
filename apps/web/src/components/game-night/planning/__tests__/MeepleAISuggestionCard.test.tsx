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
