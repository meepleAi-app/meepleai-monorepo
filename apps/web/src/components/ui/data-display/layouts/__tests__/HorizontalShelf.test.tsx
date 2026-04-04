import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { HorizontalShelf } from '../HorizontalShelf';

describe('HorizontalShelf', () => {
  it('renders title with entity name and count', () => {
    render(
      <HorizontalShelf entity="game" title="My Games" count={47}>
        <div>Card 1</div>
        <div>Card 2</div>
      </HorizontalShelf>
    );
    expect(screen.getByText('My Games')).toBeTruthy();
    expect(screen.getByText('47')).toBeTruthy();
  });

  it('does not render when count < 2', () => {
    const { container } = render(
      <HorizontalShelf entity="game" title="My Games" count={1}>
        <div>Card 1</div>
      </HorizontalShelf>
    );
    expect(container.firstChild).toBeNull();
  });

  it('renders children in horizontal scroll container', () => {
    const { container } = render(
      <HorizontalShelf entity="game" title="My Games" count={3}>
        <div>Card 1</div>
        <div>Card 2</div>
        <div>Card 3</div>
      </HorizontalShelf>
    );
    const scrollContainer = container.querySelector('[class*="overflow-x-auto"]');
    expect(scrollContainer).toBeTruthy();
  });
});
