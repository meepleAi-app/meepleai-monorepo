import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { HandRailItem } from '../HandRailItem';

describe('HandRailItem', () => {
  const baseCard = {
    id: 'azul',
    entity: 'game' as const,
    title: 'Azul',
    href: '/library/azul',
  };

  it('renders title in the card body', () => {
    render(<HandRailItem card={baseCard} isActive={false} />);
    expect(screen.getByText('Azul')).toBeInTheDocument();
  });

  it('renders as a link to card.href', () => {
    render(<HandRailItem card={baseCard} isActive={false} />);
    expect(screen.getByRole('link')).toHaveAttribute('href', '/library/azul');
  });

  it('applies active state when isActive is true', () => {
    render(<HandRailItem card={baseCard} isActive />);
    expect(screen.getByRole('link')).toHaveAttribute('data-active', 'true');
  });

  it('uses entity data attribute for styling', () => {
    render(<HandRailItem card={baseCard} isActive={false} />);
    expect(screen.getByRole('link')).toHaveAttribute('data-entity', 'game');
  });

  it('applies different entity for session', () => {
    render(<HandRailItem card={{ ...baseCard, entity: 'session' }} isActive={false} />);
    expect(screen.getByRole('link')).toHaveAttribute('data-entity', 'session');
  });
});
