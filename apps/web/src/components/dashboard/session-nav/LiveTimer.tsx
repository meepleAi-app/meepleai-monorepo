'use client';

import React, { useEffect, useState } from 'react';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function formatElapsed(startedAt: Date, now: Date): string {
  const totalSeconds = Math.max(0, Math.floor((now.getTime() - startedAt.getTime()) / 1000));
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;

  const pad = (n: number) => String(n).padStart(2, '0');
  return `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
}

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

interface LiveTimerProps {
  startedAt: Date;
  isPaused: boolean;
  onClick?: () => void;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function LiveTimer({ startedAt, isPaused, onClick }: LiveTimerProps): React.JSX.Element {
  const [now, setNow] = useState<Date>(() => new Date());

  useEffect(() => {
    if (isPaused) return;

    const id = setInterval(() => {
      setNow(new Date());
    }, 1000);

    return () => clearInterval(id);
  }, [isPaused]);

  const display = formatElapsed(startedAt, now);

  return (
    <button
      type="button"
      onClick={onClick}
      className="font-mono text-sm text-green-500 tabular-nums hover:text-green-400 transition-colors"
      aria-label={`Session timer: ${display}`}
      data-testid="live-timer"
    >
      {display}
    </button>
  );
}
