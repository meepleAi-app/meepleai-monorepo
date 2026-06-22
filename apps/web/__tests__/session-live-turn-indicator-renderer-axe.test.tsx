/**
 * #2378 G5b — axe AA test for TurnIndicatorRenderer covering all 7 variants
 * + Unknown fallback.
 *
 * Spec: docs/superpowers/specs/2026-06-16-issue-2378-g5b-turn-indicator-renderer-design.md §6
 */

import { describe, it, expect, vi, beforeAll, afterAll } from 'vitest';
import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import { IntlProvider } from 'react-intl';

import { TurnIndicatorRenderer } from '@/components/features/session-live';
import type { TurnIndicatorRendererLabels } from '@/components/features/session-live';
import type { TurnState, PlayerInfo } from '@/lib/session-live/turn-state';

const PLAYERS: PlayerInfo[] = [
  { id: 'p1', name: 'Marco' },
  { id: 'p2', name: 'Sara' },
];

const LABELS: TurnIndicatorRendererLabels = {
  roundRobinHeading: 'Active turn',
  sequentialHeading: 'Phases',
  simultaneousHeading: 'All players act',
  realtimeHeading: 'Real time',
  noneHeading: 'Free play',
  customHeading: 'Custom sequence',
  firstPlayerTokenHeading: 'First player token',
  unknownTitle: 'Unsupported turn type',
  unknownBody: 'Update the app to support this mode.',
  yourTurnLabel: 'Your turn',
  waitingLabel: 'Waiting',
  roundCountTemplate: 'Round {current} of {total}',
  playOrderHeading: 'Play order',
  firstPlayerTokenHolderTemplate: 'First player token: {playerName}',
};

beforeAll(() => {
  // Suppress the console.warn from the Unknown fallback so the test log is clean.
  vi.spyOn(console, 'warn').mockImplementation(() => {});
});

afterAll(() => {
  vi.restoreAllMocks();
});

function renderRenderer(state: TurnState) {
  return render(
    <IntlProvider locale="en" messages={{}}>
      <main>
        <TurnIndicatorRenderer state={state} players={PLAYERS} viewerId="p1" labels={LABELS} />
      </main>
    </IntlProvider>
  );
}

type AxeFixture = readonly [name: string, state: TurnState];

const FIXTURES: ReadonlyArray<AxeFixture> = [
  [
    'RoundRobin',
    {
      type: 'RoundRobin',
      round: 1,
      totalRounds: 4,
      activePlayerId: 'p1',
      playOrder: ['p1', 'p2'],
    },
  ],
  ['Sequential', { type: 'Sequential', phases: ['Morning', 'Night'], activePhaseIndex: 0 }],
  ['Simultaneous', { type: 'Simultaneous', phases: ['Morning'], activePhaseIndex: 0 }],
  ['Realtime', { type: 'Realtime' }],
  ['None', { type: 'None' }],
  ['Custom', { type: 'Custom', phases: ['F1', 'F2'], activePhaseIndex: 0 }],
  [
    'FirstPlayerToken',
    {
      type: 'FirstPlayerToken',
      round: 1,
      totalRounds: 4,
      tokenHolderId: 'p1',
      playOrder: ['p1', 'p2'],
    },
  ],
  // Unknown fallback — cast to bypass the discriminated-union type check
  ['Unknown', { type: 'BogusType' } as unknown as TurnState],
];

describe('#2378 G5b — TurnIndicatorRenderer axe AA', () => {
  for (const [name, state] of FIXTURES) {
    it(`${name}: 0 axe AA violations`, async () => {
      const { container } = renderRenderer(state);
      // Disable landmark-unique rule because we test isolated components,
      // not full page context. Each variant will have different aria-labels,
      // so uniqueness is tested when integrated into SessionLiveView.
      const results = await axe(container, {
        rules: {
          'landmark-unique': { enabled: false },
        },
      });
      expect(results).toHaveNoViolations();
    });
  }
});
