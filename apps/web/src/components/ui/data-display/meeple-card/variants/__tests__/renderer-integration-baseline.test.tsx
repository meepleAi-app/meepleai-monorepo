// Baseline DOM regression test for navItems rendering across Grid/List/Featured/Focus.
// Intentionally uses 4 independent describe blocks instead of describe.each:
// Vitest 3.x cannot populate toMatchInlineSnapshot() inside parameterised suites
// (dynamic test names break snapshot call-site association). Do not consolidate.
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { GridCard } from '../GridCard';
import { ListCard } from '../ListCard';
import { FeaturedCard } from '../FeaturedCard';
import { FocusCard } from '../FocusCard';

const mockNavItems3 = [
  {
    label: '3 sessioni',
    entity: 'session' as const,
    count: 3,
    href: '/s/1',
    icon: <i data-testid="icon-s" />,
  },
  {
    label: '2 KB docs',
    entity: 'kb' as const,
    count: 2,
    href: '/k/1',
    icon: <i data-testid="icon-k" />,
  },
  {
    label: 'Nuovo',
    entity: 'player' as const,
    count: 0,
    showPlus: true,
    onPlusClick: () => {},
    icon: <i data-testid="icon-p" />,
  },
];

describe('GridCard baseline with navItems', () => {
  it('renders the expected nav DOM (baseline)', () => {
    render(<GridCard entity="game" title="X" navItems={mockNavItems3} />);
    const links = screen.queryAllByRole('link');
    const buttons = screen.queryAllByRole('button');
    expect({ links: links.length, buttons: buttons.length }).toMatchInlineSnapshot(`
      {
        "buttons": 2,
        "links": 2,
      }
    `);
    expect(links.map(l => l.getAttribute('aria-label'))).toMatchInlineSnapshot(`
      [
        "3 sessioni",
        "2 KB docs",
      ]
    `);
  });
});

describe('ListCard baseline with navItems', () => {
  it('renders the expected nav DOM (baseline)', () => {
    render(<ListCard entity="game" title="X" navItems={mockNavItems3} />);
    const links = screen.queryAllByRole('link');
    const buttons = screen.queryAllByRole('button');
    expect({ links: links.length, buttons: buttons.length }).toMatchInlineSnapshot(`
      {
        "buttons": 2,
        "links": 2,
      }
    `);
    expect(links.map(l => l.getAttribute('aria-label'))).toMatchInlineSnapshot(`
      [
        "3 sessioni",
        "2 KB docs",
      ]
    `);
  });
});

describe('FeaturedCard baseline with navItems', () => {
  it('renders the expected nav DOM (baseline)', () => {
    render(<FeaturedCard entity="game" title="X" navItems={mockNavItems3} />);
    const links = screen.queryAllByRole('link');
    const buttons = screen.queryAllByRole('button');
    expect({ links: links.length, buttons: buttons.length }).toMatchInlineSnapshot(`
      {
        "buttons": 2,
        "links": 2,
      }
    `);
    expect(links.map(l => l.getAttribute('aria-label'))).toMatchInlineSnapshot(`
      [
        "3 sessioni",
        "2 KB docs",
      ]
    `);
  });
});

describe('FocusCard baseline with navItems', () => {
  it('renders the expected nav DOM (baseline)', () => {
    render(<FocusCard entity="game" title="X" navItems={mockNavItems3} />);
    const links = screen.queryAllByRole('link');
    const buttons = screen.queryAllByRole('button');
    expect({ links: links.length, buttons: buttons.length }).toMatchInlineSnapshot(`
      {
        "buttons": 1,
        "links": 2,
      }
    `);
    expect(links.map(l => l.getAttribute('aria-label'))).toMatchInlineSnapshot(`
      [
        "3 sessioni",
        "2 KB docs",
      ]
    `);
  });
});
