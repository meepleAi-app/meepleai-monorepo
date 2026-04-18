'use client';

import React, { useState } from 'react';
import { ExternalLink, ChevronDown, ChevronUp } from 'lucide-react';
import type { InlineCitationMatch } from '@/lib/api/clients/chatClient';
import { cn } from '@/lib/utils';

interface SnippetData {
  text: string;
  source: string;
  page: number;
  line: number;
  score: number;
}

interface InlineCitationTextProps {
  text: string;
  citations: InlineCitationMatch[];
  snippets: SnippetData[];
}

export function InlineCitationText({ text, citations, snippets }: InlineCitationTextProps) {
  const [expandedCitations, setExpandedCitations] = useState<Set<number>>(new Set());

  if (citations.length === 0) {
    return <p className="whitespace-pre-wrap break-words">{text}</p>;
  }

  const sorted = [...citations].sort((a, b) => a.startOffset - b.startOffset);
  const segments: React.ReactNode[] = [];
  let lastEnd = 0;

  sorted.forEach((citation, idx) => {
    if (citation.startOffset > lastEnd) {
      segments.push(<span key={`text-${idx}`}>{text.slice(lastEnd, citation.startOffset)}</span>);
    }

    const citedText = text.slice(citation.startOffset, citation.endOffset);
    const isExpanded = expandedCitations.has(idx);
    const snippet = snippets[citation.snippetIndex];

    segments.push(
      <React.Fragment key={`cite-${idx}`}>
        <span
          className={cn(
            'bg-amber-500/10 border-l-2 border-amber-500 px-1 cursor-pointer',
            'hover:bg-amber-500/20 transition-colors inline'
          )}
          onClick={() => {
            setExpandedCitations(prev => {
              const next = new Set(prev);
              if (next.has(idx)) next.delete(idx);
              else next.add(idx);
              return next;
            });
          }}
          title={`Pagina ${citation.pageNumber}`}
          data-testid={`citation-highlight-${idx}`}
        >
          {citedText}
          <span className="inline-flex items-center ml-1 text-amber-600 dark:text-amber-400">
            {isExpanded ? <ChevronUp className="h-3 w-3 inline" /> : <ChevronDown className="h-3 w-3 inline" />}
          </span>
        </span>
        <a
          href={`/api/v1/pdfs/${citation.pdfDocumentId}/download#page=${citation.pageNumber}`}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center ml-0.5 text-amber-600 hover:text-amber-700 dark:text-amber-400 dark:hover:text-amber-300"
          data-testid={`pdf-link-${idx}`}
          title={`Apri PDF — Pagina ${citation.pageNumber}`}
        >
          <ExternalLink className="h-3 w-3" />
        </a>
        {isExpanded && snippet && (
          <div
            className="block my-2 p-3 rounded-lg bg-amber-500/5 border border-amber-500/20 text-sm"
            data-testid={`citation-accordion-${idx}`}
          >
            <div className="flex items-center gap-2 mb-1 text-xs text-amber-600 dark:text-amber-400 font-semibold font-nunito">
              <span>Pagina {citation.pageNumber}</span>
            </div>
            <p className="text-muted-foreground font-nunito whitespace-pre-wrap">{snippet.text}</p>
          </div>
        )}
      </React.Fragment>
    );
    lastEnd = citation.endOffset;
  });

  if (lastEnd < text.length) {
    segments.push(<span key="text-end">{text.slice(lastEnd)}</span>);
  }

  return <div className="whitespace-pre-wrap break-words">{segments}</div>;
}
