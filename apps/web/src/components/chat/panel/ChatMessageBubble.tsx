'use client';

import { type ReactNode } from 'react';

import { cn } from '@/lib/utils';

import { CitationExpander } from './CitationExpander';

export type ChatMessageRole = 'user' | 'assistant';

interface ChatMessageBubbleProps {
  role: ChatMessageRole;
  content: string;
  authorName: string;
  timestamp: string;
  imageUrls?: string[];
}

function initials(name: string): string {
  return name
    .split(/\s+/)
    .map(part => part[0] ?? '')
    .join('')
    .slice(0, 2)
    .toUpperCase();
}

// eslint-disable-next-line security/detect-unsafe-regex -- bounded by \] terminator, no catastrophic backtracking risk
const CITATION_REGEX = /\[cite:([a-f0-9-]+):(\d+)(?::([^\]]{1,200}))?\]/g;

/**
 * Parse content for citation markers and replace with CitationExpander components.
 * Markers have the format: [cite:pdfId:pageNumber] or [cite:pdfId:pageNumber:docName]
 */
function renderContentWithCitations(content: string): ReactNode {
  const parts: ReactNode[] = [];
  let lastIndex = 0;
  let match: RegExpExecArray | null;
  let key = 0;

  // Reset regex state
  CITATION_REGEX.lastIndex = 0;

  while ((match = CITATION_REGEX.exec(content)) !== null) {
    // Add text before the citation
    if (match.index > lastIndex) {
      parts.push(content.slice(lastIndex, match.index));
    }

    const [, pdfId, pageStr, docName] = match;
    parts.push(
      <CitationExpander
        key={`cite-${key++}`}
        pdfId={pdfId}
        pageNumber={parseInt(pageStr, 10)}
        docName={docName}
      />
    );

    lastIndex = match.index + match[0].length;
  }

  // Add remaining text
  if (lastIndex < content.length) {
    parts.push(content.slice(lastIndex));
  }

  // No citations found — return plain string
  if (parts.length === 0) {
    return content;
  }

  return parts;
}

export function ChatMessageBubble({
  role,
  content,
  authorName,
  timestamp,
  imageUrls,
}: ChatMessageBubbleProps) {
  const isUser = role === 'user';

  return (
    <div
      data-testid="chat-message-bubble"
      data-role={role}
      className={cn(
        'flex gap-3',
        isUser ? 'max-w-[78%] flex-row-reverse self-end' : 'max-w-[92%] self-start'
      )}
    >
      <div
        aria-hidden
        className="flex h-[34px] w-[34px] flex-shrink-0 items-center justify-center rounded-full text-base text-white shadow-[var(--shadow-warm-sm)]"
        style={{
          background: isUser
            ? 'linear-gradient(135deg, hsl(262 83% 68%), hsl(262 83% 48%))'
            : 'linear-gradient(135deg, hsl(38 92% 60%), hsl(25 95% 48%))',
          fontFamily: isUser ? 'var(--font-quicksand)' : 'inherit',
          fontWeight: isUser ? 800 : undefined,
          fontSize: isUser ? '12px' : undefined,
        }}
      >
        {isUser ? initials(authorName) : '🎲'}
      </div>
      <div className={cn('flex min-w-0 flex-1 flex-col gap-2', isUser && 'items-end')}>
        <div className="flex items-center gap-2 text-[0.66rem] font-semibold text-[var(--nh-text-muted)]">
          <span className="font-quicksand font-extrabold text-[var(--nh-text-secondary)]">
            {authorName}
          </span>
          <span>· {timestamp}</span>
        </div>
        <div
          className={cn(
            'rounded-2xl px-4 py-3.5 font-nunito text-[0.88rem] leading-relaxed',
            isUser
              ? 'rounded-tr-md'
              : 'rounded-tl-md border border-[var(--nh-border-default)] bg-[var(--nh-bg-elevated)] text-[var(--nh-text-primary)] shadow-[var(--shadow-warm-sm)]'
          )}
          style={
            isUser
              ? {
                  background: 'linear-gradient(135deg, hsl(25 95% 94%), hsl(25 95% 88%))',
                  border: '1px solid hsla(25, 95%, 45%, 0.2)',
                  color: 'hsl(25 60% 18%)',
                }
              : undefined
          }
        >
          {imageUrls && imageUrls.length > 0 && (
            <div className="mb-2 flex flex-wrap gap-2">
              {imageUrls.map((url, idx) => (
                <a key={idx} href={url} target="_blank" rel="noopener noreferrer" className="block">
                  <img
                    src={url}
                    alt={`Immagine ${idx + 1}`}
                    className="h-15 w-20 cursor-pointer rounded-md border border-white/10 object-cover transition-opacity hover:opacity-80"
                  />
                </a>
              ))}
            </div>
          )}
          <div className="whitespace-pre-wrap">{renderContentWithCitations(content)}</div>
        </div>
      </div>
    </div>
  );
}
