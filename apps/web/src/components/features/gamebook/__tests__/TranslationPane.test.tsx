import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';

import { TranslationPane } from '../TranslationPane';

expect.extend(toHaveNoViolations);

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

  describe('AAA contrast + reader-mode (#1561 J + #1558 H)', () => {
    it('body <p> uses var(--c-text-high-contrast) via inline style or class (S9)', () => {
      render(
        <TranslationPane
          partialText="Test paragraph"
          isComplete={true}
          appliedTerms={[]}
          sourceTextEn=""
        />
      );
      const para = screen.getByText('Test paragraph');
      // Either inline style OR via class with custom property
      const computed = window.getComputedStyle(para);
      // jsdom returns the literal style value; the cascade resolves at runtime in browser
      expect(para.getAttribute('style')).toContain('var(--c-text-high-contrast)');
    });

    it('body <p> has .reader-mode-content class for parent data-attr cascading', () => {
      render(
        <TranslationPane partialText="Test" isComplete={true} appliedTerms={[]} sourceTextEn="" />
      );
      const para = screen.getByText('Test');
      expect(para).toHaveClass('reader-mode-content');
    });

    it('has zero AAA color-contrast violations on :root (light theme) (S7)', async () => {
      const { container } = render(
        <TranslationPane
          partialText="Lorem ipsum dolor sit amet"
          isComplete={true}
          appliedTerms={[]}
          sourceTextEn=""
        />
      );
      const results = await axe(container, {
        rules: { 'color-contrast-enhanced': { enabled: true } },
      });
      expect(results).toHaveNoViolations();
    });

    it('has zero AAA color-contrast violations on [data-theme="dark"] (S8)', async () => {
      document.documentElement.setAttribute('data-theme', 'dark');
      const { container } = render(
        <TranslationPane
          partialText="Lorem ipsum dolor sit amet"
          isComplete={true}
          appliedTerms={[]}
          sourceTextEn=""
        />
      );
      const results = await axe(container, {
        rules: { 'color-contrast-enhanced': { enabled: true } },
      });
      expect(results).toHaveNoViolations();
      document.documentElement.removeAttribute('data-theme');
    });
  });
});
