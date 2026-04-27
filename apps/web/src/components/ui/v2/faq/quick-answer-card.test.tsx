import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { QuickAnswerCard } from './quick-answer-card';

describe('QuickAnswerCard', () => {
  const defaults = {
    question: 'How do I install a toolkit?',
    short: 'Click Install on the toolkit catalog page.',
    categoryLabel: 'Games',
    categoryIcon: '🎲',
    popularRank: 2,
    onClick: vi.fn(),
  };

  it('renders the question and short summary', () => {
    render(<QuickAnswerCard {...defaults} />);
    expect(screen.getByRole('heading', { level: 3 })).toHaveTextContent(
      'How do I install a toolkit?'
    );
    expect(screen.getByText(/Click Install on the toolkit/)).toBeInTheDocument();
  });

  it('renders the category label and icon', () => {
    render(<QuickAnswerCard {...defaults} />);
    expect(screen.getByText('Games')).toBeInTheDocument();
    expect(screen.getByText('🎲')).toBeInTheDocument();
  });

  it('renders the popularRank prefixed with #', () => {
    render(<QuickAnswerCard {...defaults} popularRank={5} />);
    expect(screen.getByText('#5')).toBeInTheDocument();
  });

  it('uses provided readMoreLabel', () => {
    render(<QuickAnswerCard {...defaults} readMoreLabel="Leggi tutto →" />);
    expect(screen.getByText('Leggi tutto →')).toBeInTheDocument();
  });

  it('falls back to default readMoreLabel', () => {
    render(<QuickAnswerCard {...defaults} />);
    expect(screen.getByText('Read more →')).toBeInTheDocument();
  });

  it('fires onClick when clicked', () => {
    const onClick = vi.fn();
    render(<QuickAnswerCard {...defaults} onClick={onClick} />);
    fireEvent.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('renders as a button element', () => {
    render(<QuickAnswerCard {...defaults} />);
    expect(screen.getByRole('button').tagName).toBe('BUTTON');
  });

  it('applies custom className', () => {
    render(<QuickAnswerCard {...defaults} className="extra-class" />);
    expect(screen.getByRole('button').className).toContain('extra-class');
  });

  it('applies game-entity border-left color via inline style', () => {
    render(<QuickAnswerCard {...defaults} />);
    expect(screen.getByRole('button').style.borderLeftColor).toContain('var(--c-game)');
  });

  it('accepts a ReactNode question (e.g. highlighted text with mark)', () => {
    const node = (
      <span data-testid="hl">
        How do I install a <mark>toolkit</mark>?
      </span>
    );
    render(<QuickAnswerCard {...defaults} question={node} />);
    expect(screen.getByTestId('hl').querySelector('mark')).not.toBeNull();
  });
});
