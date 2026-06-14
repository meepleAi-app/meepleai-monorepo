/**
 * MarkdownRenderBlock — primitive markdown renderer for the KB preview pane
 * (#2311 FE-3 DEC-D4). Reusable across KB chunks, Reviews tab, Strategies tab.
 *
 * Plugin allowlist (DEC-D4):
 *   - remark-gfm: enables GFM tables (the mockup fixture uses `| col | col |`)
 *   - NO rehype-raw: prevents XSS via inline HTML in chunk content
 *
 * The component normalises typography to the token-canonical surface (no
 * hardcoded greys / colours) so consumers don't need a per-instance reset.
 */

'use client';

import { type ReactElement } from 'react';

import clsx from 'clsx';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

export interface MarkdownRenderBlockProps {
  readonly content: string;
  readonly className?: string;
}

export function MarkdownRenderBlock({
  content,
  className,
}: MarkdownRenderBlockProps): ReactElement {
  return (
    <div
      data-slot="markdown-render-block"
      className={clsx(
        'prose prose-sm max-w-none',
        // Token-canonical headings + body (no hardcoded greys).
        'prose-headings:font-display prose-headings:text-foreground',
        'prose-p:text-foreground prose-li:text-foreground',
        'prose-strong:text-foreground prose-em:text-foreground',
        'prose-code:text-foreground prose-code:bg-muted prose-code:px-1 prose-code:rounded',
        'prose-a:text-primary prose-a:no-underline hover:prose-a:underline',
        'prose-table:border prose-table:border-border',
        'prose-th:border prose-th:border-border prose-th:bg-muted prose-th:px-2 prose-th:py-1',
        'prose-td:border prose-td:border-border prose-td:px-2 prose-td:py-1',
        className
      )}
    >
      <ReactMarkdown remarkPlugins={[remarkGfm]}>{content}</ReactMarkdown>
    </div>
  );
}
