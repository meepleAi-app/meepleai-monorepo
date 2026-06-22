/**
 * GameDetailSpecsCard - Unit tests (Issue #1463).
 *
 * Test matrix (Crispin, T1-T6):
 *   T1. Render baseline 8 items.
 *   T2. data-slot + data-spec-key attributes present.
 *   T3. role=list + aria-label localized.
 *   T4. Custom title prop override.
 *   T5. Empty items array (edge state).
 *   T6. className composition.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { GameDetailSpecsCard, type GameDetailSpecsItem } from '../GameDetailSpecsCard';

const eightItems: ReadonlyArray<GameDetailSpecsItem> = [
  { key: 'players', label: 'Giocatori', value: '1–5' },
  { key: 'duration', label: 'Durata', value: '70 min' },
  { key: 'age', label: 'Età', value: '14+' },
  { key: 'complexity', label: 'Complessità', value: '2.4 / 5' },
  { key: 'year', label: 'Anno', value: '2017' },
  { key: 'designer', label: 'Designer', value: 'Stefan Feld' },
  { key: 'publisher', label: 'Editore', value: 'Kosmos' },
  { key: 'rating', label: 'Rating BGG', value: '8.1' },
];

describe('GameDetailSpecsCard (Issue #1463)', () => {
  // T1
  it('renders one listitem per entry (default 8 items)', () => {
    render(<GameDetailSpecsCard items={eightItems} title="Specifiche" />);
    const items = screen.getAllByRole('listitem');
    expect(items).toHaveLength(8);
  });

  it('renders labels and values', () => {
    render(<GameDetailSpecsCard items={eightItems} title="Specifiche" />);
    expect(screen.getByText('Giocatori')).toBeInTheDocument();
    expect(screen.getByText('1–5')).toBeInTheDocument();
    expect(screen.getByText('Rating BGG')).toBeInTheDocument();
    expect(screen.getByText('8.1')).toBeInTheDocument();
  });

  // T2
  it('exposes data-slot and data-spec-key for E2E selectors', () => {
    const { container } = render(<GameDetailSpecsCard items={eightItems} title="Specifiche" />);
    expect(container.querySelector('[data-slot="game-detail-specs-card"]')).toBeInTheDocument();
    const itemSlots = container.querySelectorAll('[data-spec-key]');
    expect(itemSlots).toHaveLength(8);
    expect(container.querySelector('[data-spec-key="rating"]')).toBeInTheDocument();
  });

  // T3
  it('renders role=list with aria-label matching title (default)', () => {
    render(<GameDetailSpecsCard items={eightItems} title="Specifiche" />);
    const list = screen.getByRole('list', { name: /specifiche/i });
    expect(list).toBeInTheDocument();
  });

  // T4
  it('honors custom title prop and propagates to aria-label', () => {
    render(<GameDetailSpecsCard items={eightItems} title="Stats" />);
    expect(screen.getByRole('heading', { name: 'Stats' })).toBeInTheDocument();
    expect(screen.getByRole('list', { name: /stats/i })).toBeInTheDocument();
  });

  // T5
  it('renders title but no listitems when items array is empty', () => {
    render(<GameDetailSpecsCard items={[]} title="Empty" />);
    expect(screen.getByRole('heading', { name: 'Empty' })).toBeInTheDocument();
    expect(screen.queryAllByRole('listitem')).toHaveLength(0);
  });

  // T6
  it('composes custom className with base classes', () => {
    const { container } = render(
      <GameDetailSpecsCard items={eightItems} className="extra-spacing" />
    );
    const section = container.querySelector('[data-slot="game-detail-specs-card"]');
    expect(section).toHaveClass('extra-spacing');
    // Base classes preserved
    expect(section).toHaveClass('bg-card');
    expect(section).toHaveClass('border-border');
  });

  it('uses DS-15 compliant semantic tokens only (text-foreground / text-muted-foreground)', () => {
    const { container } = render(<GameDetailSpecsCard items={eightItems} title="Specifiche" />);
    // Heading uses text-foreground
    const heading = container.querySelector('h3');
    expect(heading).toHaveClass('text-foreground');
    // First label uses text-muted-foreground (mono uppercase)
    const firstLabel = container.querySelector('[data-spec-key="players"] .font-mono');
    expect(firstLabel).toHaveClass('text-muted-foreground');
  });
});
