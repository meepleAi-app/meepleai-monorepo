import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { RulesQuickDemo } from '../RulesQuickDemo';

describe('RulesQuickDemo', () => {
  it('renderizza il titolo del blocco', () => {
    render(<RulesQuickDemo />);
    expect(screen.getByRole('heading', { name: /chiedi una regola/i })).toBeInTheDocument();
  });

  it('mostra esempi di domande cliccabili', () => {
    render(<RulesQuickDemo />);
    const examples = screen.getAllByRole('button');
    expect(examples.length).toBeGreaterThanOrEqual(3);
  });

  it('ha un link a /register', () => {
    render(<RulesQuickDemo />);
    const cta = screen.getByRole('link', { name: /prova gratis|inizia|chiedi ora/i });
    expect(cta).toBeInTheDocument();
    expect(cta).toHaveAttribute('href', '/register');
  });
});
