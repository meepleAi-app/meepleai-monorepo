/**
 * TranslationViewer — unit tests (Phase 3, Task 3.5b + spec-panel enhancements)
 *
 * Covers ACs 3.5b-1 through 3.5b-7 including spec-panel additions:
 *   AC-3.5b-1: Displays translated text by default
 *   AC-3.5b-2: Shows loading skeleton when isLoading=true
 *   AC-3.5b-3: aria-pressed toggle between original/translated
 *   AC-3.5b-4: Show original/translated toggle hidden when only one text
 *   AC-3.5b-5: Default font text-lg (≥18px); distance reading bumps to text-2xl
 *   AC-3.5b-6: Empty state + retry CTA when both texts undefined
 *   AC-3.5b-7: Toggle resets to translated view on pageNumber change
 *
 * Strategy:
 *  - Mock `@/hooks/useTranslation` to avoid IntlProvider setup complexity
 *  - Use React Testing Library + @testing-library/jest-dom assertions
 *  - Render directly (no QueryClientProvider needed — component is pure presentational)
 */

import { describe, expect, test, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { TranslationViewer } from '../_components/TranslationViewer';

// ── Mocks ────────────────────────────────────────────────────────────────────

/**
 * Mock useTranslation to return a simple `t` function that returns
 * the defaultMessage (second string arg) or the key itself.
 * Matches the real hook signature: { t, formatMessage, locale, ... }
 */
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (
      key: string,
      valuesOrDefault?: string | Record<string, string | number | boolean>,
      defaultValue?: string
    ) => {
      // t(key, defaultMessage: string)
      if (typeof valuesOrDefault === 'string') return valuesOrDefault;
      // t(key, values, defaultMessage: string)
      if (typeof defaultValue === 'string') return defaultValue;
      // t(key) — return key as fallback
      return key;
    },
  }),
}));

// ── Helpers ──────────────────────────────────────────────────────────────────

function renderViewer(props: Partial<React.ComponentProps<typeof TranslationViewer>> = {}) {
  return render(
    <TranslationViewer
      pageNumber={5}
      isLoading={false}
      translatedText="Translated content"
      {...props}
    />
  );
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('TranslationViewer', () => {
  test('AC-3.5b-1: displays translated text by default', () => {
    renderViewer({ translatedText: 'Rule 5: the player advances' });
    expect(screen.getByText('Rule 5: the player advances')).toBeInTheDocument();
  });

  test('AC-3.5b-2: shows loading skeleton when isLoading=true', () => {
    renderViewer({ isLoading: true });
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  test('AC-3.5b-3: show-original toggle toggles aria-pressed and swaps text', () => {
    renderViewer({
      originalText: 'Testo originale',
      translatedText: 'Translated text',
    });

    const toggleBtn = screen.getByRole('button', { name: /mostra originale/i });
    expect(toggleBtn).toHaveAttribute('aria-pressed', 'false');

    fireEvent.click(toggleBtn);

    // aria-pressed flips to true
    expect(toggleBtn).toHaveAttribute('aria-pressed', 'true');
    // Original text now visible
    expect(screen.getByText('Testo originale')).toBeInTheDocument();
  });

  test('AC-3.5b-6: empty state shows "Traduzione non disponibile" and retry CTA', () => {
    const onRetry = vi.fn();
    renderViewer({ translatedText: undefined, originalText: undefined, onRetry });

    expect(screen.getByText(/traduzione non disponibile/i)).toBeInTheDocument();
    const retryBtn = screen.getByRole('button', { name: /riprova/i });
    expect(retryBtn).toBeInTheDocument();

    fireEvent.click(retryBtn);
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  test('AC-3.5b-6: empty state has no retry CTA when onRetry not provided', () => {
    renderViewer({ translatedText: undefined, originalText: undefined });
    expect(screen.getByText(/traduzione non disponibile/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /riprova/i })).not.toBeInTheDocument();
  });

  test('AC-3.5b-5: distance reading toggle changes data-distance-mode to true and applies text-2xl', () => {
    renderViewer({ translatedText: 'Test text' });
    const distanceBtn = screen.getByRole('button', { name: /lettura distante/i });

    // Before toggle: data-distance-mode="false", class text-lg
    const article = screen.getByRole('article');
    const textEl = article.querySelector('[data-distance-mode="false"]');
    expect(textEl).toBeInTheDocument();
    expect(textEl?.className).toContain('text-lg');

    fireEvent.click(distanceBtn);

    // After toggle: data-distance-mode="true", class text-2xl
    const distanceEl = article.querySelector('[data-distance-mode="true"]');
    expect(distanceEl).toBeInTheDocument();
    expect(distanceEl?.className).toContain('text-2xl');
  });

  test('AC-3.5b-5: default font class is text-lg (18px ≥ vision §4.2 minimum)', () => {
    renderViewer({ translatedText: 'Default text' });
    const article = screen.getByRole('article');
    const textEl = article.querySelector('[data-distance-mode="false"]');
    expect(textEl?.className).toContain('text-lg');
  });

  test('hides original/translated toggle when only translatedText is provided', () => {
    renderViewer({ translatedText: 'Only translation', originalText: undefined });
    expect(screen.queryByRole('button', { name: /mostra originale/i })).not.toBeInTheDocument();
  });

  test('preserves toggle state on re-render with same pageNumber', () => {
    const { rerender } = renderViewer({
      pageNumber: 5,
      originalText: 'Originale 5',
      translatedText: 'Translated 5',
    });

    fireEvent.click(screen.getByRole('button', { name: /mostra originale/i }));
    expect(screen.getByText('Originale 5')).toBeInTheDocument();

    rerender(
      <TranslationViewer
        pageNumber={5}
        isLoading={false}
        originalText="Originale 5"
        translatedText="Translated 5"
      />
    );
    // Same pageNumber → toggle state preserved → still showing original
    expect(screen.getByText('Originale 5')).toBeInTheDocument();
  });

  test('AC-3.5b-7: resets to translated view when pageNumber changes', () => {
    const { rerender } = renderViewer({
      pageNumber: 5,
      originalText: 'Originale 5',
      translatedText: 'Translated 5',
    });

    // Switch to original on page 5
    fireEvent.click(screen.getByRole('button', { name: /mostra originale/i }));
    expect(screen.getByText('Originale 5')).toBeInTheDocument();

    // Simulate page navigation
    rerender(
      <TranslationViewer
        pageNumber={6}
        isLoading={false}
        originalText="Originale 6"
        translatedText="Translated 6"
      />
    );
    // Page changed → toggle reset → translated text shown
    expect(screen.getByText('Translated 6')).toBeInTheDocument();
    expect(screen.queryByText('Originale 6')).not.toBeInTheDocument();
  });
});
