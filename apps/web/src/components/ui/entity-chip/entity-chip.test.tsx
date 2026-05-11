import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { EntityChip } from './entity-chip';

expect.extend(toHaveNoViolations);

describe('EntityChip', () => {
  it('renders emoji and label', () => {
    render(<EntityChip entity="game" label="Catan" />);
    expect(screen.getByText('🎲')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('applies entity color class', () => {
    const { container } = render(<EntityChip entity="session" label="Turno 1" />);
    expect(container.querySelector('.bg-entity-session\\/10')).toBeInTheDocument();
  });

  it('renders size sm by default and md when prop set', () => {
    const { container, rerender } = render(<EntityChip entity="agent" label="Rulebook AI" />);
    expect(container.querySelector('.text-xs')).toBeInTheDocument();
    rerender(<EntityChip entity="agent" label="Rulebook AI" size="md" />);
    expect(container.querySelector('.text-sm')).toBeInTheDocument();
  });

  it('has no a11y violations', async () => {
    const { container } = render(<EntityChip entity="player" label="Alice" />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
