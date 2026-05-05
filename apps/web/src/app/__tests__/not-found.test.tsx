import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import NotFound from '../not-found';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it' }),
}));

describe('NotFound', () => {
  it('renders the localized 404 title, subtitle, and both CTAs', () => {
    render(<NotFound />);

    expect(screen.getByText('404')).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: /pages\.errors\.notFound\.title/ })
    ).toBeInTheDocument();
    expect(screen.getByText('pages.errors.notFound.subtitle')).toBeInTheDocument();

    const homeCta = screen.getByRole('link', { name: 'pages.errors.notFound.homeCta' });
    const exploreCta = screen.getByRole('link', { name: 'pages.errors.notFound.exploreCta' });
    expect(homeCta).toHaveAttribute('href', '/');
    expect(exploreCta).toHaveAttribute('href', '/games');
  });

  it('contains zero hardcoded English strings in rendered output', () => {
    const { container } = render(<NotFound />);
    const text = container.textContent ?? '';
    expect(text).not.toMatch(/Page not found/);
    expect(text).not.toMatch(/Back to home/);
  });
});
