/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 36)
 *
 * Badge displaying the first 8 chars of the golden-set version hash with a
 * copy-to-clipboard affordance. The full hash is shown on hover via Tooltip.
 *
 * The version hash is a stable digest of the curated golden set used by the
 * matching engine to detect drift between certified analyses and the current
 * curator state. Surfacing it in the admin UI lets curators verify they're
 * editing the expected version.
 */

'use client';

import { CopyIcon } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';

export interface GoldenVersionHashBadgeProps {
  /** Full version hash (any length — first 8 chars are shown). */
  hash: string;
}

export function GoldenVersionHashBadge({ hash }: GoldenVersionHashBadgeProps) {
  const truncated = hash.slice(0, 8) || '—';

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(hash);
      toast.success('Hash copied to clipboard');
    } catch {
      toast.error('Failed to copy hash');
    }
  };

  return (
    <TooltipProvider delayDuration={150}>
      <Tooltip>
        <TooltipTrigger asChild>
          <span className="inline-flex items-center gap-1" data-testid="golden-version-hash">
            <Badge variant="outline" className="font-mono text-xs">
              {truncated}
            </Badge>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              aria-label="Copy version hash"
              className="h-6 w-6"
              onClick={handleCopy}
            >
              <CopyIcon className="h-3 w-3" />
            </Button>
          </span>
        </TooltipTrigger>
        <TooltipContent>
          <code className="font-mono text-xs">{hash || 'No hash available'}</code>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
