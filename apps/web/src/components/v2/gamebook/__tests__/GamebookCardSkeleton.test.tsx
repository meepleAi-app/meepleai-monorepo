/**
 * GamebookCardSkeleton unit tests — SP6 Phase B Task 2 (Issue #788).
 *
 * Coverage:
 *   - data-slot identity
 *   - aria-hidden="true" (decorative)
 *   - shimmer animation classes present (motion-safe gating)
 *   - dimensions match GamebookCard (16:9 cover)
 *   - custom className composition
 *
 * Reduced motion: Tailwind's `motion-safe:` variant collapses the shimmer to
 * a static `bg-muted` under `prefers-reduced-motion: reduce`. We verify the
 * `motion-safe:animate-pulse` class is present (Tailwind handles gating).
 */

import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { GamebookCardSkeleton } from '../GamebookCardSkeleton';

describe('GamebookCardSkeleton', () => {
  it('renders data-slot="gamebook-card-skeleton"', () => {
    render(<GamebookCardSkeleton />);
    expect(document.querySelector('[data-slot="gamebook-card-skeleton"]')).not.toBeNull();
  });

  it('outer container is decorative (aria-hidden="true")', () => {
    render(<GamebookCardSkeleton />);
    const el = document.querySelector('[data-slot="gamebook-card-skeleton"]');
    expect(el!.getAttribute('aria-hidden')).toBe('true');
  });

  it('renders 16:9 aspect cover placeholder', () => {
    render(<GamebookCardSkeleton />);
    const el = document.querySelector('[data-slot="gamebook-card-skeleton"]');
    const cover = el!.querySelector('.aspect-\\[16\\/9\\]');
    expect(cover).not.toBeNull();
  });

  it('shimmer placeholders use motion-safe:animate-pulse (reduced motion honored)', () => {
    render(<GamebookCardSkeleton />);
    const shimmers = document.querySelectorAll('.motion-safe\\:animate-pulse');
    // 4 placeholders: cover, title, meta, status pill
    expect(shimmers.length).toBeGreaterThanOrEqual(4);
  });

  it('all shimmer placeholders are decorative (aria-hidden)', () => {
    render(<GamebookCardSkeleton />);
    const shimmers = document.querySelectorAll('.motion-safe\\:animate-pulse');
    shimmers.forEach(s => {
      expect(s.getAttribute('aria-hidden')).toBe('true');
    });
  });

  it('applies custom className to outer container', () => {
    render(<GamebookCardSkeleton className="my-extra-class" />);
    const el = document.querySelector('[data-slot="gamebook-card-skeleton"]');
    expect(el!.classList.contains('my-extra-class')).toBe(true);
  });
});
