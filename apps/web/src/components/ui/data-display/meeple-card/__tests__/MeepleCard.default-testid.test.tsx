/**
 * Issue #4120 — la radice di MeepleCard porta un `data-testid` stabile anche quando il chiamante
 * non lo passa.
 *
 * Il contratto esponeva `'data-testid'` da sempre e tutte le varianti lo propagavano, ma nessun
 * chiamante lo passava: nel DOM non esisteva alcun hook per le schede. Le spec E2E cercavano
 * `game-card` (106 sedi, strascico della migrazione GameCard → MeepleCard) e `meeple-card`
 * (30 sedi), e nessuno dei due esisteva — l'unica occorrenza di `meeple-card` nel repo era un
 * mock Vitest, che un browser non vede mai.
 */
import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { MeepleCard } from '../MeepleCard';

import type { MeepleCardVariant } from '../types';

const variants: MeepleCardVariant[] = ['grid', 'list', 'compact', 'featured', 'hero'];

describe('MeepleCard — data-testid predefinito (#4120)', () => {
  it.each(variants)('la variante %s porta meeple-card sulla radice', variant => {
    const { container } = render(<MeepleCard entity="game" title="Catan" variant={variant} />);
    expect(container.querySelectorAll('[data-testid="meeple-card"]')).toHaveLength(1);
  });

  it('un valore esplicito vince sul predefinito', () => {
    const { container } = render(
      <MeepleCard entity="game" title="Catan" data-testid="library-game-card" />
    );
    expect(container.querySelector('[data-testid="library-game-card"]')).toBeTruthy();
    expect(container.querySelector('[data-testid="meeple-card"]')).toBeNull();
  });

  it('data-entity resta accanto, quindi il selettore composto discrimina per entità', () => {
    // E` la forma verso cui le sedi di `game-card` vanno riscritte, ed e` piu` precisa di quanto
    // `game-card` sia mai stato: distingue una scheda di gioco da una di sessione o di agente.
    const { container } = render(<MeepleCard entity="game" title="Catan" />);
    expect(container.querySelector('[data-testid="meeple-card"][data-entity="game"]')).toBeTruthy();

    const session = render(<MeepleCard entity="session" title="Partita" />);
    expect(
      session.container.querySelector('[data-testid="meeple-card"][data-entity="game"]')
    ).toBeNull();
    expect(
      session.container.querySelector('[data-testid="meeple-card"][data-entity="session"]')
    ).toBeTruthy();
  });

  it('una sola radice lo porta, anche nel ramo con href', () => {
    // Invariante che conta per Playwright: in modalita` strict un locator che trova due nodi
    // fallisce. I due siti di GridCard sono rami mutuamente esclusivi (href -> Link, altrimenti
    // div), e questo test lo fissa — se un domani il testid finisse anche su un nodo interno,
    // ogni asserzione E2E sulle schede diventerebbe ambigua.
    const withHref = render(<MeepleCard entity="game" title="Catan" href="/library/abc" />);
    expect(withHref.container.querySelectorAll('[data-testid="meeple-card"]')).toHaveLength(1);
    expect(withHref.container.querySelector('a[data-testid="meeple-card"]')).toBeTruthy();
  });

  it('nessuna variante lascia la radice senza testid', () => {
    for (const variant of variants) {
      const { container } = render(<MeepleCard entity="agent" title="Agente" variant={variant} />);
      const root = container.querySelector('[data-testid]');
      expect(root, `variante ${variant}`).toBeTruthy();
    }
  });
});
