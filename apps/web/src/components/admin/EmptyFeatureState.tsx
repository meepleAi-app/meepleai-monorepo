import { Construction, type LucideIcon } from 'lucide-react';

interface EmptyFeatureStateProps {
  icon?: LucideIcon;
  title: string;
  description: string;
  issueNumber?: number;
  issueLabel?: string;
  className?: string;
}

export function EmptyFeatureState({
  icon: Icon = Construction,
  title,
  description,
  issueNumber,
  issueLabel,
  className,
}: EmptyFeatureStateProps) {
  return (
    <div
      className={`rounded-lg border border-dashed border-border/40 p-8 text-center ${className ?? ''}`}
    >
      <Icon className="mx-auto h-8 w-8 text-muted-foreground/50" />
      <p className="mt-3 text-sm font-medium text-foreground">{title}</p>
      <p className="mt-1 text-xs text-muted-foreground">{description}</p>
      {issueNumber && (
        <a
          href={`https://github.com/meepleAi-app/meepleai-monorepo/issues/${issueNumber}`}
          target="_blank"
          rel="noopener noreferrer"
          className="mt-3 inline-block text-xs text-muted-foreground underline hover:text-foreground"
        >
          {issueLabel ?? `Tracked in issue #${issueNumber}`}
        </a>
      )}
    </div>
  );
}
