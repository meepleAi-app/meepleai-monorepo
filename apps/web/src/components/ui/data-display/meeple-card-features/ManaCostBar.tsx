import { entityColors } from '../meeple-card-styles';

import type { MeepleEntityType } from '../meeple-card-styles';

interface ManaCostBarProps {
  relatedTypes: MeepleEntityType[];
}

export function ManaCostBar({ relatedTypes }: ManaCostBarProps) {
  if (relatedTypes.length === 0) return null;

  return (
    <div
      className="absolute right-3 top-2.5 z-[6] flex gap-[3px] rounded-[14px] border border-white/[0.08] bg-black/50 p-[3px_5px] backdrop-blur-[8px]"
      aria-label={`Related: ${relatedTypes.map(t => entityColors[t]?.name ?? t).join(', ')}`}
    >
      {relatedTypes.map(type => {
        const color = entityColors[type];
        if (!color) return null;
        return (
          <div
            key={type}
            data-mana-pip={type}
            className="h-4 w-4 rounded-full"
            style={{
              background: `radial-gradient(circle at 35% 35%, hsl(${color.hsl}), hsl(${color.hsl}))`,
              border: `1px solid hsla(${color.hsl}, 0.3)`,
              boxShadow: `0 1px 4px hsla(${color.hsl}, 0.25)`,
            }}
            title={color.name}
            aria-hidden="true"
          />
        );
      })}
    </div>
  );
}
