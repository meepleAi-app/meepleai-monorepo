/**
 * GameDetailLeaderboard Storybook Stories (Issue #1468)
 *
 * Visual fixtures for the Stats-tab leaderboard. Covers: many players, single player,
 * empty state, and a null avgScore row. Avatar colors are driven by the `hueFor` prop
 * (the caller injects `userHue` #1470; here a demo hash is used).
 * Dark theme variant handled globally via the preview decorator.
 */

import type { GameLeaderboardEntry } from '@/lib/api/schemas';

import { GameDetailLeaderboard } from './GameDetailLeaderboard';

import type { Meta, StoryObj } from '@storybook/react';

const labels = {
  plays: 'partite',
  avgScore: 'avg',
  wins: 'vittorie',
  empty: 'Nessuna classifica disponibile',
};

// Demo hue mapper — deterministic per playerId. The real caller passes userHue (#1470).
const demoHueFor = (playerId: string): number => {
  let h = 0;
  for (const ch of playerId) {
    h = (h * 31 + ch.charCodeAt(0)) % 360;
  }
  return h;
};

function makeEntry(
  playerId: string,
  displayName: string,
  initials: string,
  wins: number,
  plays: number,
  avgScore: number | null
): GameLeaderboardEntry {
  return {
    playerId,
    displayName,
    initials,
    wins,
    plays,
    avgScore,
    lastPlayedAt: '2026-05-20T12:00:00.000Z',
  };
}

const manyPlayers: ReadonlyArray<GameLeaderboardEntry> = [
  makeEntry('marco', 'Marco Rossi', 'MR', 12, 18, 91.2),
  makeEntry('lucia', 'Lucia Bianchi', 'LB', 9, 15, 84.0),
  makeEntry('gianni', 'Gianni Verdi', 'GV', 7, 14, 78.5),
  makeEntry('sara', 'Sara Neri', 'SN', 5, 11, 72.0),
  makeEntry('paolo', 'Paolo Gallo', 'PG', 3, 8, null),
];

const meta = {
  title: 'Components/GameDetail/GameDetailLeaderboard',
  component: GameDetailLeaderboard,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Pure presentational leaderboard. Ranks registered players by wins; the caller owns the useGameLeaderboard hook (#1467) and passes entries + a hueFor mapper (userHue #1470). DS-15 compliant.',
      },
    },
    chromatic: { viewports: [375, 768, 1024] },
  },
  tags: ['autodocs'],
  decorators: [
    Story => (
      <div className="w-full max-w-md">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof GameDetailLeaderboard>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Many players with colored avatars (hueFor injected). */
export const ManyPlayers: Story = {
  args: {
    entries: manyPlayers,
    title: 'Classifica giocatori',
    labels,
    hueFor: demoHueFor,
  },
};

/** Single player — trophy rank, colored avatar. */
export const SinglePlayer: Story = {
  args: {
    entries: [makeEntry('marco', 'Marco Rossi', 'MR', 4, 6, 88.0)],
    title: 'Classifica giocatori',
    labels,
    hueFor: demoHueFor,
  },
};

/** Empty state — no entries, localized empty label. */
export const Empty: Story = {
  args: {
    entries: [],
    title: 'Classifica giocatori',
    labels,
  },
};

/** Null avgScore — em-dash placeholder; neutral avatars (no hueFor). */
export const NullAvgScoreNeutralAvatars: Story = {
  args: {
    entries: [
      makeEntry('marco', 'Marco Rossi', 'MR', 8, 14, null),
      makeEntry('lucia', 'Lucia Bianchi', 'LB', 5, 12, null),
    ],
    title: 'Classifica giocatori',
    labels,
  },
};
