import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { DiscoverCarousel } from '../DiscoverCarousel';

describe('DiscoverCarousel', () => {
  const originalScrollBy = Element.prototype.scrollBy;
  const originalScrollTo = Element.prototype.scrollTo;
  const originalScrollWidthDescriptor = Object.getOwnPropertyDescriptor(
    HTMLElement.prototype,
    'scrollWidth'
  );
  const originalClientWidthDescriptor = Object.getOwnPropertyDescriptor(
    HTMLElement.prototype,
    'clientWidth'
  );

  beforeEach(() => {
    Element.prototype.scrollBy = vi.fn();
    Element.prototype.scrollTo = vi.fn();
    Object.defineProperty(HTMLElement.prototype, 'scrollWidth', {
      configurable: true,
      get: () => 1000,
    });
    Object.defineProperty(HTMLElement.prototype, 'clientWidth', {
      configurable: true,
      get: () => 400,
    });
  });

  afterEach(() => {
    // Restore prototype mutations to avoid leakage across test files in the same worker.
    Element.prototype.scrollBy = originalScrollBy;
    Element.prototype.scrollTo = originalScrollTo;
    if (originalScrollWidthDescriptor) {
      Object.defineProperty(HTMLElement.prototype, 'scrollWidth', originalScrollWidthDescriptor);
    } else {
      delete (HTMLElement.prototype as { scrollWidth?: number }).scrollWidth;
    }
    if (originalClientWidthDescriptor) {
      Object.defineProperty(HTMLElement.prototype, 'clientWidth', originalClientWidthDescriptor);
    } else {
      delete (HTMLElement.prototype as { clientWidth?: number }).clientWidth;
    }
  });

  it('renders children in a role=region with ariaLabel', () => {
    render(
      <DiscoverCarousel ariaLabel="Test carousel">
        <div data-testid="child-1">A</div>
        <div data-testid="child-2">B</div>
      </DiscoverCarousel>
    );
    const region = screen.getByRole('region', { name: 'Test carousel' });
    expect(region).toBeInTheDocument();
    expect(screen.getByTestId('child-1')).toBeInTheDocument();
    expect(screen.getByTestId('child-2')).toBeInTheDocument();
  });

  it('track is focusable (tabIndex=0)', () => {
    render(
      <DiscoverCarousel ariaLabel="x">
        <div>A</div>
      </DiscoverCarousel>
    );
    const track = screen.getByTestId('discover-track');
    expect(track).toHaveAttribute('tabindex', '0');
  });

  it('arrow buttons exist and have aria-labels', () => {
    render(
      <DiscoverCarousel ariaLabel="x">
        <div>A</div>
      </DiscoverCarousel>
    );
    expect(screen.getByRole('button', { name: /Scorri verso sinistra/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Scorri verso destra/i })).toBeInTheDocument();
  });

  it('arrows have aria-controls pointing to track id', () => {
    render(
      <DiscoverCarousel ariaLabel="x">
        <div>A</div>
      </DiscoverCarousel>
    );
    const track = screen.getByTestId('discover-track');
    const trackId = track.id;
    expect(trackId).toBeTruthy();
    const leftArrow = screen.getByRole('button', { name: /sinistra/i });
    const rightArrow = screen.getByRole('button', { name: /destra/i });
    expect(leftArrow).toHaveAttribute('aria-controls', trackId);
    expect(rightArrow).toHaveAttribute('aria-controls', trackId);
  });

  it('right arrow click calls scrollBy on track', () => {
    render(
      <DiscoverCarousel ariaLabel="x" itemWidth={260} gap={12}>
        <div>A</div>
        <div>B</div>
      </DiscoverCarousel>
    );
    const rightArrow = screen.getByRole('button', { name: /destra/i });
    fireEvent.click(rightArrow);
    expect(Element.prototype.scrollBy).toHaveBeenCalledWith({
      left: 272,
      behavior: 'smooth',
    });
  });

  it('left arrow click calls scrollBy with negative offset', () => {
    render(
      <DiscoverCarousel ariaLabel="x" itemWidth={260} gap={12}>
        <div>A</div>
      </DiscoverCarousel>
    );
    const leftArrow = screen.getByRole('button', { name: /sinistra/i });
    fireEvent.click(leftArrow);
    expect(Element.prototype.scrollBy).toHaveBeenCalledWith({
      left: -272,
      behavior: 'smooth',
    });
  });

  it('ArrowRight keydown on track scrolls right', () => {
    render(
      <DiscoverCarousel ariaLabel="x" itemWidth={200} gap={8}>
        <div>A</div>
      </DiscoverCarousel>
    );
    const track = screen.getByTestId('discover-track');
    fireEvent.keyDown(track, { key: 'ArrowRight' });
    expect(Element.prototype.scrollBy).toHaveBeenCalledWith({
      left: 208,
      behavior: 'smooth',
    });
  });

  it('ArrowLeft keydown on track scrolls left', () => {
    render(
      <DiscoverCarousel ariaLabel="x" itemWidth={200} gap={8}>
        <div>A</div>
      </DiscoverCarousel>
    );
    const track = screen.getByTestId('discover-track');
    fireEvent.keyDown(track, { key: 'ArrowLeft' });
    expect(Element.prototype.scrollBy).toHaveBeenCalledWith({
      left: -208,
      behavior: 'smooth',
    });
  });

  it('Home key scrolls to start, End key scrolls to end', () => {
    render(
      <DiscoverCarousel ariaLabel="x">
        <div>A</div>
      </DiscoverCarousel>
    );
    const track = screen.getByTestId('discover-track');
    fireEvent.keyDown(track, { key: 'Home' });
    expect(Element.prototype.scrollTo).toHaveBeenCalledWith({ left: 0, behavior: 'smooth' });
    fireEvent.keyDown(track, { key: 'End' });
    expect(Element.prototype.scrollTo).toHaveBeenCalledWith({ left: 1000, behavior: 'smooth' });
  });

  it('does NOT inject role=list or role=listitem (preserves consumer semantics)', () => {
    render(
      <DiscoverCarousel ariaLabel="x">
        <div data-testid="c1">A</div>
      </DiscoverCarousel>
    );
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
    expect(screen.queryByRole('listitem')).not.toBeInTheDocument();
  });

  it('renders fade gradients on both sides', () => {
    render(
      <DiscoverCarousel ariaLabel="x">
        <div>A</div>
      </DiscoverCarousel>
    );
    expect(screen.getByTestId('discover-fade-left')).toBeInTheDocument();
    expect(screen.getByTestId('discover-fade-right')).toBeInTheDocument();
  });

  it('wrapper carries the "group" Tailwind class (required for hover-revealed arrows)', () => {
    const { container } = render(
      <DiscoverCarousel ariaLabel="x">
        <div>A</div>
      </DiscoverCarousel>
    );
    const wrapper = container.querySelector('.discover-carousel');
    expect(wrapper).toBeInTheDocument();
    expect(wrapper?.classList.contains('group')).toBe(true);
  });
});
