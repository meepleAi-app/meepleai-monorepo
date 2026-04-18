'use client';

import React, { useState } from 'react';
import { FileText, ExternalLink, ChevronDown, ChevronUp } from 'lucide-react';
import { cn } from '@/lib/utils';

interface SnippetData {
  text: string;
  source: string;
  page: number;
  line: number;
  score: number;
}

interface CitationBlockProps {
  snippets: SnippetData[];
  excludeIndices: Set<number>;
}

function extractPdfId(source: string): string {
  return source.startsWith('PDF:') ? source.slice(4) : source;
}

export function CitationBlock({ snippets, excludeIndices }: CitationBlockProps) {
  const [expanded, setExpanded] = useState<number | null>(null);

  const visible = snippets
    .map((s, i) => ({ ...s, originalIndex: i }))
    .filter(s => !excludeIndices.has(s.originalIndex));

  if (visible.length === 0) return null;

  return (
    <div className="mt-3 flex flex-wrap gap-2" data-testid="citation-block">
      {visible.map((snippet) => {
        const pdfId = extractPdfId(snippet.source);
        const isExpanded = expanded === snippet.originalIndex;

        return (
          <div key={snippet.originalIndex} className="flex flex-col">
            <div className="flex items-center gap-1">
              <button
                onClick={() => setExpanded(isExpanded ? null : snippet.originalIndex)}
                className={cn(
                  'inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-nunito font-medium transition-all',
                  'border border-amber-500/30 hover:border-amber-500/60',
                  isExpanded
                    ? 'bg-amber-500/15 text-amber-700 dark:text-amber-300'
                    : 'bg-amber-500/5 text-amber-600 dark:text-amber-400 hover:bg-amber-500/10'
                )}
                data-testid={`citation-chip-${snippet.originalIndex}`}
              >
                <FileText className="h-3 w-3" />
                Pagina {snippet.page}
                {isExpanded ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />}
              </button>
              <a
                href={`/api/v1/pdfs/${pdfId}/download#page=${snippet.page}`}
                target="_blank"
                rel="noopener noreferrer"
                className="text-amber-600 hover:text-amber-700 dark:text-amber-400"
                title={`Apri PDF — Pagina ${snippet.page}`}
                data-testid={`citation-pdf-link-${snippet.originalIndex}`}
              >
                <ExternalLink className="h-3 w-3" />
              </a>
            </div>
            {isExpanded && (
              <div className="mt-1.5 p-3 rounded-lg bg-amber-500/5 border border-amber-500/20 text-sm max-w-md" data-testid={`citation-expanded-${snippet.originalIndex}`}>
                <p className="text-muted-foreground font-nunito whitespace-pre-wrap">{snippet.text}</p>
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
