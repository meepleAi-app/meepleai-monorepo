'use client';

import type { SessionSummaryDto } from '@/lib/api/dashboard-client';
import { cn } from '@/lib/utils';

interface ActiveSessionsProps {
  sessions?: SessionSummaryDto[];
  loading?: boolean;
  className?: string;
}

export function ActiveSessions({ sessions, loading = false, className }: ActiveSessionsProps) {
  return (
    <div data-testid="active-sessions" className={cn('flex flex-col gap-3', className)}>
      <h2 className="font-quicksand font-bold text-sm uppercase tracking-wide">
        🎮 Sessioni Attive
      </h2>

      {loading ? (
        <div className="h-[100px] rounded-[14px] bg-[rgba(200,180,160,0.20)] animate-pulse" />
      ) : !sessions || sessions.length === 0 ? (
        <div className="border-[1.5px] border-dashed border-border rounded-[14px] p-7 text-center bg-[rgba(255,255,255,0.4)]">
          <p className="text-muted-foreground text-sm">Nessuna partita in corso</p>
          <p className="text-primary text-[13px] font-semibold mt-2 cursor-pointer">
            Inizia a giocare →
          </p>
        </div>
      ) : (
        <div>Session cards TBD</div>
      )}
    </div>
  );
}
