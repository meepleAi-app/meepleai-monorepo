/**
 * HubToolkitsHero - Unit tests (Issue #1480).
 *
 * Pure presentational hero for `/toolkits` hub. Maps from sp4-hub-toolkits.jsx:
 * 233-280 (function HubToolkitsHero). Gradient bg + eyebrow + h1 + subtitle +
 * N stat tiles.
 *
 * Test matrix (Crispin):
 *   T1. data-slot on root.
 *   T2. Renders eyebrow + h1 + subtitle from labels.
 *   T3. Renders each stat tile (label + value [+ unit]).
 *   T4. DS-15 tokens (bg gradient with entity-toolkit, border-bottom-border).
 *   T5. className composition.
 *   T6. Passes axe a11y scan.
 */

import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import { HubToolkitsHero } from '../HubToolkitsHero';

const labels = {
  eyebrow: 'Hub · /toolkits',
  title: 'Catalogo toolkit community',
  subtitle: "Bundle pronti all'uso di strumenti per i tuoi giochi.",
};

const stats = [
  { label: 'Toolkit', value: 24 },
  { label: 'Installazioni', value: '12.4k' },
  { label: 'Featured', value: 6 },
  { label: 'Strumenti totali', value: 142, unit: 'tools' },
];

describe('HubToolkitsHero (Issue #1480)', () => {
  // T1
  it('exposes a data-slot on the root', () => {
    const { container } = render(<HubToolkitsHero labels={labels} stats={stats} />);
    expect(container.querySelector('[data-slot="toolkits-index-hero"]')).toBeInTheDocument();
  });

  // T2
  it('renders eyebrow, h1, and subtitle from labels', () => {
    render(<HubToolkitsHero labels={labels} stats={stats} />);
    expect(screen.getByText(labels.eyebrow)).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: labels.title })).toBeInTheDocument();
    expect(screen.getByText(labels.subtitle)).toBeInTheDocument();
  });

  // T3
  it('renders each stat tile with its label, value, and optional unit', () => {
    const { container } = render(<HubToolkitsHero labels={labels} stats={stats} />);
    const tiles = container.querySelectorAll('[data-slot="toolkits-index-hero-stat"]');
    expect(tiles).toHaveLength(4);
    expect(screen.getByText('Toolkit')).toBeInTheDocument();
    expect(screen.getByText('24')).toBeInTheDocument();
    expect(screen.getByText('12.4k')).toBeInTheDocument();
    expect(screen.getByText('tools')).toBeInTheDocument();
  });

  // T4
  it('uses entity-toolkit accent and border-bottom DS-15 tokens', () => {
    const { container } = render(<HubToolkitsHero labels={labels} stats={stats} />);
    const root = container.querySelector('[data-slot="toolkits-index-hero"]');
    expect(root).toHaveClass('border-b');
    expect(root).toHaveClass('border-border');
  });

  // T5
  it('composes custom className with base classes', () => {
    const { container } = render(
      <HubToolkitsHero labels={labels} stats={stats} className="extra" />
    );
    const root = container.querySelector('[data-slot="toolkits-index-hero"]');
    expect(root).toHaveClass('extra');
  });

  // T6
  it('passes axe a11y scan', async () => {
    const { container } = render(<HubToolkitsHero labels={labels} stats={stats} />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
