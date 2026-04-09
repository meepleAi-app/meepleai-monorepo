import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ChatCitationCard } from '../ChatCitationCard';

describe('ChatCitationCard', () => {
  const citation = {
    documentName: 'Regolamento Azul v1.2',
    pages: [7, 8],
    excerpt:
      'La partita termina alla fine del round in cui un giocatore ha completato almeno una riga.',
  };

  it('renders document name and pages', () => {
    render(<ChatCitationCard citation={citation} />);
    expect(screen.getByText(/Regolamento Azul/i)).toBeInTheDocument();
    expect(screen.getByText(/pag\. 7/i)).toBeInTheDocument();
    expect(screen.getByText(/pag\. 8/i)).toBeInTheDocument();
  });

  it('renders the excerpt as italic quote', () => {
    render(<ChatCitationCard citation={citation} />);
    expect(screen.getByText(/La partita termina/i)).toBeInTheDocument();
  });

  it('renders a single page without repetition', () => {
    render(<ChatCitationCard citation={{ ...citation, pages: [7] }} />);
    expect(screen.getByText(/pag\. 7/i)).toBeInTheDocument();
    expect(screen.queryByText(/pag\. 8/i)).not.toBeInTheDocument();
  });
});
