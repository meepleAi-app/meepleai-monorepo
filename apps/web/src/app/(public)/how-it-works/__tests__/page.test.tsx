// apps/web/src/app/(public)/how-it-works/__tests__/page.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import HowItWorksPage from '../page';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it' }),
}));

describe('HowItWorksPage', () => {
  it('renders hero with a primary CTA to /register', () => {
    render(<HowItWorksPage />);
    const cta = screen.getByRole('link', { name: 'pages.howItWorks.ctaRegister' });
    expect(cta).toHaveAttribute('href', '/register');
  });

  it('renders 3 steps (step1, step2, step3)', () => {
    render(<HowItWorksPage />);
    expect(
      screen.getByRole('heading', { name: 'pages.howItWorks.step1.title' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'pages.howItWorks.step2.title' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'pages.howItWorks.step3.title' })
    ).toBeInTheDocument();
  });

  it('renders 4 feature cards (rag, multilingual, pdfUpload, gameLibrary)', () => {
    render(<HowItWorksPage />);
    expect(
      screen.getByRole('heading', { name: 'pages.howItWorks.features.rag.title' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'pages.howItWorks.features.multilingual.title' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'pages.howItWorks.features.pdfUpload.title' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'pages.howItWorks.features.gameLibrary.title' })
    ).toBeInTheDocument();
  });

  it('footer CTAs link to /about and /faq', () => {
    render(<HowItWorksPage />);
    expect(screen.getByRole('link', { name: 'pages.howItWorks.aboutCta' })).toHaveAttribute(
      'href',
      '/about'
    );
    expect(screen.getByRole('link', { name: 'pages.howItWorks.faqCta' })).toHaveAttribute(
      'href',
      '/faq'
    );
  });
});
