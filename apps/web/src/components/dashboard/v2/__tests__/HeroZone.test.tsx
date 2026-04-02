import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import { HeroZone } from '../HeroZone';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

describe('HeroZone', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders greeting with user name when no session', () => {
    render(<HeroZone userName="Marco" />);

    const heroZone = screen.getByTestId('hero-zone');
    expect(heroZone).toBeInTheDocument();

    // Greeting text should contain the user name
    expect(heroZone).toHaveTextContent('Marco');
    // Should contain one of the Italian greetings
    const text = heroZone.textContent ?? '';
    const hasGreeting =
      text.includes('Buongiorno') || text.includes('Buon pomeriggio') || text.includes('Buonasera');
    expect(hasGreeting).toBe(true);
  });

  it('renders active session banner with game name and Riprendi button', () => {
    const activeSession = {
      gameName: 'Catan',
      duration: '1h 23m',
      sessionId: 'session-abc',
    };

    render(<HeroZone userName="Marco" activeSession={activeSession} />);

    const heroZone = screen.getByTestId('hero-zone');
    expect(heroZone).toBeInTheDocument();
    expect(heroZone).toHaveTextContent('Hai una partita in corso: Catan');

    const button = screen.getByRole('button', { name: 'Riprendi' });
    expect(button).toBeInTheDocument();
  });

  it('shows date element with data-testid="hero-date"', () => {
    render(<HeroZone userName="Marco" />);

    const dateEl = screen.getByTestId('hero-date');
    expect(dateEl).toBeInTheDocument();
    expect(dateEl.textContent).not.toBe('');
  });
});
