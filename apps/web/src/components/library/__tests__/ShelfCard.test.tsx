/**
 * #5072: ShelfCard puts its cover into CSS through cssImageUrl. A BGG cover (#2123, ADR-059 §5) is treated as no
 * cover: the gradient and the cover icon. jsdom drops an invalid CSS value as a browser does, so the check compares
 * against the render with no image, not "no BGG string in the DOM".
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ShelfCard } from '../ShelfCard';

const BGG = 'https://cf.geekdo-images.com/abc/pic1.jpg';
const ALLOWED = 'https://covers.meepleai.app/abc/cover.webp';

function coverArea(imageUrl: string | undefined): { html: string; unmount: () => void } {
  const { container, unmount } = render(
    <ShelfCard title="Wingspan" subtitle="Stonemaier" coverIcon="🐦" imageUrl={imageUrl} />
  );
  const cover = container.querySelector<HTMLElement>('[data-testid="shelf-card"] > div')!;
  return { html: cover.outerHTML, unmount };
}

describe('ShelfCard cover (#5072)', () => {
  it('treats a BGG cover as no cover: the gradient and the icon (#5072)', () => {
    const noCover = coverArea(undefined);
    expect(screen.getByText('🐦')).toBeInTheDocument();
    noCover.unmount();

    const bgg = coverArea(BGG);
    expect(bgg.html).toBe(noCover.html);
    expect(bgg.html).not.toContain('geekdo-images');
    bgg.unmount();
  });

  it('draws an allowed cover, without the icon (#5072)', () => {
    const allowed = coverArea(ALLOWED);

    expect(allowed.html).toContain('url(&quot;https://covers.meepleai.app/abc/cover.webp&quot;)');
    expect(screen.queryByText('🐦')).not.toBeInTheDocument();
    allowed.unmount();
  });
});
