import { statusColors } from '../tokens';

import type { CardStatus } from '../types';

interface CardFooterProps {
  status?: CardStatus;
  badge?: string;
}

/**
 * Footer row for MeepleCardGrid following SP4 mockup
 * (`admin-mockups/design_files/sp4-library-desktop.jsx:736-745`).
 *
 * Renders a border-top divider, a `StatusDot` for the lifecycle color, and the
 * label (preferring `badge` over `status` when both are present). Renderless when
 * both inputs are absent — keeps the card body flush.
 *
 * See #1856 DEC-5.
 */
export function CardFooter({ status, badge }: CardFooterProps) {
  const label = badge ?? status;
  if (!label) return null;

  const dotColor = status
    ? (statusColors[status]?.text ?? 'var(--muted-foreground)')
    : 'var(--muted-foreground)';

  return (
    <div className="mt-1 flex items-center gap-1.5 border-t border-border-light px-3.5 py-1.5">
      {status && (
        <span
          data-slot="footer-status-dot"
          aria-hidden="true"
          className="inline-block h-1.5 w-1.5 shrink-0 rounded-full"
          style={{ background: dotColor }}
        />
      )}
      <span
        data-slot="footer-badge"
        className="font-mono text-[9.5px] font-bold uppercase tracking-wide text-muted-foreground"
      >
        {label}
      </span>
    </div>
  );
}
