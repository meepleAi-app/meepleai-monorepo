import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { AISuggestionCard } from '../AISuggestionCard';

describe('AISuggestionCard', () => {
  it('renders AI suggestion title', () => {
    render(
      <AISuggestionCard suggestions={[{ gameTitle: 'Catan', reason: 'Perfect for 4 players' }]} />
    );
    expect(screen.getByText(/suggerimenti ai/i)).toBeInTheDocument();
  });

  it('renders game suggestions', () => {
    render(
      <AISuggestionCard suggestions={[{ gameTitle: 'Catan', reason: 'Adatto a principianti' }]} />
    );
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText(/adatto a principianti/i)).toBeInTheDocument();
  });

  it('shows empty state when no suggestions', () => {
    render(<AISuggestionCard suggestions={[]} />);
    expect(screen.getByText(/nessun suggerimento/i)).toBeInTheDocument();
  });
});
