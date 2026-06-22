/**
 * EnvPill (Issue #1836)
 *
 * Small static pill that surfaces the environment scope of a configuration
 * (development, staging, production). The backend does not yet expose a
 * per-flag `environment` field, so for now callers hardcode `env="prd"`.
 *
 * Once the backend lands a per-config env scope we will swap the prop to be
 * data-driven (see [[project_issue_1836_config_flags_wip]]).
 */

import { cn } from '@/lib/utils';

export type EnvKind = 'dev' | 'stg' | 'prd';

export interface EnvPillProps {
  env: EnvKind;
  className?: string;
  /**
   * Compact display omits the "env:" prefix. Defaults to `false`.
   */
  compact?: boolean;
}

const ENV_LABEL: Record<EnvKind, string> = {
  dev: 'dev',
  stg: 'stg',
  prd: 'prd',
};

const ENV_TONE: Record<EnvKind, string> = {
  dev: 'bg-[hsl(var(--c-info)/0.12)] text-[hsl(var(--c-info))] border-[hsl(var(--c-info)/0.30)]',
  stg: 'bg-[hsl(var(--c-warning)/0.15)] text-[hsl(var(--c-warning))] border-[hsl(var(--c-warning)/0.35)]',
  prd: 'bg-[hsl(var(--c-success)/0.12)] text-[hsl(var(--c-success))] border-[hsl(var(--c-success)/0.30)]',
};

export function EnvPill({ env, className, compact = false }: EnvPillProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[10px] font-mono font-semibold uppercase tracking-wide tabular-nums',
        ENV_TONE[env],
        className
      )}
      data-testid={`env-pill-${env}`}
      aria-label={`Environment scope: ${ENV_LABEL[env]}`}
    >
      {!compact && <span aria-hidden="true">env</span>}
      <span>{ENV_LABEL[env]}</span>
    </span>
  );
}

EnvPill.displayName = 'EnvPill';

export default EnvPill;
