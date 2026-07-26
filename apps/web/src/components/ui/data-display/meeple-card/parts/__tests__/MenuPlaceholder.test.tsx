import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@testing-library/react';

import { MenuPlaceholder } from '../MenuPlaceholder';

// MenuPlaceholder is a decorative-only, aria-hidden placeholder (#3289), so it
// is absent from the accessibility tree — getByRole/name queries cannot reach it
// (aria-hidden also blanks the computed accessible name, so `hidden: true` alone
// does not help). Query it via the DOM by its stable aria-label instead.
function renderMenu() {
  const result = render(<MenuPlaceholder />);
  const btn = result.container.querySelector<HTMLButtonElement>('button[aria-label="Azioni"]');
  return { ...result, btn };
}

describe('MenuPlaceholder', () => {
  it('renders an aria-hidden button with aria-label="Azioni"', () => {
    const { btn } = renderMenu();
    expect(btn).not.toBeNull();
    expect(btn).toHaveAttribute('aria-hidden', 'true');
  });

  it('renders the ⋯ glyph', () => {
    const { btn } = renderMenu();
    expect(btn?.textContent).toContain('⋯');
  });

  it('starts hidden (opacity-0) and becomes visible on parent group-hover', () => {
    const { btn } = renderMenu();
    expect(btn?.className).toMatch(/\bopacity-0\b/);
    expect(btn?.className).toMatch(/group-hover:opacity-100/);
    expect(btn?.className).toMatch(/transition-opacity/);
  });

  it('positions absolute top-2 right-2 with glass style', () => {
    const { btn } = renderMenu();
    expect(btn?.className).toMatch(/\babsolute\b/);
    expect(btn?.className).toMatch(/\btop-2\b/);
    expect(btn?.className).toMatch(/\bright-2\b/);
    expect(btn?.className).toMatch(/bg-white\/85/);
    expect(btn?.className).toMatch(/backdrop-blur-md/);
  });

  it('stops click event propagation (prevents triggering parent card onClick)', () => {
    const parentClick = vi.fn();
    const { container } = render(
      <div onClick={parentClick}>
        <MenuPlaceholder />
      </div>
    );
    const btn = container.querySelector<HTMLButtonElement>('button[aria-label="Azioni"]');
    fireEvent.click(btn!);
    expect(parentClick).not.toHaveBeenCalled();
  });

  it('is removed from the keyboard tab order (tabIndex={-1}) — no false a11y affordance', () => {
    const { btn } = renderMenu();
    // Without tabIndex={-1}, screen-reader / keyboard users would land on this
    // non-functional placeholder on every one of the 72 consumer card surfaces.
    // DEC-4: placeholder is visual-only; restore tabIndex={0} when a real
    // onActionsMenu handler is wired in a follow-up issue.
    expect(btn?.tabIndex).toBe(-1);
  });
});
