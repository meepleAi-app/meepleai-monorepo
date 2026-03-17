'use client';

import { memo } from 'react';

import type { MeepleEntityType } from '../../meeple-card-styles';

interface ContentItem {
  title: string;
  entityType: MeepleEntityType;
  id: string;
  status?: string;
}

interface ContentsBlockProps {
  title: string;
  entityColor: string;
  data: {
    type: 'contents';
    items: ContentItem[];
  };
}

/** Compact entity type color dots instead of full ManaSymbol to avoid circular deps */
const ENTITY_COLORS: Partial<Record<MeepleEntityType, string>> = {
  game: '25 95% 45%',
  player: '262 83% 58%',
  session: '240 60% 55%',
  agent: '38 92% 50%',
  kb: '174 60% 40%',
  chatSession: '220 80% 55%',
  event: '350 89% 60%',
  toolkit: '142 70% 45%',
  tool: '195 80% 50%',
  collection: '20 70% 42%',
  group: '280 50% 48%',
  location: '200 55% 45%',
  expansion: '290 65% 50%',
  achievement: '45 90% 48%',
  note: '40 30% 42%',
  custom: '220 15% 45%',
};

export const ContentsBlock = memo(function ContentsBlock({
  title,
  entityColor,
  data,
}: ContentsBlockProps) {
  const { items } = data;

  return (
    <div className="flex flex-col gap-2">
      {title && (
        <>
          <h4
            className="text-xs font-semibold uppercase tracking-wide"
            style={{ color: `hsl(${entityColor})` }}
          >
            {title}
          </h4>
          <div className="h-px w-full" style={{ backgroundColor: `hsl(${entityColor} / 0.2)` }} />
        </>
      )}

      {items.length === 0 ? (
        <p className="text-xs text-muted-foreground italic">No data yet</p>
      ) : (
        <ul className="flex flex-col gap-1">
          {items.map((item) => {
            const dotColor = ENTITY_COLORS[item.entityType] ?? entityColor;
            return (
              <li key={item.id} className="flex items-center gap-2 text-xs">
                <span
                  className="h-2 w-2 shrink-0 rounded-full"
                  style={{ backgroundColor: `hsl(${dotColor})` }}
                  aria-label={item.entityType}
                />
                <span className="flex-1 truncate text-foreground">{item.title}</span>
                {item.status && (
                  <span className="shrink-0 text-[10px] capitalize text-muted-foreground">
                    {item.status}
                  </span>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
});

ContentsBlock.displayName = 'ContentsBlock';
