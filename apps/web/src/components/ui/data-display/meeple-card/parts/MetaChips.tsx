import type { MeepleCardMetadata } from '../types';

interface MetaChipsProps {
  metadata: MeepleCardMetadata[];
}

export function MetaChips({ metadata }: MetaChipsProps) {
  if (metadata.length === 0) return null;

  return (
    <div className="mt-1 flex flex-wrap gap-1.5">
      {metadata.map((m, i) => (
        <span
          key={i}
          className="flex items-center gap-[3px] rounded-md bg-[var(--mc-bg-muted)] px-2 py-0.5 text-[10px] text-[var(--mc-text-secondary)]"
        >
          {m.icon && <span className="text-[11px]">{m.icon}</span>}
          {m.label}
          {m.value && <span className="font-semibold">{m.value}</span>}
        </span>
      ))}
    </div>
  );
}
