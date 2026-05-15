/**
 * Barrel for /game-nights v2 components (SP4 #1170 commit 2).
 *
 * Components are pure-presentational; orchestrator (Commit 3) resolves
 * i18n via `useTranslations` and passes labels through props.
 */

export { CalendarDayCell } from './CalendarDayCell';
export type { CalendarDayCellLabels, CalendarDayCellProps } from './CalendarDayCell';

export { CalendarMonthGrid } from './CalendarMonthGrid';
export type { CalendarMonthGridLabels, CalendarMonthGridProps } from './CalendarMonthGrid';

export { DayDetailDrawer } from './DayDetailDrawer';
export type { DayDetailDrawerLabels, DayDetailDrawerProps } from './DayDetailDrawer';

export { FilterPillBar } from './FilterPillBar';
export type { FilterPillBarLabels, FilterPillBarProps } from './FilterPillBar';

export { GameNightListCard } from './GameNightListCard';
export type {
  GameNightListCardAction,
  GameNightListCardCtaLabels,
  GameNightListCardLabels,
  GameNightListCardProps,
} from './GameNightListCard';

export { GameNightsHeader } from './GameNightsHeader';
export type {
  GameNightsHeaderLabels,
  GameNightsHeaderProps,
  GameNightsView,
} from './GameNightsHeader';

export { PlayerAvatars } from './PlayerAvatars';
export type { AvatarPlayer, PlayerAvatarsProps } from './PlayerAvatars';

export { StatusPill } from './StatusPill';
export type { StatusPillLabels, StatusPillProps } from './StatusPill';
