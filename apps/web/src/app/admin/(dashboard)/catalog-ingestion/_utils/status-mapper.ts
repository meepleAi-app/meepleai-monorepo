export type SyncStatus = 'running' | 'idle' | 'never_run';
export type LastRunStatus = 'Success' | 'Failed' | 'TimedOut' | null;
export type ChipState = 'running' | 'healthy' | 'degraded' | 'setup';

export function deriveChipState(status: SyncStatus, lastRunStatus: LastRunStatus): ChipState {
  if (status === 'running') return 'running';
  if (status === 'never_run') return 'setup';
  if (lastRunStatus === 'Failed' || lastRunStatus === 'TimedOut') return 'degraded';
  return 'healthy';
}

export const chipPresentation: Record<ChipState, { label: string; toneClass: string }> = {
  running: {
    label: 'Running',
    toneClass: 'bg-amber-500/15 text-amber-500 ring-amber-500/30',
  },
  healthy: {
    label: 'Idle',
    toneClass: 'bg-toolkit/15 text-toolkit ring-toolkit/30',
  },
  degraded: {
    label: 'Last sync failed',
    toneClass: 'bg-event/15 text-event ring-event/30',
  },
  setup: {
    label: 'Setup',
    toneClass: 'bg-muted/40 text-muted-foreground ring-border',
  },
};
