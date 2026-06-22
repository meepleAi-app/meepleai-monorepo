/**
 * Confetti unit tests — Wave D.3 (Issue #756).
 */

import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { Confetti } from '../Confetti';

describe('Confetti', () => {
  it('renders data-slot="confetti" when active', () => {
    render(<Confetti active />);
    expect(document.querySelector('[data-slot="confetti"]')).not.toBeNull();
  });

  it('returns null when active=false', () => {
    const { container } = render(<Confetti active={false} />);
    expect(container.firstChild).toBeNull();
  });

  it('returns null when active prop omitted with explicit false', () => {
    const { container } = render(<Confetti active={false} />);
    expect(container.firstChild).toBeNull();
  });

  it('defaults to active=true (renders by default)', () => {
    render(<Confetti />);
    expect(document.querySelector('[data-slot="confetti"]')).not.toBeNull();
  });

  it('renders 24 confetti pieces matching mockup', () => {
    render(<Confetti active />);
    expect(document.querySelectorAll('[data-slot="confetti-piece"]').length).toBe(24);
  });

  it('marks confetti container as aria-hidden (decorative)', () => {
    render(<Confetti active />);
    const root = document.querySelector('[data-slot="confetti"]');
    expect(root!.getAttribute('aria-hidden')).toBe('true');
  });

  it('applies custom className when provided', () => {
    render(<Confetti active className="my-extra" />);
    const root = document.querySelector('[data-slot="confetti"]');
    expect(root!.classList.contains('my-extra')).toBe(true);
  });

  it('renders pieces with positioning style props', () => {
    render(<Confetti active />);
    const piece = document.querySelector('[data-slot="confetti-piece"]') as HTMLElement | null;
    expect(piece).not.toBeNull();
    // First piece (i=0) → left=0%, no animationDelay computed string
    expect(piece!.style.left).toBe('0%');
  });

  it('uses pointer-events-none for non-interactive overlay', () => {
    render(<Confetti active />);
    const root = document.querySelector('[data-slot="confetti"]');
    expect(root!.className).toMatch(/pointer-events-none/);
  });
});
