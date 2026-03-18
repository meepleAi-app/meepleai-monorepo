'use client';

import { FileText, Hash, ChevronDown, ChevronUp } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import type { RetrievedChunk } from './types';

interface RetrievedChunkCardProps {
  chunk: RetrievedChunk;
  expanded?: boolean;
  onToggleExpand?: () => void;
}

function getScoreColor(score: number): string {
  if (score > 0.7) return 'bg-green-500';
  if (score >= 0.4) return 'bg-yellow-500';
  return 'bg-red-500';
}

function getScoreBarBg(score: number): string {
  if (score > 0.7) return 'bg-green-100';
  if (score >= 0.4) return 'bg-yellow-100';
  return 'bg-red-100';
}

export function RetrievedChunkCard({
  chunk,
  expanded = false,
  onToggleExpand,
}: RetrievedChunkCardProps) {
  return (
    <div
      className="rounded-lg border bg-white p-3 transition-all hover:shadow-sm"
      data-testid="chunk-card"
    >
      {/* Score bar + numeric */}
      <div className="flex items-center gap-2 mb-2">
        <div className={`h-2 flex-1 rounded-full ${getScoreBarBg(chunk.score)}`}>
          <div
            className={`h-full rounded-full transition-all ${getScoreColor(chunk.score)}`}
            style={{ width: `${Math.round(chunk.score * 100)}%` }}
            data-testid="score-bar"
          />
        </div>
        <span className="font-nunito text-xs font-medium tabular-nums" data-testid="score-value">
          {chunk.score.toFixed(2)}
        </span>
        {chunk.used && (
          <Badge
            variant="outline"
            className="border-green-300 bg-green-50 text-green-700 text-[10px] px-1.5 py-0"
            data-testid="used-badge"
          >
            Usato
          </Badge>
        )}
      </div>

      {/* Text preview */}
      <div className="cursor-pointer" onClick={onToggleExpand} data-testid="chunk-text-area">
        <p
          className={`font-nunito text-xs text-foreground/80 ${expanded ? '' : 'line-clamp-2'}`}
          data-testid="chunk-text"
        >
          {chunk.text}
        </p>
        {onToggleExpand && (
          <button
            className="mt-1 flex items-center gap-0.5 text-[10px] text-muted-foreground hover:text-foreground"
            data-testid="expand-toggle"
          >
            {expanded ? (
              <>
                <ChevronUp className="h-3 w-3" /> Comprimi
              </>
            ) : (
              <>
                <ChevronDown className="h-3 w-3" /> Espandi
              </>
            )}
          </button>
        )}
      </div>

      {/* Source info */}
      <div className="mt-2 flex items-center gap-3 text-[10px] text-muted-foreground">
        <span className="flex items-center gap-1">
          <FileText className="h-3 w-3" />
          {chunk.pdfName}
        </span>
        <span className="flex items-center gap-1">p.{chunk.page}</span>
        <span className="flex items-center gap-1">
          <Hash className="h-3 w-3" />
          {chunk.chunkIndex}
        </span>
      </div>
    </div>
  );
}
