import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';

import { TranslationPane } from '../TranslationPane';

describe('TranslationPane', () => {
  it('renders error alert when error prop is provided', () => {
    render(
      <TranslationPane
        partialText=""
        isComplete={false}
        appliedTerms={[]}
        sourceTextEn="You stand at the crossroads."
        error="translation_failed"
      />
    );
    const alert = screen.getByRole('alert');
    expect(alert).toBeInTheDocument();
    expect(alert).toHaveTextContent('translation_failed');
    // No translation pane IT section when in error state
    expect(screen.queryByTestId('translation-pane-it')).not.toBeInTheDocument();
  });

  it('renders applied glossary terms when present', () => {
    render(
      <TranslationPane
        partialText="Sei in una foresta buia."
        isComplete={true}
        appliedTerms={['Dragon', 'Forest', 'Dragon']}
        sourceTextEn="You are in a dark forest."
      />
    );
    const termsEl = screen.getByTestId('translation-pane-terms');
    // Deduplicates terms
    expect(termsEl).toHaveTextContent('Dragon');
    expect(termsEl).toHaveTextContent('Forest');
    // Appears only once despite duplicate in input
    expect(termsEl.textContent).toContain('Dragon, Forest');
  });

  it('shows placeholder text when partialText is empty and not complete', () => {
    render(
      <TranslationPane
        partialText=""
        isComplete={false}
        appliedTerms={[]}
        sourceTextEn="source text"
      />
    );
    expect(screen.getByText('Traduzione in corso…')).toBeInTheDocument();
  });

  it('shows completion indicator when isComplete is true', () => {
    render(
      <TranslationPane
        partialText="Testo tradotto."
        isComplete={true}
        appliedTerms={[]}
        sourceTextEn="Translated text."
      />
    );
    expect(screen.getByText(/completata/)).toBeInTheDocument();
  });

  it('does not render terms section when appliedTerms is empty', () => {
    render(
      <TranslationPane
        partialText="Ciao"
        isComplete={false}
        appliedTerms={[]}
        sourceTextEn="Hello"
      />
    );
    expect(screen.queryByTestId('translation-pane-terms')).not.toBeInTheDocument();
  });

  it('renders original English text in details element', () => {
    render(
      <TranslationPane
        partialText=""
        isComplete={false}
        appliedTerms={[]}
        sourceTextEn="You stand at the crossroads."
      />
    );
    expect(screen.getByText('You stand at the crossroads.')).toBeInTheDocument();
  });
});
