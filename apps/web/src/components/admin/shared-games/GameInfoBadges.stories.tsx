/**
 * GameInfoBadges - Issue #2372
 *
 * Reusable badge components for displaying game metadata:
 * - PlayersBadge: min-max players count
 * - PlayTimeBadge: playing time in minutes
 * - ComplexityBadge: complexity rating (1-5)
 * - RatingBadge: average rating (0-10)
 * - AgeBadge: minimum age
 */

import {
  PlayersBadge,
  PlayTimeBadge,
  ComplexityBadge,
  RatingBadge,
  AgeBadge,
} from './GameInfoBadges';

import type { Meta, StoryObj } from '@storybook/react';

// ==================== PlayersBadge ====================

const playersMeta = {
  title: 'Admin/SharedGames/GameInfoBadges/PlayersBadge',
  component: PlayersBadge,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof PlayersBadge>;

export default playersMeta;
type PlayersStory = StoryObj<typeof playersMeta>;

/**
 * Range of players (2-4)
 */
export const PlayersRange: PlayersStory = {
  args: {
    min: 2,
    max: 4,
  },
};

/**
 * Single player count
 */
export const PlayersSingle: PlayersStory = {
  args: {
    min: 4,
    max: 4,
  },
};

/**
 * Large range (2-8)
 */
export const PlayersLargeRange: PlayersStory = {
  args: {
    min: 2,
    max: 8,
  },
};

/**
 * Small size
 */
export const PlayersSmall: PlayersStory = {
  args: {
    min: 2,
    max: 4,
    size: 'sm',
  },
};

// ==================== PlayTimeBadge ====================

const playTimeMeta = {
  title: 'Admin/SharedGames/GameInfoBadges/PlayTimeBadge',
  component: PlayTimeBadge,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof PlayTimeBadge>;

type PlayTimeStory = StoryObj<typeof playTimeMeta>;

/**
 * Short game (30 minutes)
 */
export const PlayTimeShort: PlayTimeStory = {
  render: () => <PlayTimeBadge minutes={30} />,
};

/**
 * Medium game (60 minutes)
 */
export const PlayTimeMedium: PlayTimeStory = {
  render: () => <PlayTimeBadge minutes={60} />,
};

/**
 * Long game (90 minutes)
 */
export const PlayTimeLong: PlayTimeStory = {
  render: () => <PlayTimeBadge minutes={90} />,
};

/**
 * Very long game (180 minutes)
 */
export const PlayTimeVeryLong: PlayTimeStory = {
  render: () => <PlayTimeBadge minutes={180} />,
};

/**
 * Small size
 */
export const PlayTimeSmall: PlayTimeStory = {
  render: () => <PlayTimeBadge minutes={45} size="sm" />,
};

// ==================== ComplexityBadge ====================

const complexityMeta = {
  title: 'Admin/SharedGames/GameInfoBadges/ComplexityBadge',
  component: ComplexityBadge,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof ComplexityBadge>;

type ComplexityStory = StoryObj<typeof complexityMeta>;

/**
 * Light complexity (1.5)
 */
export const ComplexityLight: ComplexityStory = {
  render: () => <ComplexityBadge rating={1.5} />,
};

/**
 * Medium-light complexity (2.5)
 */
export const ComplexityMediumLight: ComplexityStory = {
  render: () => <ComplexityBadge rating={2.5} />,
};

/**
 * Medium-heavy complexity (3.5)
 */
export const ComplexityMediumHeavy: ComplexityStory = {
  render: () => <ComplexityBadge rating={3.5} />,
};

/**
 * Heavy complexity (4.5)
 */
export const ComplexityHeavy: ComplexityStory = {
  render: () => <ComplexityBadge rating={4.5} />,
};

/**
 * No data
 */
export const ComplexityNoData: ComplexityStory = {
  render: () => <ComplexityBadge rating={null} />,
};

/**
 * Small size
 */
export const ComplexitySmall: ComplexityStory = {
  render: () => <ComplexityBadge rating={2.8} size="sm" />,
};

/**
 * All complexity levels
 */
export const ComplexityAllLevels: ComplexityStory = {
  render: () => (
    <div className="flex items-center gap-4">
      <ComplexityBadge rating={1.5} />
      <ComplexityBadge rating={2.5} />
      <ComplexityBadge rating={3.5} />
      <ComplexityBadge rating={4.5} />
      <ComplexityBadge rating={null} />
    </div>
  ),
};

// ==================== RatingBadge ====================

const ratingMeta = {
  title: 'Admin/SharedGames/GameInfoBadges/RatingBadge',
  component: RatingBadge,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof RatingBadge>;

type RatingStory = StoryObj<typeof ratingMeta>;

/**
 * High rating (8.5)
 */
export const RatingHigh: RatingStory = {
  render: () => <RatingBadge rating={8.5} />,
};

/**
 * Good rating (7.2)
 */
export const RatingGood: RatingStory = {
  render: () => <RatingBadge rating={7.2} />,
};

/**
 * Average rating (6.5)
 */
export const RatingAverage: RatingStory = {
  render: () => <RatingBadge rating={6.5} />,
};

/**
 * Low rating (5.2)
 */
export const RatingLow: RatingStory = {
  render: () => <RatingBadge rating={5.2} />,
};

/**
 * No data
 */
export const RatingNoData: RatingStory = {
  render: () => <RatingBadge rating={null} />,
};

/**
 * Small size
 */
export const RatingSmall: RatingStory = {
  render: () => <RatingBadge rating={7.8} size="sm" />,
};

/**
 * All rating levels
 */
export const RatingAllLevels: RatingStory = {
  render: () => (
    <div className="flex items-center gap-4">
      <RatingBadge rating={8.5} />
      <RatingBadge rating={7.2} />
      <RatingBadge rating={6.5} />
      <RatingBadge rating={5.2} />
      <RatingBadge rating={null} />
    </div>
  ),
};

// ==================== AgeBadge ====================

const ageMeta = {
  title: 'Admin/SharedGames/GameInfoBadges/AgeBadge',
  component: AgeBadge,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof AgeBadge>;

type AgeStory = StoryObj<typeof ageMeta>;

/**
 * Age 8+
 */
export const Age8: AgeStory = {
  render: () => <AgeBadge minAge={8} />,
};

/**
 * Age 10+
 */
export const Age10: AgeStory = {
  render: () => <AgeBadge minAge={10} />,
};

/**
 * Age 14+
 */
export const Age14: AgeStory = {
  render: () => <AgeBadge minAge={14} />,
};

/**
 * Small size
 */
export const AgeSmall: AgeStory = {
  render: () => <AgeBadge minAge={12} size="sm" />,
};

// ==================== Combined ====================

/**
 * All badges together (typical game info)
 */
export const AllBadgesTogether: PlayersStory = {
  render: () => (
    <div className="flex flex-wrap items-center gap-2">
      <PlayersBadge min={2} max={4} />
      <PlayTimeBadge minutes={90} />
      <ComplexityBadge rating={3.2} />
      <RatingBadge rating={8.1} />
      <AgeBadge minAge={12} />
    </div>
  ),
};

/**
 * All badges small size
 */
export const AllBadgesSmall: PlayersStory = {
  render: () => (
    <div className="flex flex-wrap items-center gap-2">
      <PlayersBadge min={2} max={4} size="sm" />
      <PlayTimeBadge minutes={90} size="sm" />
      <ComplexityBadge rating={3.2} size="sm" />
      <RatingBadge rating={8.1} size="sm" />
      <AgeBadge minAge={12} size="sm" />
    </div>
  ),
};

/**
 * Game card example
 */
export const GameCardExample: PlayersStory = {
  render: () => (
    <div className="w-80 rounded-lg border p-4 space-y-3">
      <h3 className="font-semibold text-lg">I Coloni di Catan</h3>
      <p className="text-sm text-muted-foreground line-clamp-2">
        Costruisci insediamenti, raccogli risorse e commercia con gli altri giocatori per diventare
        il dominatore dell&apos;isola di Catan.
      </p>
      <div className="flex flex-wrap items-center gap-2">
        <PlayersBadge min={3} max={4} size="sm" />
        <PlayTimeBadge minutes={75} size="sm" />
        <ComplexityBadge rating={2.3} size="sm" />
        <RatingBadge rating={7.1} size="sm" />
        <AgeBadge minAge={10} size="sm" />
      </div>
    </div>
  ),
};
