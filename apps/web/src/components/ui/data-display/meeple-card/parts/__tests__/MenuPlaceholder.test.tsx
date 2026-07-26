import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@testing-library/react';

import { MenuPlaceholder } from '../MenuPlaceholder';

// MenuPlaceholder is a decorative-only, aria-hidden <div> placeholder (#3289).
// It is deliberately NOT a <button>: a nested interactive control inside the
// card <button> trips axe's nested-interactive rule (aria-hidden does not exempt
// it). It is also absent from the a11y tree, so query it via the DOM by data-slot.
function renderMenu() {
  const result = render(<MenuPlaceholder />);
  const el = result.container.querySelector<HTMLElement>('[data-slot="menu-placeholder"]');
  return { ...result, el };
}

describe('MenuPlaceholder', () => {
  it('renders an aria-hidden, non-interactive placeholder (a <div>, not a <button>)', () => {
    const { el } = renderMenu();
    expect(el).not.toBeNull();
    expect(el).toHaveAttribute('aria-hidden', 'true');
    // Must NOT be a <button> — nesting one inside the card <button> violates
    // axe nested-interactive regardless of aria-hidden.
    expect(el?.tagName).toBe('DIV');
  });

  it('renders the ⋯ glyph', () => {
    const { el } = renderMenu();
    expect(el?.textContent).toContain('⋯');
  });

  it('starts hidden (opacity-0) and becomes visible on parent group-hover', () => {
    const { el } = renderMenu();
    expect(el?.className).toMatch(/\bopacity-0\b/);
    expect(el?.className).toMatch(/group-hover:opacity-100/);
    expect(el?.className).toMatch(/transition-opacity/);
  });

  it('positions absolute top-2 right-2 with glass style', () => {
    const { el } = renderMenu();
    expect(el?.className).toMatch(/\babsolute\b/);
    expect(el?.className).toMatch(/\btop-2\b/);
    expect(el?.className).toMatch(/\bright-2\b/);
    expect(el?.className).toMatch(/bg-white\/85/);
    expect(el?.className).toMatch(/backdrop-blur-md/);
  });

  it('stops click event propagation (prevents triggering parent card onClick)', () => {
    const parentClick = vi.fn();
    const { container } = render(
      <div onClick={parentClick}>
        <MenuPlaceholder />
      </div>
    );
    const el = container.querySelector<HTMLElement>('[data-slot="menu-placeholder"]');
    fireEvent.click(el!);
    expect(parentClick).not.toHaveBeenCalled();
  });

  it('is not a focusable/interactive control (no tab-order affordance, no role)', () => {
    const { el } = renderMenu();
    // A plain <div> is not focusable by default, so there is no false a11y
    // affordance for keyboard / screen-reader users. #1856 DEC-4 / #3289.
    expect(el?.tabIndex).toBe(-1);
    expect(el?.getAttribute('role')).toBeNull();
  });
});
