'use client';

import type { SessionSummaryDto } from '@/lib/api/dashboard-client';

import { BentoWidget, WidgetLabel } from './BentoWidget';
import { C } from './dashboard-colors';

const MEDALS = ['🥇', '🥈', '🥉', '4️⃣'] as const;
const AVATAR_COLORS = [C.game, C.player, C.event, C.session] as const;

interface LeaderboardWidgetProps {
  sessions: SessionSummaryDto[];
}

export function LeaderboardWidget({ sessions }: LeaderboardWidgetProps) {
  const winners = sessions
    .filter(s => s.winnerName)
    .reduce<Record<string, number>>((acc, s) => {
      const key = s.winnerName ?? '';
      acc[key] = (acc[key] ?? 0) + 1;
      return acc;
    }, {});

  const sorted = Object.entries(winners)
    .sort(([, a], [, b]) => b - a)
    .slice(0, 4);

  return (
    <BentoWidget colSpan={6} rowSpan={3} accentColor={C.event} className="flex flex-col">
      <WidgetLabel>Classifica Gruppo (ultime partite)</WidgetLabel>
      <div className="flex-1 flex flex-col overflow-hidden">
        {sorted.length === 0 ? (
          <p className="text-[11px] text-muted-foreground mt-2">
            Gioca partite con amici per vedere la classifica
          </p>
        ) : (
          sorted.map(([name, wins], i) => (
            <div
              key={name}
              className="flex items-center gap-2 py-1.5 border-b border-border/40 last:border-0"
            >
              <span className="text-sm w-5 text-center shrink-0">{MEDALS[i]}</span>
              <div
                className="w-5 h-5 rounded-full flex items-center justify-center text-[9px] font-bold text-white shrink-0"
                style={{ background: AVATAR_COLORS[i] }}
              >
                {name[0]?.toUpperCase()}
              </div>
              <span className="flex-1 font-quicksand font-semibold text-[11px] truncate">
                {name}
              </span>
              <span className="font-mono text-[9px] text-muted-foreground shrink-0">
                {wins} vitt.
              </span>
            </div>
          ))
        )}
      </div>
    </BentoWidget>
  );
}
