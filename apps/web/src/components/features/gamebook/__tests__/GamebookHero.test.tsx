/**
 * GamebookHero unit tests — SP6 Phase B Task 2 (Issue #788).
 *
 * Coverage:
 *   - data-slot identity
 *   - title + subtitle from labels
 *   - 3 KPI tiles render correct values + labels
 *   - CTA fires onAddManualClick
 *   - aria-label on CTA
 *   - custom className composition
 *   - accessibility: H1 heading, dl/dt/dd semantic structure
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { GamebookHero } from '../GamebookHero';
import type { GamebookHeroProps } from '../GamebookHero';

const LABELS: GamebookHeroProps['labels'] = {
  title: 'I tuoi manuali',
  subtitle: 'Manuali fotografati pronti per il tavolo',
  kpiTotalGamebooks: 'manuali',
  kpiTotalSessions: 'questo mese',
  kpiActiveAgents: 'traduzioni',
  ctaAddManual: '+ Aggiungi manuale',
};

const DEFAULT_PROPS: GamebookHeroProps = {
  totalGamebooks: 4,
  totalSessions: 2,
  activeAgents: 12,
  onAddManualClick: vi.fn(),
  labels: LABELS,
};

describe('GamebookHero', () => {
  it('renders data-slot="gamebook-hero"', () => {
    render(<GamebookHero {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="gamebook-hero"]')).not.toBeNull();
  });

  it('renders title from labels as H1 heading', () => {
    render(<GamebookHero {...DEFAULT_PROPS} />);
    const heading = document.querySelector('h1');
    expect(heading?.textContent).toBe('I tuoi manuali');
  });

  it('renders title with data-slot="gamebook-hero-title"', () => {
    render(<GamebookHero {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="gamebook-hero-title"]')).not.toBeNull();
  });

  it('renders subtitle text from labels', () => {
    render(<GamebookHero {...DEFAULT_PROPS} />);
    expect(screen.getByText('Manuali fotografati pronti per il tavolo')).toBeTruthy();
  });

  it('renders 3 KPI tiles inside dl', () => {
    render(<GamebookHero {...DEFAULT_PROPS} />);
    const dl = document.querySelector('dl[data-slot="gamebook-hero-kpis"]');
    expect(dl).not.toBeNull();
    expect(dl!.querySelectorAll('dt')).toHaveLength(3);
    expect(dl!.querySelectorAll('dd')).toHaveLength(3);
  });

  it('renders KPI totalGamebooks value and label', () => {
    render(<GamebookHero {...DEFAULT_PROPS} />);
    const tile = document.querySelector('[data-slot="gamebook-hero-kpi-totalGamebooks"]');
    expect(tile).not.toBeNull();
    expect(tile!.textContent).toContain('4');
    expect(tile!.textContent).toContain('manuali');
  });

  it('renders KPI totalSessions value and label', () => {
    render(<GamebookHero {...DEFAULT_PROPS} />);
    const tile = document.querySelector('[data-slot="gamebook-hero-kpi-totalSessions"]');
    expect(tile).not.toBeNull();
    expect(tile!.textContent).toContain('2');
    expect(tile!.textContent).toContain('questo mese');
  });

  it('renders KPI activeAgents value and label', () => {
    render(<GamebookHero {...DEFAULT_PROPS} />);
    const tile = document.querySelector('[data-slot="gamebook-hero-kpi-activeAgents"]');
    expect(tile).not.toBeNull();
    expect(tile!.textContent).toContain('12');
    expect(tile!.textContent).toContain('traduzioni');
  });

  it('renders zero values gracefully (empty state)', () => {
    render(
      <GamebookHero {...DEFAULT_PROPS} totalGamebooks={0} totalSessions={0} activeAgents={0} />
    );
    const tile = document.querySelector('[data-slot="gamebook-hero-kpi-totalGamebooks"]');
    expect(tile!.textContent).toContain('0');
  });

  it('renders CTA button with correct aria-label', () => {
    render(<GamebookHero {...DEFAULT_PROPS} />);
    const cta = document.querySelector('[data-slot="gamebook-hero-cta"]');
    expect(cta).not.toBeNull();
    expect(cta!.getAttribute('aria-label')).toBe('+ Aggiungi manuale');
  });

  it('fires onAddManualClick when CTA clicked', () => {
    const onAddManualClick = vi.fn();
    render(<GamebookHero {...DEFAULT_PROPS} onAddManualClick={onAddManualClick} />);
    fireEvent.click(document.querySelector('[data-slot="gamebook-hero-cta"]')!);
    expect(onAddManualClick).toHaveBeenCalledOnce();
  });

  it('applies custom className to outer section', () => {
    render(<GamebookHero {...DEFAULT_PROPS} className="my-extra-class" />);
    const section = document.querySelector('[data-slot="gamebook-hero"]');
    expect(section!.classList.contains('my-extra-class')).toBe(true);
  });
});
