import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { Cover } from '../Cover';
import { entityIcon } from '../../tokens';

vi.mock('@/lib/games/cover-utils', () => ({
  shouldUsePlaceholder: (url?: string) => !url || url.includes('boardgamegeek'),
  hashToHue: () => 100,
  extractInitials: (t: string) => t.charAt(0) || '?',
}));

describe('Cover dual-mode (#1856 DEC-3)', () => {
  describe('image-mode (imageUrl present)', () => {
    it('renders <img> with aspect-[7/10] when imageUrl is a non-BGG URL', () => {
      const { container } = render(
        <Cover
          entity="game"
          variant="grid"
          imageUrl="https://cdn.example.com/catan.webp"
          alt="Catan"
        />
      );
      const img = container.querySelector('img');
      expect(img).not.toBeNull();
      expect(img?.getAttribute('src')).toBe('https://cdn.example.com/catan.webp');
      const wrapper = container.firstChild as HTMLElement;
      expect(wrapper.className).toMatch(/aspect-\[7\/10\]/);
    });

    it('switches to emoji-band on <img> onError', () => {
      const { container } = render(
        <Cover
          entity="game"
          variant="grid"
          imageUrl="https://cdn.example.com/broken.webp"
          alt="X"
        />
      );
      const img = container.querySelector('img') as HTMLImageElement;
      fireEvent.error(img);
      // After error, <img> is replaced by emoji-band fallback.
      expect(container.querySelector('img')).toBeNull();
      expect(screen.getByText(entityIcon.game)).toBeInTheDocument();
    });

    it('resets hasImgError state when imageUrl prop changes (recovery from broken URL)', () => {
      const { container, rerender } = render(
        <Cover
          entity="game"
          variant="grid"
          imageUrl="https://cdn.example.com/broken.webp"
          alt="X"
        />
      );
      // Trigger onError → state flips to error, emoji-band shown.
      const img = container.querySelector('img') as HTMLImageElement;
      fireEvent.error(img);
      expect(container.querySelector('img')).toBeNull();

      // Re-render with a new (valid) imageUrl — state should reset, <img> rendered again.
      rerender(
        <Cover
          entity="game"
          variant="grid"
          imageUrl="https://cdn.example.com/new-good.webp"
          alt="X"
        />
      );
      expect(container.querySelector('img')).not.toBeNull();
      expect(container.querySelector('img')?.getAttribute('src')).toBe(
        'https://cdn.example.com/new-good.webp'
      );
    });
  });

  describe('emoji-band-mode (imageUrl absent or BGG-blocked)', () => {
    it('renders squat band with h-[100px] when imageUrl is undefined', () => {
      const { container } = render(<Cover entity="session" variant="grid" />);
      // Outer wrapper still uses variant aspect ratio (grid → aspect-[7/10]).
      // Inner emoji-band uses h-[100px]:
      const band = container.querySelector('[data-slot="cover-emoji-band"]') as HTMLElement;
      expect(band).not.toBeNull();
      expect(band.className).toMatch(/h-\[100px\]/);
    });

    it('renders coverEmoji prop when provided', () => {
      render(<Cover entity="session" variant="grid" coverEmoji="🎲" />);
      expect(screen.getByText('🎲')).toBeInTheDocument();
    });

    it('falls back to entityIcon[entity] when coverEmoji is omitted', () => {
      render(<Cover entity="agent" variant="grid" />);
      expect(screen.getByText(entityIcon.agent)).toBeInTheDocument();
    });

    it('emoji is rendered at 38px (text-[38px])', () => {
      render(<Cover entity="session" variant="grid" />);
      const emoji = screen.getByText(entityIcon.session);
      expect(emoji.className).toMatch(/text-\[38px\]/);
    });

    it('renders emoji-band for entity=game (DEC-2: no GameCoverPlaceholder fallback)', () => {
      const { container } = render(<Cover entity="game" variant="grid" gameId="g1" alt="Catan" />);
      const placeholder = container.querySelector('[data-testid="game-cover-placeholder"]');
      expect(placeholder).toBeNull();
      const band = container.querySelector('[data-slot="cover-emoji-band"]');
      expect(band).not.toBeNull();
      expect(screen.getByText(entityIcon.game)).toBeInTheDocument();
    });

    it('renders emoji-band when imageUrl is BGG-blocked', () => {
      const { container } = render(
        <Cover entity="game" variant="grid" imageUrl="https://boardgamegeek.com/x.jpg" alt="X" />
      );
      expect(container.querySelector('img')).toBeNull();
      const band = container.querySelector('[data-slot="cover-emoji-band"]');
      expect(band).not.toBeNull();
    });
  });

  describe('CoverProps backwards compat', () => {
    it('accepts the legacy props (gameId, alt) without throwing', () => {
      expect(() =>
        render(<Cover entity="game" variant="grid" gameId="g1" alt="Catan" coverEmoji="🎲" />)
      ).not.toThrow();
    });
  });
});
