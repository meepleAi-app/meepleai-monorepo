/**
 * Wave B.2 (Issue #634) — AgentsHero v2 component tests.
 *
 * Pure component pattern (mirror Wave B.1 GamesHero):
 *   labels passed via prop, no `useTranslation` internal — keeps the component
 *   provider-free and testable without IntlProvider wrap.
 *
 * Contract under test (spec §3.2 + plan §4.1):
 *   - eyebrow + h1 title + subtitle from `labels`
 *   - exactly 4 stat tiles ({attivo, inSetup, archiviato, totalInvocations})
 *   - primary CTA "Crea agente" wired to `onCreateAgent`
 *   - `compact` prop collapses subtitle (eyebrow + title remain)
 *   - root carries `data-slot="agents-library-hero"` for spec scoping
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { AgentsHero, type AgentsHeroLabels, type AgentsHeroStat } from '../AgentsHero';

const baseLabels: AgentsHeroLabels = {
  eyebrow: 'I tuoi agenti AI',
  title: 'Studio agenti',
  subtitle: 'Crea, gestisci e affina i tuoi agenti AI specializzati per ogni gioco.',
  ctaCreate: 'Crea agente',
};

const baseStats: readonly AgentsHeroStat[] = [
  { label: 'Attivi', value: 3 },
  { label: 'In setup', value: 2 },
  { label: 'Archiviati', value: 1 },
  { label: 'Invocazioni totali', value: 253 },
];

describe('AgentsHero (Wave B.2)', () => {
  it('renders eyebrow text from labels.eyebrow above the title', () => {
    const { container } = render(<AgentsHero labels={baseLabels} stats={baseStats} />);
    const eyebrow = container.querySelector('[data-slot="agents-hero-eyebrow"]');
    expect(eyebrow).not.toBeNull();
    expect(eyebrow?.textContent).toBe('I tuoi agenti AI');
  });

  it('renders title as h1 from labels.title', () => {
    render(<AgentsHero labels={baseLabels} stats={baseStats} />);
    expect(screen.getByRole('heading', { level: 1, name: 'Studio agenti' })).toBeInTheDocument();
  });

  it('renders subtitle from labels.subtitle by default', () => {
    render(<AgentsHero labels={baseLabels} stats={baseStats} />);
    expect(
      screen.getByText('Crea, gestisci e affina i tuoi agenti AI specializzati per ogni gioco.')
    ).toBeInTheDocument();
  });

  it('renders all 4 stat tiles with their labels and values', () => {
    render(<AgentsHero labels={baseLabels} stats={baseStats} />);
    expect(screen.getByText('Attivi')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('In setup')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
    expect(screen.getByText('Archiviati')).toBeInTheDocument();
    expect(screen.getByText('1')).toBeInTheDocument();
    expect(screen.getByText('Invocazioni totali')).toBeInTheDocument();
    expect(screen.getByText('253')).toBeInTheDocument();
  });

  it('renders zero values explicitly when stats are empty (no fallback dash)', () => {
    const zeroStats: readonly AgentsHeroStat[] = [
      { label: 'Attivi', value: 0 },
      { label: 'In setup', value: 0 },
      { label: 'Archiviati', value: 0 },
      { label: 'Invocazioni totali', value: 0 },
    ];
    const { container } = render(<AgentsHero labels={baseLabels} stats={zeroStats} />);
    const tiles = container.querySelectorAll('[data-slot="agents-hero-stat"]');
    expect(tiles).toHaveLength(4);
    const zeroNodes = container.querySelectorAll('[data-slot="agents-hero-stat-value"]');
    expect(zeroNodes).toHaveLength(4);
    zeroNodes.forEach(node => {
      expect(node.textContent?.trim()).toBe('0');
    });
  });

  it('renders the "Crea agente" CTA and calls onCreateAgent on click', () => {
    const onCreateAgent = vi.fn();
    render(<AgentsHero labels={baseLabels} stats={baseStats} onCreateAgent={onCreateAgent} />);
    const cta = screen.getByRole('button', { name: 'Crea agente' });
    expect(cta).toBeInTheDocument();
    fireEvent.click(cta);
    expect(onCreateAgent).toHaveBeenCalledTimes(1);
  });

  it('omits subtitle when compact is true (mobile collapse) but keeps eyebrow + title', () => {
    const { container } = render(<AgentsHero labels={baseLabels} stats={baseStats} compact />);
    expect(
      screen.queryByText('Crea, gestisci e affina i tuoi agenti AI specializzati per ogni gioco.')
    ).toBeNull();
    expect(screen.getByRole('heading', { level: 1, name: 'Studio agenti' })).toBeInTheDocument();
    const eyebrow = container.querySelector('[data-slot="agents-hero-eyebrow"]');
    expect(eyebrow?.textContent).toBe('I tuoi agenti AI');
  });

  it('exposes data-slot="agents-library-hero" on the root for spec scoping', () => {
    const { container } = render(<AgentsHero labels={baseLabels} stats={baseStats} />);
    expect(container.querySelector('[data-slot="agents-library-hero"]')).not.toBeNull();
  });
});
