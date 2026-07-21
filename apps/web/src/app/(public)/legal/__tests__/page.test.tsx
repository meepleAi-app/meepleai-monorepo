/** @vitest-environment jsdom */
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';

import LegalHubPage, { metadata } from '../page';

// The page renders LegalPageLayout, which is a client component that supplies
// its own LegalLocaleProvider (+ IntlProvider) — no extra wrapper needed here.
describe('LegalHubPage', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('exposes SEO metadata (title + canonical)', () => {
    expect(metadata.title).toMatch(/legal/i);
    expect(metadata.alternates?.canonical).toBe('https://meepleai.com/legal');
  });

  it('renders the legal hub heading (default IT locale)', () => {
    render(<LegalHubPage />);
    // Page heading uses the pageKey testid from LegalPageLayout.
    expect(screen.getByTestId('hub-heading')).toBeInTheDocument();
  });

  it('links to all four legal documents', () => {
    render(<LegalHubPage />);
    const hrefs = screen.getAllByRole('link').map(a => a.getAttribute('href'));
    expect(hrefs).toContain('/terms');
    expect(hrefs).toContain('/privacy');
    expect(hrefs).toContain('/cookies');
    expect(hrefs).toContain('/legal/takedown');
  });

  it('is a real hub, not the old "Coming Soon" redirect placeholder', () => {
    render(<LegalHubPage />);
    expect(screen.queryByText(/coming soon/i)).toBeNull();
  });
});
