/**
 * Wave C.1 (Issue #581) — GameDetailFaqList unit tests.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import {
  GameDetailFaqList,
  type GameDetailFaqEntry,
  type GameDetailFaqListLabels,
} from '../GameDetailFaqList';

const labels: GameDetailFaqListLabels = {
  title: 'FAQ',
  subtitle: 'Le domande più frequenti.',
  viewAll: 'Vedi tutte',
  viewAllAriaLabel: 'Vai alla pagina FAQ completa',
  empty: 'Nessuna FAQ',
  questionAriaLabel: 'Domanda',
};

const faqs: ReadonlyArray<GameDetailFaqEntry> = [
  { id: 'f1', question: 'Quanto dura una partita?', answer: 'Circa 70 minuti.' },
  { id: 'f2', question: 'Quanti giocatori?', answer: 'Da 1 a 5.' },
];

describe('GameDetailFaqList (Wave C.1)', () => {
  it('renders empty state and no view-all link when faqs is empty', () => {
    render(<GameDetailFaqList faqs={[]} viewAllHref="/games/g/faqs" labels={labels} />);
    expect(screen.getByText('Nessuna FAQ')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Vai alla pagina/ })).not.toBeInTheDocument();
  });

  it('renders FAQ questions and answers when populated', () => {
    render(<GameDetailFaqList faqs={faqs} viewAllHref="/games/g/faqs" labels={labels} />);
    expect(screen.getByText('Quanto dura una partita?')).toBeInTheDocument();
    expect(screen.getByText('Circa 70 minuti.')).toBeInTheDocument();
    expect(screen.getByText('Quanti giocatori?')).toBeInTheDocument();
  });

  it('exposes a view-all link with the configured href when populated', () => {
    render(<GameDetailFaqList faqs={faqs} viewAllHref="/games/abc/faqs" labels={labels} />);
    const link = screen.getByRole('link', { name: 'Vai alla pagina FAQ completa' });
    expect(link).toHaveAttribute('href', '/games/abc/faqs');
  });

  it('exposes data-empty="true" when no FAQs', () => {
    const { container } = render(
      <GameDetailFaqList faqs={[]} viewAllHref="/games/g/faqs" labels={labels} />
    );
    const root = container.querySelector('[data-slot="game-detail-faq-list"]');
    expect(root).toHaveAttribute('data-empty', 'true');
  });

  it('exposes data-slot="game-detail-faq-list" for E2E selector', () => {
    const { container } = render(
      <GameDetailFaqList faqs={faqs} viewAllHref="/games/g/faqs" labels={labels} />
    );
    expect(container.querySelector('[data-slot="game-detail-faq-list"]')).toBeInTheDocument();
  });
});
