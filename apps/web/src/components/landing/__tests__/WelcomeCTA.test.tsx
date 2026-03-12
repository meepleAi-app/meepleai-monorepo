import { render, screen } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import { WelcomeCTA } from '../WelcomeCTA';

expect.extend(toHaveNoViolations);

describe('WelcomeCTA', () => {
  it('renders heading', () => {
    render(<WelcomeCTA />);
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent(
      'Pronto per la prossima serata giochi?'
    );
  });

  it('renders primary CTA linking to /register', () => {
    render(<WelcomeCTA />);
    const cta = screen.getByRole('link', { name: /inizia gratis/i });
    expect(cta).toHaveAttribute('href', '/register');
  });

  it('renders secondary CTA linking to /games', () => {
    render(<WelcomeCTA />);
    const cta = screen.getByRole('link', { name: /esplora il catalogo/i });
    expect(cta).toHaveAttribute('href', '/games');
  });

  it('has no accessibility violations', async () => {
    const { container } = render(<WelcomeCTA />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
