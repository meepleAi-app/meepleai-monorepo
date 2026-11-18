import { render } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ENTITY_COLORS, EntityHeader } from '../shared';

const BGG = 'https://cf.geekdo-images.com/abc/pic1.jpg';
const ALLOWED = 'https://covers.meepleai.app/abc/cover.webp';

describe('EntityHeader', () => {
  // #5072: a BGG image (#2123, ADR-059 §5) is treated as no image: the plain colour block.
  it('treats a BGG image as no image, and draws an allowed one (#5072)', () => {
    const backdrop = (imageUrl: string | undefined) => {
      const { container, unmount } = render(
        <EntityHeader title="Azul" imageUrl={imageUrl} color={ENTITY_COLORS.game.hsl} />
      );
      const html = container.innerHTML;
      const style = container.firstElementChild!.firstElementChild!.getAttribute('style');
      unmount();
      return { html, style };
    };

    const noImage = backdrop(undefined);
    const bgg = backdrop(BGG);
    expect(noImage.style).toMatch(/^background: /);
    expect(bgg.style).toBe(noImage.style);
    expect(bgg.html).not.toContain('geekdo-images');
    expect(backdrop(ALLOWED).style).toContain(`url("${ALLOWED}")`);
  });
});
