import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import FaqPage from '../page';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it' }),
}));

describe('FaqPage', () => {
  it('renders the hero with title and subtitle', () => {
    render(<FaqPage />);
    expect(screen.getByRole('heading', { name: 'pages.faq.title', level: 1 })).toBeInTheDocument();
    expect(screen.getByText('pages.faq.subtitle')).toBeInTheDocument();
  });

  it('renders 4 category sections (general, usage, technical, account)', () => {
    render(<FaqPage />);
    expect(
      screen.getByRole('heading', { name: /pages\.faq\.categories\.general/ })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: /pages\.faq\.categories\.usage/ })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: /pages\.faq\.categories\.technical/ })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: /pages\.faq\.categories\.account/ })
    ).toBeInTheDocument();
  });

  it('expands an accordion item on click to reveal the answer', async () => {
    const user = userEvent.setup();
    render(<FaqPage />);
    const firstTrigger = screen.getAllByRole('button', {
      name: /pages\.faq\.items\.general\.q1\.question/,
    })[0];
    await user.click(firstTrigger);
    expect(screen.getByText('pages.faq.items.general.q1.answer')).toBeVisible();
  });

  it('footer CTAs link to /contact and /how-it-works', () => {
    render(<FaqPage />);
    const contactLink = screen.getByRole('link', { name: 'pages.faq.contactCta' });
    const howLink = screen.getByRole('link', { name: 'pages.faq.howItWorksCta' });
    expect(contactLink).toHaveAttribute('href', '/contact');
    expect(howLink).toHaveAttribute('href', '/how-it-works');
  });
});
