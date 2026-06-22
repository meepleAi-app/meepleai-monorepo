/**
 * PlayAgainCta unit tests — Wave D.3 (Issue #756).
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { PlayAgainCta } from '../PlayAgainCta';
import type { PlayAgainCtaProps } from '../PlayAgainCta';

const LABELS: PlayAgainCtaProps['labels'] = {
  title: 'Pronti per la rivincita?',
  description: 'Stessi giocatori, stesso gioco — agente già caricato.',
  cta: 'Riavvia con stessi giocatori',
};

const DEFAULT_PROPS: PlayAgainCtaProps = {
  sessionId: 'session-1',
  labels: LABELS,
};

describe('PlayAgainCta', () => {
  it('renders data-slot="play-again-cta"', () => {
    render(<PlayAgainCta {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="play-again-cta"]')).not.toBeNull();
  });

  it('marks data-session-id from prop', () => {
    render(<PlayAgainCta {...DEFAULT_PROPS} sessionId="abc-123" />);
    const root = document.querySelector('[data-slot="play-again-cta"]')!;
    expect(root.getAttribute('data-session-id')).toBe('abc-123');
  });

  it('renders title, description, and CTA from labels', () => {
    render(<PlayAgainCta {...DEFAULT_PROPS} />);
    expect(screen.getByText('Pronti per la rivincita?')).toBeTruthy();
    expect(screen.getByText('Stessi giocatori, stesso gioco — agente già caricato.')).toBeTruthy();
    expect(screen.getByText('Riavvia con stessi giocatori')).toBeTruthy();
  });

  it('disables CTA button when onPlayAgain is omitted', () => {
    render(<PlayAgainCta {...DEFAULT_PROPS} />);
    const btn = document.querySelector('[data-slot="play-again-cta-button"]') as HTMLButtonElement;
    expect(btn.disabled).toBe(true);
    expect(btn.getAttribute('aria-disabled')).toBe('true');
    expect(btn.getAttribute('data-disabled')).toBe('true');
  });

  it('enables CTA button when onPlayAgain is provided', () => {
    render(<PlayAgainCta {...DEFAULT_PROPS} onPlayAgain={vi.fn()} />);
    const btn = document.querySelector('[data-slot="play-again-cta-button"]') as HTMLButtonElement;
    expect(btn.disabled).toBe(false);
    expect(btn.getAttribute('data-disabled')).toBeNull();
  });

  it('fires onPlayAgain when CTA button is clicked', () => {
    const onPlayAgain = vi.fn();
    render(<PlayAgainCta {...DEFAULT_PROPS} onPlayAgain={onPlayAgain} />);
    const btn = document.querySelector('[data-slot="play-again-cta-button"]') as HTMLButtonElement;
    fireEvent.click(btn);
    expect(onPlayAgain).toHaveBeenCalledOnce();
  });

  it('does not fire onPlayAgain when button is disabled', () => {
    const onPlayAgain = vi.fn();
    render(<PlayAgainCta {...DEFAULT_PROPS} onPlayAgain={onPlayAgain} isPending />);
    const btn = document.querySelector('[data-slot="play-again-cta-button"]') as HTMLButtonElement;
    fireEvent.click(btn);
    // Button is disabled (isPending=true) so onClick should NOT fire (browser ignores click)
    expect(onPlayAgain).not.toHaveBeenCalled();
  });

  it('marks aria-busy when isPending=true', () => {
    render(<PlayAgainCta {...DEFAULT_PROPS} onPlayAgain={vi.fn()} isPending />);
    const btn = document.querySelector('[data-slot="play-again-cta-button"]')!;
    expect(btn.getAttribute('aria-busy')).toBe('true');
  });

  it('disables button while pending even with onPlayAgain', () => {
    render(<PlayAgainCta {...DEFAULT_PROPS} onPlayAgain={vi.fn()} isPending />);
    const btn = document.querySelector('[data-slot="play-again-cta-button"]') as HTMLButtonElement;
    expect(btn.disabled).toBe(true);
  });

  it('renders the section semantic element', () => {
    render(<PlayAgainCta {...DEFAULT_PROPS} />);
    const root = document.querySelector('[data-slot="play-again-cta"]')!;
    expect(root.tagName).toBe('SECTION');
  });

  it('renders as <h3> heading for title', () => {
    render(<PlayAgainCta {...DEFAULT_PROPS} />);
    expect(document.querySelector('h3')).not.toBeNull();
  });

  it('applies className when provided', () => {
    render(<PlayAgainCta {...DEFAULT_PROPS} className="custom-cta" />);
    const root = document.querySelector('[data-slot="play-again-cta"]')!;
    expect(root.classList.contains('custom-cta')).toBe(true);
  });
});
