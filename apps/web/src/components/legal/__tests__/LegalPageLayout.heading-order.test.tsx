/**
 * @vitest-environment jsdom
 */

import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, expect, it, vi } from 'vitest';

import { LegalPageLayout } from '../LegalPageLayout';

// #4916. The accordion of a legal page is its list of top-level sections, right after the h1. Radix renders each
// header as an h3, so /privacy, /terms and /cookies skipped a level (axe: «Heading order invalid»).

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it', formatDate: () => '' }),
}));
vi.mock('@/components/legal/LegalLocaleToggle', () => ({
  LegalLocaleProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  LegalLocaleToggle: () => null,
  useLegalLocale: () => ({ locale: 'it', setLocale: vi.fn(), toggleLocale: vi.fn() }),
}));
vi.mock('@/components/legal/StructuredData', () => ({
  StructuredData: () => null,
  legalPageSchema: () => ({}),
  breadcrumbSchema: () => ({}),
}));
vi.mock('@/components/legal/LegalMarkdown', () => ({
  LegalMarkdown: () => null,
}));

function renderPage() {
  return render(
    <LegalPageLayout
      pageKey="privacy"
      sections={['introduction', 'dataCollected', 'yourRights']}
      defaultOpenSection="introduction"
      lastUpdated={new Date('2026-01-01')}
    />
  );
}

describe('LegalPageLayout — heading order', () => {
  it('lists its sections as h2, right under the h1 of the page', () => {
    renderPage();

    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument();
    const sectionHeadings = screen.getAllByRole('heading', { level: 2 });
    expect(sectionHeadings).toHaveLength(3);
    expect(screen.queryByRole('heading', { level: 3 })).not.toBeInTheDocument();
  });

  it('passes the axe heading-order rule', async () => {
    const { container } = renderPage();

    const results = await axe(container, { runOnly: { type: 'rule', values: ['heading-order'] } });

    expect(results).toHaveNoViolations();
  });
});
