/**
 * SessionsHero unit tests — Wave D.1 (Issue #735).
 *
 * 6 tests:
 * 1. Renders data-slot="sessions-hero"
 * 2. Renders title from labels
 * 3. Renders resolved subtitle string from labels
 * 4. Renders CTA button with correct aria-label
 * 5. Fires onNewSession on CTA click
 * 6. Applies custom className
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { SessionsHero } from '../SessionsHero';
import type { SessionsHeroProps } from '../SessionsHero';

const LABELS: SessionsHeroProps['labels'] = {
  title: 'Le tue partite',
  subtitle: '42 partite registrate',
  ctaNew: 'Nuova sessione',
};

const DEFAULT_PROPS: SessionsHeroProps = {
  onNewSession: vi.fn(),
  labels: LABELS,
};

describe('SessionsHero', () => {
  it('renders data-slot="sessions-hero"', () => {
    render(<SessionsHero {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="sessions-hero"]')).not.toBeNull();
  });

  it('renders title from labels', () => {
    render(<SessionsHero {...DEFAULT_PROPS} />);
    expect(screen.getByText('Le tue partite')).toBeTruthy();
  });

  it('renders resolved subtitle string from labels', () => {
    render(<SessionsHero {...DEFAULT_PROPS} />);
    expect(screen.getByText('42 partite registrate')).toBeTruthy();
  });

  it('renders CTA button with correct aria-label', () => {
    render(<SessionsHero {...DEFAULT_PROPS} />);
    const cta = document.querySelector('[data-slot="sessions-hero-cta"]');
    expect(cta).not.toBeNull();
    expect(cta!.getAttribute('aria-label')).toBe('Nuova sessione');
  });

  it('fires onNewSession callback on CTA click', () => {
    const onNewSession = vi.fn();
    render(<SessionsHero {...DEFAULT_PROPS} onNewSession={onNewSession} />);
    const cta = document.querySelector('[data-slot="sessions-hero-cta"]') as HTMLButtonElement;
    fireEvent.click(cta);
    expect(onNewSession).toHaveBeenCalledOnce();
  });

  it('applies custom className to the section', () => {
    render(<SessionsHero {...DEFAULT_PROPS} className="my-custom-class" />);
    const section = document.querySelector('[data-slot="sessions-hero"]');
    expect(section!.classList.contains('my-custom-class')).toBe(true);
  });
});
