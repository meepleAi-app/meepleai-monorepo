'use client';

import { DashboardStatCard } from './DashboardStatCard';

export interface DashboardStatState {
  value: number;
  isLoading: boolean;
  isError: boolean;
  isFetching: boolean;
}

export interface DashboardStatsRowProps {
  stats: {
    games: DashboardStatState;
    sessions: DashboardStatState;
    agents: DashboardStatState;
    events: DashboardStatState;
  };
  onRetry: Partial<{
    games: () => void;
    sessions: () => void;
    agents: () => void;
    events: () => void;
  }>;
}

const STAT_ENTRIES = [
  { key: 'games' as const, entity: 'game' as const, label: 'Giochi', href: '/library' },
  { key: 'sessions' as const, entity: 'session' as const, label: 'Sessioni', href: '/sessions' },
  { key: 'agents' as const, entity: 'agent' as const, label: 'Agenti', href: '/agents' },
  { key: 'events' as const, entity: 'event' as const, label: 'Eventi', href: '/game-nights' },
];

const ERROR_THRESHOLD = 3;

export function DashboardStatsRow({ stats, onRetry }: DashboardStatsRowProps) {
  const errorCount = STAT_ENTRIES.filter(e => stats[e.key].isError).length;
  const showBanner = errorCount >= ERROR_THRESHOLD;

  const handleRetryAll = () => {
    STAT_ENTRIES.forEach(({ key }) => {
      if (stats[key].isError) {
        onRetry[key]?.();
      }
    });
  };

  return (
    <nav aria-label="Statistiche personali" className="mb-12">
      {showBanner && (
        <div
          role="status"
          className="mb-3 flex items-center justify-between rounded-lg border border-border bg-muted px-4 py-2 text-sm"
        >
          <span className="font-medium text-foreground">
            Connessione instabile — alcuni dati non sono disponibili
          </span>
          <button
            type="button"
            onClick={handleRetryAll}
            className="rounded-full bg-foreground/10 px-3 py-1 text-xs font-bold text-foreground hover:bg-foreground/20"
          >
            Riprova tutto
          </button>
        </div>
      )}
      <div className="grid grid-cols-1 gap-4 min-[480px]:grid-cols-2 min-[800px]:grid-cols-4">
        {STAT_ENTRIES.map(({ key, entity, label, href }) => {
          const state = stats[key];
          return (
            <DashboardStatCard
              key={key}
              entity={entity}
              value={state.value}
              label={label}
              href={href}
              isLoading={state.isLoading}
              isError={state.isError}
              isFetching={state.isFetching}
              onRetry={onRetry[key]}
            />
          );
        })}
      </div>
    </nav>
  );
}
