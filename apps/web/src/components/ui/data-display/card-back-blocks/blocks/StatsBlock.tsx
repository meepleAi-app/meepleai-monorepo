import { memo } from 'react';

interface StatsBlockProps {
  title: string;
  entityColor: string;
  data: { type: 'stats'; entries: Array<{ label: string; value: string | number; icon?: string }> };
}

export const StatsBlock = memo(function StatsBlock({ title, entityColor, data }: StatsBlockProps) {
  return (
    <div className="px-4 py-2">
      <h4
        className="text-[11px] font-bold uppercase tracking-wider mb-1.5"
        style={{ color: `hsl(${entityColor})` }}
      >
        {title}
      </h4>
      <div className="border-t border-white/5 pt-1.5">
        {data.entries.length === 0 ? (
          <p className="text-xs text-slate-500">No data yet</p>
        ) : (
          data.entries.map(entry => (
            <div key={entry.label} className="flex justify-between items-center py-0.5 text-xs">
              <span className="text-slate-400 flex items-center gap-1">
                {entry.icon && <span>{entry.icon}</span>}
                {entry.label}
              </span>
              <span className="text-slate-300 font-medium">{entry.value}</span>
            </div>
          ))
        )}
      </div>
    </div>
  );
});
