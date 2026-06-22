export type SyncStatus = 'running' | 'idle' | 'never_run';
export type LastRunStatus = 'Success' | 'Failed' | 'TimedOut' | null;
export type ChipState = 'running' | 'healthy' | 'degraded' | 'setup';

export function deriveChipState(status: SyncStatus, lastRunStatus: LastRunStatus): ChipState {
  if (status === 'running') return 'running';
  if (status === 'never_run') return 'setup';
  if (lastRunStatus === 'Failed' || lastRunStatus === 'TimedOut') return 'degraded';
  return 'healthy';
}

/**
 * Chip presentation matches mockup `.status-chip` (admin-base.css):
 *   - inline-flex, gap 4px, padding 2px 8px, rounded-full
 *   - font-mono 10px font-weight 700 uppercase letter-spacing .04em
 *   - 6px circle indicator before label (::before)
 * `toneClass` styles the chip wrapper (bg + text).
 * `dotClass` styles the indicator dot (bg + optional pulse halo).
 */
export const chipPresentation: Record<
  ChipState,
  { label: string; toneClass: string; dotClass: string }
> = {
  running: {
    label: 'Running',
    toneClass: 'bg-amber-500/12 text-amber-500',
    dotClass: 'bg-amber-500 animate-pulse',
  },
  healthy: {
    label: 'Idle',
    toneClass: 'bg-entity-toolkit/12 text-entity-toolkit',
    dotClass: 'bg-entity-toolkit ring-4 ring-entity-toolkit/25',
  },
  degraded: {
    label: 'Last sync failed',
    toneClass: 'bg-entity-event/12 text-entity-event',
    dotClass: 'bg-entity-event',
  },
  setup: {
    label: 'Setup',
    toneClass: 'bg-muted text-muted-foreground',
    dotClass: 'bg-muted-foreground',
  },
};
