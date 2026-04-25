import { act, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import CookiesPage from '../page';

// Mirror the test pattern used by existing locale-aware pages in this repo:
// useTranslation returns t(key) => key so assertions hit the key paths directly.
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (k: string) => k,
    locale: 'it',
    formatDate: () => '',
  }),
}));

// LegalLocaleProvider uses react-intl with localStorage; replace with a passthrough
// to keep the test focused on render output without intl plumbing.
vi.mock('@/components/legal/LegalLocaleToggle', () => ({
  LegalLocaleProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  LegalLocaleToggle: () => null,
  useLegalLocale: () => ({ locale: 'it', setLocale: vi.fn(), toggleLocale: vi.fn() }),
}));

// StructuredData injects JSON-LD into the document head; not relevant here.
vi.mock('@/components/legal/StructuredData', () => ({
  StructuredData: () => null,
  legalPageSchema: () => ({}),
  breadcrumbSchema: () => ({}),
}));

// LegalMarkdown content rendering is incidental to footer tests.
vi.mock('@/components/legal/LegalMarkdown', () => ({
  LegalMarkdown: () => null,
}));

describe('CookiesPage footer', () => {
  it('renders the manage-preferences link pointing to /cookie-settings', async () => {
    await act(async () => {
      render(<CookiesPage />);
    });

    const link = screen.getByRole('link', {
      name: 'pages.cookies.managePreferencesCta',
    });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/cookie-settings');
  });

  it('still renders the previous footer nav link to /terms', async () => {
    await act(async () => {
      render(<CookiesPage />);
    });

    // prevLink labelIt is "Termini e Condizioni" (rendered as raw string by layout)
    const prev = screen.getByRole('link', { name: /Termini e Condizioni/ });
    expect(prev).toBeInTheDocument();
    expect(prev).toHaveAttribute('href', '/terms');
  });
});
