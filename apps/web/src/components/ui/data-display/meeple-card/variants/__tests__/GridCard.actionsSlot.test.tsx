import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import { GridCard } from '../GridCard';

/**
 * #5081: an actions menu in `actionsSlot` is a real control OUTSIDE the card's clickable root (no axe
 * nested-interactive), and it replaces the decorative menu placeholder. Without it the card is unchanged.
 */
describe('GridCard actionsSlot (#5081)', () => {
  const slot = (
    <button type="button" aria-label="Azioni per «Catan»">
      ⋯
    </button>
  );

  it('renders the slot outside the clickable root, instead of the placeholder', () => {
    const { container } = render(
      <GridCard
        entity="game"
        variant="grid"
        title="Catan"
        onClick={() => {}}
        data-testid="card"
        actionsSlot={slot}
      />
    );

    const root = screen.getByTestId('card');
    const menu = screen.getByRole('button', { name: 'Azioni per «Catan»' });
    expect(root.contains(menu)).toBe(false);
    expect(container.querySelector('[data-slot="menu-placeholder"]')).toBeNull();
  });

  it('keeps the placeholder and the bare root when there is no slot', () => {
    const { container } = render(
      <GridCard entity="game" variant="grid" title="Catan" onClick={() => {}} data-testid="card" />
    );

    expect(container.querySelector('[data-slot="menu-placeholder"]')).not.toBeNull();
    expect(screen.getByTestId('card').parentElement).toBe(container);
  });

  it('has no axe AA violations with the actions menu next to a clickable card', async () => {
    const { container } = render(
      <GridCard entity="game" variant="grid" title="Catan" onClick={() => {}} actionsSlot={slot} />
    );

    expect(await axe(container)).toHaveNoViolations();
  });
});
