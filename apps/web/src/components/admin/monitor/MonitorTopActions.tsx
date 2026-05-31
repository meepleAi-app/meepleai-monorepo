'use client';

import { PauseCircle, Download } from 'lucide-react';

export function MonitorTopActions() {
  return (
    <div className="flex items-center gap-2">
      {/* Search ⌘K visual — read-only placeholder, functional binding deferred to Task 4.x */}
      <div className="hidden md:flex items-center gap-2 rounded-lg border border-border/60 bg-card px-3 py-1.5 text-sm text-muted-foreground w-[280px]">
        <span aria-hidden>🔍</span>
        <input
          className="flex-1 bg-transparent outline-none text-foreground placeholder:text-muted-foreground"
          placeholder="Search events, containers, logs…"
          aria-label="Search monitor"
          readOnly
          tabIndex={-1}
        />
        <kbd className="rounded border border-border/60 bg-muted px-1.5 font-mono text-[10px]">
          ⌘K
        </kbd>
      </div>

      {/* Pause stream — ghost button, non-functional placeholder (transport wiring: Task 4.x) */}
      <button
        type="button"
        aria-label="Pause stream"
        className="inline-flex items-center gap-1.5 rounded-md border border-border/60 bg-card px-3 py-1.5 text-sm font-semibold font-quicksand text-foreground hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
        onClick={() => {}}
      >
        <PauseCircle className="h-3.5 w-3.5" aria-hidden />
        Pause stream
      </button>

      {/* Export ndjson — ghost button, non-functional placeholder (transport wiring: Task 4.x) */}
      <button
        type="button"
        aria-label="Export ndjson"
        className="inline-flex items-center gap-1.5 rounded-md border border-border/60 bg-card px-3 py-1.5 text-sm font-semibold font-quicksand text-foreground hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
        onClick={() => {}}
      >
        <Download className="h-3.5 w-3.5" aria-hidden />
        Export ndjson
      </button>
    </div>
  );
}
