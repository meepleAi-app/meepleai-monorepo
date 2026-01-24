/**
 * Stats Grid Component (Issue #2833)
 *
 * 4-metric grid display for game statistics
 */

interface StatsGridProps {
  timesPlayed: number;
  winRate?: string | null;
  avgDuration?: string | null;
  lastPlayed?: string | null;
}

export function StatsGrid({ timesPlayed, winRate, avgDuration, lastPlayed }: StatsGridProps) {
  const stats = [
    {
      label: 'Times Played',
      value: timesPlayed.toString(),
      icon: '🎲',
    },
    {
      label: 'Win Rate',
      value: winRate ?? 'N/A',
      icon: '🏆',
    },
    {
      label: 'Avg Duration',
      value: avgDuration ?? 'N/A',
      icon: '⏱️',
    },
    {
      label: 'Last Played',
      value: lastPlayed ? new Date(lastPlayed).toLocaleDateString() : 'Never',
      icon: '📅',
    },
  ];

  return (
    <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
      {stats.map((stat) => (
        <div
          key={stat.label}
          className="bg-white dark:bg-neutral-800 rounded-lg p-4 border border-neutral-200 dark:border-neutral-700"
        >
          <div className="flex items-center gap-2 mb-2">
            <span className="text-2xl" aria-hidden="true">
              {stat.icon}
            </span>
            <span className="text-sm text-neutral-600 dark:text-neutral-400">{stat.label}</span>
          </div>
          <p className="text-2xl font-bold text-neutral-900 dark:text-neutral-50">{stat.value}</p>
        </div>
      ))}
    </div>
  );
}
