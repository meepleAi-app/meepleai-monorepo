/** @vitest-environment jsdom */
// apps/web/src/app/(public)/how-it-works/game-comprehension/__tests__/page.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import GameComprehensionPage from '../page';

// Mirror the sibling how-it-works test: return the key as the string. The
// second arg (interpolation values) is accepted but ignored so keys with
// placeholders (e.g. demoPageLabel) still resolve to a stable string.
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it' }),
}));

describe('GameComprehensionPage', () => {
  it('renders a single h1', () => {
    render(<GameComprehensionPage />);
    const h1 = screen.getByRole('heading', { level: 1 });
    expect(h1).toHaveTextContent('pages.gameComprehension.title');
  });

  it('renders the 4 trust-chain steps in order', () => {
    render(<GameComprehensionPage />);
    for (const key of ['pdf', 'read', 'review', 'card']) {
      expect(
        screen.getByRole('heading', { name: `pages.gameComprehension.chain.${key}.title` })
      ).toBeInTheDocument();
    }
    // The steps are an ordered list (semantic sequence).
    const items = screen.getByRole('list').querySelectorAll('li');
    expect(items).toHaveLength(4);
  });

  it('renders the live citation demo badge (interactive [p.N])', () => {
    render(<GameComprehensionPage />);
    // MechanicCitationBadge renders a button with an Italian aria-label + p.7.
    const badge = screen.getByRole('button', { name: /Citazione regolamento, pagina 7/ });
    expect(badge).toHaveTextContent('p.7');
  });

  it('opens the illustrative citation panel when the badge is activated', () => {
    render(<GameComprehensionPage />);
    // testid: the illustrative demo panel is a plain container with no ARIA
    // role/name (it is not a modal dialog), so getByRole cannot target it.
    expect(screen.queryByTestId('game-comprehension-demo-panel')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Citazione regolamento, pagina 7/ }));
    expect(screen.getByTestId('game-comprehension-demo-panel')).toBeInTheDocument();
  });

  it('renders the primary CTA to /register', () => {
    render(<GameComprehensionPage />);
    const cta = screen.getByRole('link', { name: 'pages.gameComprehension.ctaPrimary' });
    expect(cta).toHaveAttribute('href', '/register');
  });

  it('renders the secondary CTA back to /how-it-works', () => {
    render(<GameComprehensionPage />);
    const cta = screen.getByRole('link', { name: 'pages.gameComprehension.ctaSecondary' });
    expect(cta).toHaveAttribute('href', '/how-it-works');
  });

  it('emits a LearningResource JSON-LD structured-data script', () => {
    const { container } = render(<GameComprehensionPage />);
    const script = container.querySelector('script[type="application/ld+json"]');
    expect(script).toBeTruthy();
    expect(script?.textContent).toContain('LearningResource');
  });
});
