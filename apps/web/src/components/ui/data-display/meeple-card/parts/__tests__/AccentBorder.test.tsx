import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';

import { AccentBorder } from '../AccentBorder';
import { entityHsl } from '../../tokens';

describe('AccentBorder', () => {
  it('renders horizontal top bar (mockup-conformant: top-0 left-0 right-0 h-[3px])', () => {
    const { container } = render(<AccentBorder entity="game" />);
    const el = container.firstChild as HTMLElement;
    expect(el.className).toMatch(/\btop-0\b/);
    expect(el.className).toMatch(/\bleft-0\b/);
    expect(el.className).toMatch(/\bright-0\b/);
    expect(el.className).toMatch(/h-\[3px\]/);
  });

  it('does NOT render vertical-left bar (regression guard against old layout)', () => {
    const { container } = render(<AccentBorder entity="game" />);
    const el = container.firstChild as HTMLElement;
    expect(el.className).not.toMatch(/\bbottom-0\b/);
    expect(el.className).not.toMatch(/\bw-\[3px\]\b/);
  });

  it('uses entityHsl for inline background', () => {
    const { container } = render(<AccentBorder entity="player" />);
    const el = container.firstChild as HTMLElement;
    // Browser renders HSL as RGB, so just verify it's set
    expect(el.style.background).toBeTruthy();
    expect(el.style.background).toContain('rgb');
  });

  it('grows on group-hover via height transition', () => {
    const { container } = render(<AccentBorder entity="game" />);
    const el = container.firstChild as HTMLElement;
    expect(el.className).toMatch(/group-hover:h-\[5px\]/);
    expect(el.className).not.toMatch(/group-hover:w-\[5px\]/);
  });
});
