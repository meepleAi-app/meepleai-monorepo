/**
 * #5072 — SafeImg, the guarded <img>, and the same guard on AvatarImage.
 *
 * A URL that a browser would send to a BGG host (#2123, ADR-059 §5) never reaches the DOM, the SLO beacon fires,
 * and the fallback takes its place. Everything else passes unchanged, local previews included.
 */

import { createRef } from 'react';

import { render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/data-display/avatar';
import { SafeImg } from '@/components/ui/data-display/safe-img';

const reportBggAttempt = vi.hoisted(() => vi.fn());
vi.mock('@/lib/images/bgg-attempt-beacon', () => ({ reportBggAttempt }));

const BGG = 'https://cf.geekdo-images.com/abc/pic1.jpg';
const R2 = 'https://covers.meepleai.app/abc/cover.webp';

beforeEach(() => {
  reportBggAttempt.mockClear();
});

describe('SafeImg (#5072)', () => {
  it('renders an allowed URL unchanged, with its props', () => {
    render(<SafeImg src={R2} alt="Wingspan" className="h-8 w-8" loading="lazy" />);

    const img = screen.getByRole('img', { name: 'Wingspan' });
    expect(img).toHaveAttribute('src', R2);
    expect(img).toHaveAttribute('loading', 'lazy');
    expect(img).toHaveClass('h-8', 'w-8');
    expect(reportBggAttempt).not.toHaveBeenCalled();
  });

  it.each([
    BGG,
    'https://boardgamegeek.com/image/123',
    '//cf.geekdo-images.com/pic.jpg',
    '\\\\cf.geekdo-images.com/pic.jpg',
  ])('renders the fallback for %p, never an <img> with it, and fires the beacon', src => {
    const { container } = render(
      <SafeImg src={src} alt="Wingspan" fallback={<span data-testid="fallback">🎲</span>} />
    );

    expect(container.querySelector('img')).toBeNull();
    expect(screen.getByTestId('fallback')).toBeInTheDocument();
    expect(reportBggAttempt).toHaveBeenCalledTimes(1);
    expect(reportBggAttempt).toHaveBeenCalledWith(src);
  });

  it('renders nothing for a BGG URL without a fallback', () => {
    const { container } = render(<SafeImg src={BGG} alt="" />);

    expect(container).toBeEmptyDOMElement();
  });

  it.each([null, undefined, '', '   '])(
    'renders the fallback for a missing src %p, without a beacon',
    src => {
      render(<SafeImg src={src} alt="" fallback={<span data-testid="fallback" />} />);

      expect(screen.getByTestId('fallback')).toBeInTheDocument();
      expect(reportBggAttempt).not.toHaveBeenCalled();
    }
  );

  it.each([
    'blob:https://meepleai.app/3f1c2a9e-0000-4000-8000-000000000000',
    'data:image/png;base64,iVBORw0KGgo=',
    '/uploads/covers/abc.webp',
  ])('lets the local or same-origin src %p through', src => {
    render(<SafeImg src={src} alt="preview" />);

    expect(screen.getByRole('img', { name: 'preview' })).toHaveAttribute('src', src);
  });

  it('renders a BGG URL with allowBggHost, the admin exception (ADR-059 §5.6), without a beacon', () => {
    render(<SafeImg src={BGG} alt="Wingspan" allowBggHost />);

    expect(screen.getByRole('img', { name: 'Wingspan' })).toHaveAttribute('src', BGG);
    expect(reportBggAttempt).not.toHaveBeenCalled();
  });

  it('passes the ref to the <img>, as react-image-crop needs', () => {
    const ref = createRef<HTMLImageElement>();
    render(<SafeImg ref={ref} src={R2} alt="" />);

    expect(ref.current).toBeInstanceOf(HTMLImageElement);
  });
});

describe('AvatarImage (#5072)', () => {
  // Radix loads the image through `new window.Image()` before it renders the <img>: that is where a browser would
  // send the request, so the probe records every src assigned to an image element.
  let srcSetter: ReturnType<typeof vi.spyOn>;
  const requested = (): string[] => srcSetter.mock.calls.map(([value]) => String(value));

  beforeEach(() => {
    srcSetter = vi.spyOn(HTMLImageElement.prototype, 'src', 'set');
  });

  afterEach(() => {
    srcSetter.mockRestore();
  });

  const renderAvatar = (src: string) =>
    render(
      <Avatar>
        <AvatarImage src={src} alt="" />
        <AvatarFallback>AB</AvatarFallback>
      </Avatar>
    );

  it('never loads a BGG URL, shows the fallback and fires the beacon', () => {
    renderAvatar(BGG);

    expect(requested()).not.toContain(BGG);
    expect(screen.getByText('AB')).toBeInTheDocument();
    expect(reportBggAttempt).toHaveBeenCalledWith(BGG);
  });

  it('loads an allowed URL, so the probe above can see a request', () => {
    renderAvatar(R2);

    expect(requested()).toContain(R2);
    expect(reportBggAttempt).not.toHaveBeenCalled();
  });
});
