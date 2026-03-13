import { render, screen } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import { WelcomeHero } from '../WelcomeHero';

expect.extend(toHaveNoViolations);

describe('WelcomeHero', () => {
  it('renders heading', () => {
    render(<WelcomeHero />);
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(
      'Ogni serata giochi merita un arbitro'
    );
  });

  it('renders kicker text', () => {
    render(<WelcomeHero />);
    expect(screen.getByText('Il tuo compagno di gioco AI')).toBeInTheDocument();
  });

  it('renders primary CTA linking to /register', () => {
    render(<WelcomeHero />);
    const cta = screen.getByRole('link', { name: /inizia gratis/i });
    expect(cta).toHaveAttribute('href', '/register');
  });

  it('renders secondary CTA as anchor to #come-funziona', () => {
    render(<WelcomeHero />);
    const cta = screen.getByRole('link', { name: /scopri come funziona/i });
    expect(cta).toHaveAttribute('href', '#come-funziona');
  });

  it('has no accessibility violations', async () => {
    const { container } = render(<WelcomeHero />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
