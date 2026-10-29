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

  // "Prova gratis — risposta in 10 secondi" measured 368px with the Button's `whitespace-nowrap`, inside a column
  // 326px wide on a 390px phone: the whole landing page scrolled sideways by 10px. jsdom has no layout (the
  // measurement was taken in a browser); what is pinned here is the class contract that lets the label wrap.
  it('lets the CTA wrap and stay inside its column on a phone', () => {
    render(<RulesQuickDemo />);
    const cta = screen.getByRole('link', { name: /prova gratis/i });
    expect(cta).toHaveClass('max-w-full', 'whitespace-normal', 'h-auto', 'text-center');
  });
});
