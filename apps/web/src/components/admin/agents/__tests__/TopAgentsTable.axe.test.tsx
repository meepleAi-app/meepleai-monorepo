/**
 * TopAgentsTable — axe AA accessibility gate (#2955 Fase 3).
 *
 * The only existing consumer test (analytics-tabs.test.tsx) mocks this
 * component out, so there is no jest-axe coverage on the real render. This
 * component is pure props-based (no fetch / router / providers), so it mounts
 * directly — mirroring the sibling `__tests__/*-table.test.tsx` fixtures and
 * the standalone axe precedents (wizard-modal-axe, entity-table-view).
 *
 * Guards the restored entity="agent" per-entity coloring (row border + badge)
 * across multiple rows on this admin consumer surface.
 */
import { render } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import { TopAgentsTable } from '../TopAgentsTable';

import type { TopAgent } from '@/app/(authenticated)/admin/agents/metrics/client';

expect.extend(toHaveNoViolations);

// Spread across the confidence-badge colour buckets (>=90 / >=70 / >=50 / <50)
// so every branch of the table's cell renderers is exercised in one scan.
const AGENTS: TopAgent[] = [
  {
    agentDefinitionId: 'a1',
    typologyName: 'Rules Expert',
    invocations: 1280,
    cost: 4.21,
    avgConfidence: 0.93,
    avgLatencyMs: 820,
  },
  {
    agentDefinitionId: 'a2',
    typologyName: 'Setup Helper',
    invocations: 342,
    cost: 0.087,
    avgConfidence: 0.71,
    avgLatencyMs: 1450,
  },
  {
    agentDefinitionId: 'a3',
    typologyName: 'Scorekeeper',
    invocations: 58,
    cost: 0.0043,
    avgConfidence: 0.44,
    avgLatencyMs: 260,
  },
];

describe('TopAgentsTable — axe AA gate (#2955 Fase 3)', () => {
  it('has no axe violations rendering the entity="agent" metrics table', async () => {
    const { container } = render(<TopAgentsTable agents={AGENTS} />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
