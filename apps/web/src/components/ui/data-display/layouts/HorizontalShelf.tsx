import { entityHsl, type MeepleEntityType } from '../meeple-card';

interface HorizontalShelfProps {
  entity: MeepleEntityType;
  title: string;
  count: number;
  children: React.ReactNode;
  className?: string;
}

export function HorizontalShelf({
  entity,
  title,
  count,
  children,
  className = '',
}: HorizontalShelfProps) {
  if (count < 2) return null;

  const colorStr = entityHsl(entity);
  if (!colorStr) return null;

  return (
    <div className={`mb-8 ${className}`}>
      <div className="mb-3 flex items-center gap-2">
        <div
          className="h-[18px] w-[18px] rounded-full"
          style={{
            background: `radial-gradient(circle at 35% 35%, ${colorStr}, ${colorStr})`,
            border: `1px solid ${entityHsl(entity, 0.3)}`,
          }}
          aria-hidden="true"
        />
        <span className="font-quicksand text-[13px] font-bold" style={{ color: colorStr }}>
          {title}
        </span>
        <span className="text-[10px] text-[var(--nh-text-muted,#555)]">{count}</span>
      </div>
      <div
        className="flex gap-4 overflow-x-auto pb-2 scroll-smooth [scroll-snap-type:x_mandatory] [-webkit-overflow-scrolling:touch]"
        role="list"
      >
        {children}
      </div>
    </div>
  );
}
