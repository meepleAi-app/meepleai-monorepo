import { Camera, Zap } from 'lucide-react';
import Link from 'next/link';

import { Badge } from '@/components/ui/data-display/badge';
import type { ComponentEntry } from '@/config/component-registry';

interface ComponentCardProps {
  entry: ComponentEntry;
}

export function ComponentCard({ entry }: ComponentCardProps) {
  const isInteractive = entry.tier === 'interactive';

  return (
    <Link
      href={`/admin/ui-library/${entry.id}`}
      className="group flex flex-col rounded-xl border border-border/50 bg-card/50 backdrop-blur-sm p-4 transition-all duration-200 hover:border-amber-300/50 hover:shadow-md hover:bg-card/70 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      {/* Header row */}
      <div className="mb-2 flex items-start justify-between gap-2">
        <h3 className="font-quicksand text-sm font-semibold text-foreground leading-tight group-hover:text-amber-900 dark:group-hover:text-amber-300 transition-colors">
          {entry.name}
        </h3>
        <span
          title={isInteractive ? 'Interactive' : 'Static'}
          className="shrink-0 text-muted-foreground"
        >
          {isInteractive ? (
            <Zap className="h-3.5 w-3.5 text-amber-500" aria-label="Interactive" />
          ) : (
            <Camera className="h-3.5 w-3.5 text-slate-400" aria-label="Static" />
          )}
        </span>
      </div>

      {/* Description */}
      <p className="mb-3 font-nunito text-xs text-muted-foreground line-clamp-2 flex-1">
        {entry.description}
      </p>

      {/* Footer badges */}
      <div className="flex flex-wrap gap-1.5">
        <Badge
          variant="secondary"
          className="text-[10px] px-1.5 py-0 h-5 bg-amber-50 text-amber-800 border-amber-200 dark:bg-amber-950/30 dark:text-amber-300 dark:border-amber-800/50"
        >
          {entry.category}
        </Badge>
        {entry.areas.map(area => (
          <Badge
            key={area}
            variant="outline"
            className="text-[10px] px-1.5 py-0 h-5 text-muted-foreground"
          >
            {area}
          </Badge>
        ))}
      </div>
    </Link>
  );
}
