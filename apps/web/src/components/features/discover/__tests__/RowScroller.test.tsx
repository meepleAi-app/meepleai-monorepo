/**
 * Issue #1483 — RowScroller unit tests.
 *
 * Horizontal scroll region with keyboard arrow navigation and hover affordance buttons.
 * The scroll region uses role="region" with an ariaLabel. Tab-focusable (tabIndex=0).
 * Left/right hover buttons exist but are tabIndex=-1 (visual-only affordance).
 * Keyboard: ArrowLeft/ArrowRight scroll by 1.5×cardWidth; Home/End scroll to extremes.
 *
 * Contract:
 *   - data-slot="row-scroller" on the scroll region div (role="region")
 *   - ariaLabel is applied to the scroll region
 *   - children are rendered inside the scroll region
 *   - Left arrow button has aria-label="Scorri a sinistra" and tabIndex=-1
 *   - Right arrow button has aria-label="Scorri a destra" and tabIndex=-1
 *   - Both affordance buttons are initially disabled (no scroll offset in jsdom)
 *   - KeyDown ArrowRight calls scrollBy on the scroll div
 *   - KeyDown Home calls scrollTo({ left: 0 })
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { RowScroller } from '../RowScroller';

describe('RowScroller', () => {
  it('renders data-slot="row-scroller" with role="region"', () => {
    const { container } = render(
      <RowScroller ariaLabel="Giochi recenti">
        <div>card</div>
      </RowScroller>
    );
    const region = container.querySelector('[data-slot="row-scroller"]');
    expect(region).not.toBeNull();
    expect(region).toHaveAttribute('role', 'region');
  });

  it('applies ariaLabel to the scroll region', () => {
    render(
      <RowScroller ariaLabel="Top Giochi">
        <div>card</div>
      </RowScroller>
    );
    expect(screen.getByRole('region', { name: 'Top Giochi' })).toBeInTheDocument();
  });

  it('renders children inside the scroll region', () => {
    render(
      <RowScroller ariaLabel="Test">
        <div data-testid="child-card">Card 1</div>
        <div data-testid="child-card-2">Card 2</div>
      </RowScroller>
    );
    expect(screen.getByTestId('child-card')).toBeInTheDocument();
    expect(screen.getByTestId('child-card-2')).toBeInTheDocument();
  });

  it('scroll region is keyboard-focusable (tabIndex=0)', () => {
    const { container } = render(
      <RowScroller ariaLabel="Giochi">
        <div>card</div>
      </RowScroller>
    );
    const region = container.querySelector('[data-slot="row-scroller"]');
    expect(region).toHaveAttribute('tabIndex', '0');
  });

  it('renders left affordance button with aria-label "Scorri a sinistra"', () => {
    render(
      <RowScroller ariaLabel="Test">
        <div>card</div>
      </RowScroller>
    );
    expect(screen.getByLabelText('Scorri a sinistra')).toBeInTheDocument();
  });

  it('renders right affordance button with aria-label "Scorri a destra"', () => {
    render(
      <RowScroller ariaLabel="Test">
        <div>card</div>
      </RowScroller>
    );
    expect(screen.getByLabelText('Scorri a destra')).toBeInTheDocument();
  });

  it('affordance buttons have tabIndex=-1 (visual-only, not keyboard-focusable)', () => {
    render(
      <RowScroller ariaLabel="Test">
        <div>card</div>
      </RowScroller>
    );
    expect(screen.getByLabelText('Scorri a sinistra')).toHaveAttribute('tabIndex', '-1');
    expect(screen.getByLabelText('Scorri a destra')).toHaveAttribute('tabIndex', '-1');
  });

  it('left affordance button is initially disabled (no scroll yet)', () => {
    render(
      <RowScroller ariaLabel="Test">
        <div>card</div>
      </RowScroller>
    );
    expect(screen.getByLabelText('Scorri a sinistra')).toBeDisabled();
  });

  it('right affordance button is initially disabled in jsdom (no scroll width)', () => {
    render(
      <RowScroller ariaLabel="Test">
        <div>card</div>
      </RowScroller>
    );
    // In jsdom, scrollWidth === clientWidth === 0, so canScrollRight is false
    expect(screen.getByLabelText('Scorri a destra')).toBeDisabled();
  });

  it('ArrowRight keydown calls scrollBy on the scroll region', () => {
    const { container } = render(
      <RowScroller ariaLabel="Test" cardWidth={260}>
        <div>card</div>
      </RowScroller>
    );
    const region = container.querySelector('[data-slot="row-scroller"]')!;
    // jsdom doesn't implement scrollBy — add it so vi.spyOn can intercept it
    if (!region.scrollBy) {
      (region as HTMLElement & { scrollBy: (opts: ScrollToOptions) => void }).scrollBy = vi.fn();
    }
    const scrollBySpy = vi.spyOn(region, 'scrollBy').mockImplementation(() => {});
    fireEvent.keyDown(region, { key: 'ArrowRight' });
    expect(scrollBySpy).toHaveBeenCalledWith(expect.objectContaining({ left: expect.any(Number) }));
    // Delta should be cardWidth * 1.5 = 390
    const call = scrollBySpy.mock.calls[0][0] as ScrollToOptions;
    expect(call.left).toBe(390);
  });

  it('ArrowLeft keydown calls scrollBy with negative delta', () => {
    const { container } = render(
      <RowScroller ariaLabel="Test" cardWidth={260}>
        <div>card</div>
      </RowScroller>
    );
    const region = container.querySelector('[data-slot="row-scroller"]')!;
    if (!region.scrollBy) {
      (region as HTMLElement & { scrollBy: (opts: ScrollToOptions) => void }).scrollBy = vi.fn();
    }
    const scrollBySpy = vi.spyOn(region, 'scrollBy').mockImplementation(() => {});
    fireEvent.keyDown(region, { key: 'ArrowLeft' });
    const call = scrollBySpy.mock.calls[0][0] as ScrollToOptions;
    expect(call.left).toBe(-390);
  });

  it('Home keydown calls scrollTo({ left: 0 })', () => {
    const { container } = render(
      <RowScroller ariaLabel="Test">
        <div>card</div>
      </RowScroller>
    );
    const region = container.querySelector('[data-slot="row-scroller"]')!;
    if (!region.scrollTo) {
      (region as HTMLElement & { scrollTo: (opts: ScrollToOptions) => void }).scrollTo = vi.fn();
    }
    const scrollToSpy = vi.spyOn(region, 'scrollTo').mockImplementation(() => {});
    fireEvent.keyDown(region, { key: 'Home' });
    expect(scrollToSpy).toHaveBeenCalledWith(expect.objectContaining({ left: 0 }));
  });

  it('End keydown calls scrollTo with scrollWidth', () => {
    const { container } = render(
      <RowScroller ariaLabel="Test">
        <div>card</div>
      </RowScroller>
    );
    const region = container.querySelector('[data-slot="row-scroller"]')!;
    if (!region.scrollTo) {
      (region as HTMLElement & { scrollTo: (opts: ScrollToOptions) => void }).scrollTo = vi.fn();
    }
    const scrollToSpy = vi.spyOn(region, 'scrollTo').mockImplementation(() => {});
    // jsdom scrollWidth defaults to 0
    Object.defineProperty(region, 'scrollWidth', { value: 1200, configurable: true });
    fireEvent.keyDown(region, { key: 'End' });
    expect(scrollToSpy).toHaveBeenCalledWith(expect.objectContaining({ left: 1200 }));
  });
});
