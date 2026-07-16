/**
 * CategoriesTable (EntityTableView) — axe AA accessibility gate (#2955 Fase 3).
 *
 * This is the EntityTableView entity="game" table (Issue #4862), distinct from
 * the bespoke `categories-table.tsx` admin CRUD table. It is pure props-based
 * (no fetch / router / providers), so it mounts directly — mirroring the
 * sibling axe precedents (TopAgentsTable.axe, entity-table-view). Guards the
 * restored entity="game" per-entity coloring (row border + badge) across
 * multiple rows on this admin consumer surface.
 */
import { render } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import { CategoriesTable, type GameCategory } from '../CategoriesTable';

expect.extend(toHaveNoViolations);

const CATEGORIES: GameCategory[] = [
  { id: 'c1', name: 'Strategy', gameCount: 42, description: 'Deep decision-making' },
  { id: 'c2', name: 'Party', gameCount: 28, description: 'Large groups, light rules' },
  { id: 'c3', name: 'Cooperative', gameCount: 19 },
];

describe('CategoriesTable (EntityTableView) — axe AA gate (#2955 Fase 3)', () => {
  it('has no axe violations rendering the entity="game" categories table', async () => {
    const { container } = render(<CategoriesTable categories={CATEGORIES} />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
