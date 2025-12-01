/**
 * HowItWorksSection Tests
 *
 * Unit tests for the three-step process section.
 */

import { render, screen } from '@testing-library/react';
import { HowItWorksSection } from '../HowItWorksSection';

describe('HowItWorksSection', () => {
  it('renders section heading', () => {
    render(<HowItWorksSection />);

    expect(screen.getByRole('heading', { name: /come funziona/i })).toBeInTheDocument();
  });

  it('renders all three steps with correct numbers', () => {
    render(<HowItWorksSection />);

    expect(screen.getByText('1')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('renders step titles', () => {
    render(<HowItWorksSection />);

    expect(screen.getByText('Scegli il gioco')).toBeInTheDocument();
    expect(screen.getByText('Fai una domanda')).toBeInTheDocument();
    expect(screen.getByText('Ottieni risposta citata')).toBeInTheDocument();
  });

  it('renders step descriptions', () => {
    render(<HowItWorksSection />);

    expect(screen.getByText(/cerca tra migliaia di giochi/i)).toBeInTheDocument();
    expect(screen.getByText(/chiedi in linguaggio naturale/i)).toBeInTheDocument();
    expect(screen.getByText(/risposte accurate con riferimenti/i)).toBeInTheDocument();
  });

  it('renders step emojis with aria labels', () => {
    const { container } = render(<HowItWorksSection />);

    const emojis = container.querySelectorAll('[role="img"]');
    expect(emojis.length).toBe(3);
  });

  it('applies responsive grid layout', () => {
    const { container } = render(<HowItWorksSection />);

    const grid = container.querySelector('.grid');
    expect(grid).toHaveClass('grid-cols-1');
  });

  it('renders arrow connectors between steps', () => {
    const { container } = render(<HowItWorksSection />);

    // Arrows should exist in DOM (check for elements with hidden class)
    const arrows = container.querySelectorAll('.hidden');
    expect(arrows.length).toBeGreaterThan(0);
  });
});
