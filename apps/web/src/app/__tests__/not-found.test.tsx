/**
 * Regression tests for Issue #1076 — `NotFoundContent` hydration mismatch.
 *
 * `NotFoundContent` MUST NOT depend on the client-only `IntlProvider`
 * (mounted in `apps/web/src/app/providers.tsx`), otherwise SSR renders
 * the raw message-id keys (`pages.errors.notFound.title`) while the
 * client renders the translated strings → React error #418.
 *
 * Acceptance criteria validated below:
 *   - AC-1/AC-2: SSR pass succeeds without IntlProvider (no useTranslation())
 *   - AC-3:     SSR-rendered string and client-rendered DOM agree on
 *                the user-visible text (no message-id keys on either side)
 *   - Coverage: Translated IT strings appear in output (not message-ids)
 */
import { renderToString } from 'react-dom/server';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import NotFound from '../not-found';

describe('NotFound (Issue #1076)', () => {
  it('renders translated IT strings (not raw message-id keys)', () => {
    render(<NotFound />);

    expect(screen.getByText('404')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /Pagina non trovata/ })).toBeInTheDocument();
    expect(
      screen.getByText('La pagina che stai cercando non esiste o è stata spostata.')
    ).toBeInTheDocument();

    const homeCta = screen.getByRole('link', { name: 'Torna alla home' });
    const exploreCta = screen.getByRole('link', { name: 'Esplora i giochi' });
    expect(homeCta).toHaveAttribute('href', '/');
    expect(exploreCta).toHaveAttribute('href', '/games');
  });

  it('does NOT render raw i18n message-id keys (regression for #1076 root cause)', () => {
    const { container } = render(<NotFound />);
    const text = container.textContent ?? '';
    expect(text).not.toMatch(/pages\.errors\.notFound\./);
  });

  it('does NOT render English fallback strings (single-locale guarantee)', () => {
    const { container } = render(<NotFound />);
    const text = container.textContent ?? '';
    expect(text).not.toMatch(/Page not found/);
    expect(text).not.toMatch(/Back to home/);
  });

  it('SSR pass produces translated markup without IntlProvider (AC-1/AC-2)', () => {
    // Pre-fix: calling `renderToString(<NotFound />)` outside an
    // IntlProvider made `useTranslation()` return raw message-id keys.
    // Post-fix: NotFoundContent is a server component reading static IT
    // messages, so the SSR string already contains the translated copy.
    const ssrMarkup = renderToString(<NotFound />);

    expect(ssrMarkup).toContain('Pagina non trovata');
    expect(ssrMarkup).toContain('La pagina che stai cercando non esiste');
    expect(ssrMarkup).toContain('Torna alla home');
    expect(ssrMarkup).toContain('Esplora i giochi');
    expect(ssrMarkup).not.toMatch(/pages\.errors\.notFound\./);
  });

  it('SSR-rendered text matches client-rendered text (AC-3 — no React #418 surface)', () => {
    // Equivalence check: the strings produced by `renderToString` and by
    // `render` must agree on every translated label. If they diverge,
    // React would emit error #418 during hydration in a real browser.
    const ssrMarkup = renderToString(<NotFound />);
    const { container } = render(<NotFound />);
    const clientText = container.textContent ?? '';

    const labels = [
      '404',
      'Pagina non trovata',
      'La pagina che stai cercando non esiste o è stata spostata.',
      'Torna alla home',
      'Esplora i giochi',
    ];

    for (const label of labels) {
      expect(ssrMarkup, `SSR markup missing "${label}"`).toContain(label);
      expect(clientText, `Client DOM missing "${label}"`).toContain(label);
    }
  });
});
