import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { MeepleCard } from '../MeepleCard';
import type { MeepleEntityType, MeepleCardVariant } from '../types';

const entities: MeepleEntityType[] = ['game', 'player', 'session', 'agent', 'kb', 'chat', 'event', 'toolkit', 'tool'];
const variants: MeepleCardVariant[] = ['grid', 'list', 'compact', 'featured', 'hero'];

describe('MeepleCard', () => {
  it.each(entities)('renders %s entity type without error', (entity) => {
    const { container } = render(<MeepleCard entity={entity} title={`Test ${entity}`} />);
    expect(container.firstChild).toBeTruthy();
  });

  it.each(variants)('renders %s variant without error', (variant) => {
    const { container } = render(<MeepleCard entity="game" title="Test" variant={variant} />);
    expect(container.firstChild).toBeTruthy();
  });

  it('displays title text', () => {
    render(<MeepleCard entity="game" title="Catan" />);
    expect(screen.getByText('Catan')).toBeTruthy();
  });

  it('displays subtitle when provided', () => {
    render(<MeepleCard entity="game" title="Catan" subtitle="Kosmos" />);
    expect(screen.getByText('Kosmos')).toBeTruthy();
  });

  it('renders entity badge with correct label', () => {
    render(<MeepleCard entity="game" title="Test" />);
    expect(screen.getByText('Game')).toBeTruthy();
  });

  it('defaults to grid variant', () => {
    const { container } = render(<MeepleCard entity="game" title="Test" />);
    expect(container.querySelector('[class*="aspect-"]')).toBeTruthy();
  });
});
