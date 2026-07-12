/** @vitest-environment jsdom */
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';

import TakedownPage, { metadata } from '../page';

// The page renders LegalPageLayout, which is a client component that supplies
// its own LegalLocaleProvider (+ IntlProvider) — no extra wrapper needed here.
describe('TakedownPage', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('exposes SEO metadata (title + canonical)', () => {
    expect(metadata.title).toMatch(/takedown/i);
    expect(metadata.alternates?.canonical).toBe('https://meepleai.com/legal/takedown');
  });

  it('renders the takedown heading and policy section titles (default IT locale)', () => {
    render(<TakedownPage />);

    // Page heading uses the pageKey testid from LegalPageLayout.
    expect(screen.getByTestId('takedown-heading')).toBeInTheDocument();

    // Default locale is IT — the first section title comes from legal.takedown.sections.overview.
    // Accordion header + table-of-contents link both render the title, so use getAllByText.
    expect(screen.getAllByText(/Panoramica/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Contatti/i).length).toBeGreaterThan(0);
  });

  it('mounts the takedown request form in the footer slot', () => {
    const { container } = render(<TakedownPage />);
    expect(container.querySelector('[data-slot="takedown-request-form"]')).not.toBeNull();
    // Direct mailto link is always present.
    const mailLink = screen.getByRole('link', { name: 'takedown@meepleai.app' });
    expect(mailLink).toHaveAttribute('href', 'mailto:takedown@meepleai.app');
  });

  it('links back to the terms page', () => {
    render(<TakedownPage />);
    // prevLink → /terms
    const termsLink = screen.getAllByRole('link').find(a => a.getAttribute('href') === '/terms');
    expect(termsLink).toBeTruthy();
  });
});
