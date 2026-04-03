import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

import { CreateGameCtaCard } from '@/components/library/PersonalLibraryPage';

describe('CreateGameCtaCard', () => {
  it('deve renderizzare un elemento <button> nativo (non div)', () => {
    render(<CreateGameCtaCard />);
    const cta = screen.getByTestId('create-game-cta');
    expect(cta.tagName).toBe('BUTTON');
  });

  it('deve avere aria-label descrittivo', () => {
    render(<CreateGameCtaCard />);
    expect(
      screen.getByRole('button', { name: /crea un gioco personalizzato/i })
    ).toBeInTheDocument();
  });
});
