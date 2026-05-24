/**
 * GameDetailCommunityGate - Unit tests (Issue #1465).
 *
 * Pure presentational empty-state for the community variant of /games/[id].
 * Test matrix (Crispin):
 *   T1. Renders default icon + title + description + cta.
 *   T2. Custom icon override.
 *   T3. onAdd fires on CTA click.
 *   T4. data-slot attributes.
 *   T5. DS-15 entity-game token.
 *   T6. className composition.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { GameDetailCommunityGate } from '../GameDetailCommunityGate';

const baseProps = {
  title: 'Aggiungi alla libreria',
  description: 'Aggiungi questo gioco per sbloccare sessioni e statistiche.',
  ctaLabel: '+ Aggiungi a libreria',
  onAdd: () => {},
};

describe('GameDetailCommunityGate (Issue #1465)', () => {
  // T1
  it('renders default icon, title, description and cta', () => {
    render(<GameDetailCommunityGate {...baseProps} />);
    expect(screen.getByText('📘')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Aggiungi alla libreria' })).toBeInTheDocument();
    expect(
      screen.getByText('Aggiungi questo gioco per sbloccare sessioni e statistiche.')
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '+ Aggiungi a libreria' })).toBeInTheDocument();
  });

  // T2
  it('honors a custom icon', () => {
    render(<GameDetailCommunityGate {...baseProps} icon="🌐" />);
    expect(screen.getByText('🌐')).toBeInTheDocument();
    expect(screen.queryByText('📘')).not.toBeInTheDocument();
  });

  // T3
  it('fires onAdd when the CTA is clicked', () => {
    const onAdd = vi.fn();
    render(<GameDetailCommunityGate {...baseProps} onAdd={onAdd} />);
    fireEvent.click(screen.getByRole('button', { name: '+ Aggiungi a libreria' }));
    expect(onAdd).toHaveBeenCalledTimes(1);
  });

  // T4
  it('exposes data-slot for the card and cta', () => {
    const { container } = render(<GameDetailCommunityGate {...baseProps} />);
    expect(container.querySelector('[data-slot="game-detail-community-gate"]')).toBeInTheDocument();
    expect(
      container.querySelector('[data-slot="game-detail-community-gate-cta"]')
    ).toBeInTheDocument();
  });

  // T5
  it('uses DS-15 entity-game tokens (tinted circle + solid CTA)', () => {
    const { container } = render(<GameDetailCommunityGate {...baseProps} />);
    // Icon circle: tinted background + entity foreground.
    const circle = container.querySelector('[aria-hidden="true"]');
    expect(circle).toHaveClass('bg-entity-game/12');
    expect(circle).toHaveClass('text-entity-game');
    // CTA: solid entity background.
    const cta = container.querySelector('[data-slot="game-detail-community-gate-cta"]');
    expect(cta).toHaveClass('bg-entity-game');
  });

  // T6
  it('composes custom className with base classes', () => {
    const { container } = render(<GameDetailCommunityGate {...baseProps} className="extra" />);
    const card = container.querySelector('[data-slot="game-detail-community-gate"]');
    expect(card).toHaveClass('extra');
    expect(card).toHaveClass('bg-card');
    expect(card).toHaveClass('border-dashed');
  });
});
