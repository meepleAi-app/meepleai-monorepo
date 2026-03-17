import { lazy } from 'react';

// Exploration mode zones
export const HeroZone = lazy(() => import('./HeroZone').then(m => ({ default: m.HeroZone })));
export const StatsZone = lazy(() => import('./StatsZone').then(m => ({ default: m.StatsZone })));
export const CardsZone = lazy(() => import('./CardsZone').then(m => ({ default: m.CardsZone })));
export const AgentsSidebar = lazy(() =>
  import('./AgentsSidebar').then(m => ({ default: m.AgentsSidebar }))
);

// Game mode zones
export const SessionBar = lazy(() => import('./SessionBar').then(m => ({ default: m.SessionBar })));
export const ScoreboardZone = lazy(() =>
  import('./ScoreboardZone').then(m => ({ default: m.ScoreboardZone }))
);
