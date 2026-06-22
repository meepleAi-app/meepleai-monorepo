import type { JSX } from 'react';

export type StrengthLabels = readonly [string, string, string, string, string];

export interface StrengthMeterProps {
  readonly value: string;
  /** Labels for scores 0-4. Defaults to Italian ("Debole", "Debole", "Discreta", "Buona", "Ottima"). */
  readonly labels?: StrengthLabels;
  /** Prefix shown before the label (e.g. "Password:"). Empty string omits the prefix. */
  readonly prefix?: string;
}

const DEFAULT_LABELS: StrengthLabels = ['Debole', 'Debole', 'Discreta', 'Buona', 'Ottima'];
const COLORS = [
  'hsl(var(--c-danger))',
  'hsl(var(--c-danger))',
  'hsl(var(--c-event))',
  'hsl(var(--c-game))',
  'hsl(var(--c-toolkit))',
] as const;

export function computeScore(password: string): number {
  if (password.length === 0) return 0;
  let score = 1;
  if (password.length >= 6) score++;
  if (/[A-Z]/.test(password) && /[a-z]/.test(password)) score++;
  if (/\d/.test(password)) score++;
  if (/[^A-Za-z0-9]/.test(password)) score++;
  return Math.min(score, 4);
}

export function StrengthMeter({
  value,
  labels = DEFAULT_LABELS,
  prefix = 'Password:',
}: StrengthMeterProps): JSX.Element {
  const score = computeScore(value);
  const label = labels[score];
  const color = COLORS[score];

  return (
    <div role="status" aria-live="polite" className="flex flex-col gap-1">
      <div className="flex gap-1" aria-hidden="true">
        {[0, 1, 2, 3].map(i => (
          <div
            key={i}
            data-segment={i}
            className="h-1 flex-1 rounded-full transition-colors motion-reduce:transition-none"
            style={{ background: i < score ? color : 'hsl(var(--border))' }}
          />
        ))}
      </div>
      <span className="text-xs text-muted-foreground font-body">
        {prefix ? `${prefix} ` : ''}
        <span style={{ color }}>{label}</span>
      </span>
    </div>
  );
}
