'use client';

export interface GreetingStat {
  label: string;
  value: string;
}

interface GreetingRowProps {
  displayName: string;
  subtitle: string;
  stats: GreetingStat[];
}

/**
 * Phase 2 dashboard greeting row: "Ciao {name}" with gradient wordmark on the name,
 * subtitle underneath, and a right-aligned cluster of quick stats.
 */
export function GreetingRow({ displayName, subtitle, stats }: GreetingRowProps) {
  return (
    <div className="mb-6 flex items-end justify-between gap-6">
      <div>
        <h1 className="font-quicksand text-[1.8rem] font-extrabold leading-tight text-[var(--nh-text-primary)]">
          Ciao{' '}
          <span
            className="bg-clip-text text-transparent"
            style={{
              backgroundImage: 'linear-gradient(135deg, hsl(25 95% 48%), hsl(38 92% 55%))',
            }}
          >
            {displayName}
          </span>{' '}
          <span aria-hidden>👋</span>
        </h1>
        <p className="mt-1 text-sm text-[var(--nh-text-muted)]">{subtitle}</p>
      </div>
      {stats.length > 0 && (
        <div data-testid="greet-stats" className="flex gap-5">
          {stats.map(stat => (
            <div key={stat.label} className="text-right">
              <div className="font-quicksand text-[1.35rem] font-extrabold leading-none text-[var(--nh-text-primary)]">
                {stat.value}
              </div>
              <div className="mt-1 text-[0.68rem] font-bold uppercase tracking-wider text-[var(--nh-text-muted)]">
                {stat.label}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
