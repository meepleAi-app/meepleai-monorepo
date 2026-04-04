import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { RulesContent } from '../RulesContent';

describe('RulesContent', () => {
  it('renders the rules-content container', () => {
    render(<RulesContent gameId={null} />);
    expect(screen.getByTestId('rules-content')).toBeInTheDocument();
  });

  it('shows placeholder when gameId is null', () => {
    render(<RulesContent gameId={null} />);
    expect(screen.getByText('Seleziona un gioco per vederne le regole')).toBeInTheDocument();
  });

  it('shows KB placeholder when gameId is set', () => {
    render(<RulesContent gameId="game-123" />);
    expect(
      screen.getByText('Le regole verranno caricate dalla knowledge base del gioco')
    ).toBeInTheDocument();
  });

  it('does not show null placeholder when gameId is set', () => {
    render(<RulesContent gameId="game-123" />);
    expect(screen.queryByText('Seleziona un gioco per vederne le regole')).not.toBeInTheDocument();
  });
});
