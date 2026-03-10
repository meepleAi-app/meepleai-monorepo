/**
 * LegalMarkdown - Lightweight markdown renderer for legal content
 *
 * Renders a subset of markdown commonly used in legal documents:
 * - **bold** text
 * - - unordered lists
 * - | table | rows |
 * - [links](url)
 * - Paragraph breaks (\n\n)
 *
 * No external dependencies — purpose-built for legal page i18n content.
 */

import { Fragment, type ReactNode } from 'react';

import { cn } from '@/lib/utils';

interface LegalMarkdownProps {
  content: string;
  className?: string;
}

/**
 * Validate that a URL uses a safe protocol (no javascript:, data:, vbscript:, etc.)
 */
function isSafeHref(url: string): boolean {
  return (
    url.startsWith('/') ||
    url.startsWith('https://') ||
    url.startsWith('http://') ||
    url.startsWith('mailto:') ||
    url.startsWith('#')
  );
}

/**
 * Parse inline markdown: **bold**, [link](url)
 */
function parseInline(text: string): ReactNode[] {
  const nodes: ReactNode[] = [];
  // Match **bold** and [text](url) patterns
  const regex = /\*\*(.+?)\*\*|\[(.+?)\]\((.+?)\)/g;
  let lastIndex = 0;
  let match: RegExpExecArray | null;

  while ((match = regex.exec(text)) !== null) {
    // Text before match
    if (match.index > lastIndex) {
      nodes.push(text.slice(lastIndex, match.index));
    }

    if (match[1]) {
      // **bold**
      nodes.push(
        <strong key={match.index} className="font-semibold">
          {match[1]}
        </strong>
      );
    } else if (match[2] && match[3]) {
      // [text](url) — only render as link if protocol is safe
      if (isSafeHref(match[3])) {
        nodes.push(
          <a
            key={match.index}
            href={match[3]}
            className="text-blue-600 dark:text-blue-400 underline hover:text-blue-800 dark:hover:text-blue-300"
            target={match[3].startsWith('http') ? '_blank' : undefined}
            rel={match[3].startsWith('http') ? 'noopener noreferrer' : undefined}
          >
            {match[2]}
          </a>
        );
      } else {
        // Unsafe protocol — render as plain text
        nodes.push(<span key={match.index}>{match[2]}</span>);
      }
    }

    lastIndex = match.index + match[0].length;
  }

  // Remaining text
  if (lastIndex < text.length) {
    nodes.push(text.slice(lastIndex));
  }

  return nodes.length > 0 ? nodes : [text];
}

/**
 * Parse a table block (lines starting with |)
 */
function parseTable(lines: string[]): ReactNode {
  const rows = lines.map(line =>
    line
      .split('|')
      .filter(cell => cell.trim() !== '')
      .map(cell => cell.trim())
  );

  if (rows.length === 0) return null;

  // Detect separator row (---|---|---)
  const separatorIndex = rows.findIndex(row => row.every(cell => /^[-:]+$/.test(cell)));

  const headerRows = separatorIndex > 0 ? rows.slice(0, separatorIndex) : [];
  const bodyRows = separatorIndex >= 0 ? rows.slice(separatorIndex + 1) : rows;

  return (
    <div className="overflow-x-auto my-3">
      <table className="min-w-full text-sm border-collapse border border-slate-200 dark:border-slate-700">
        {headerRows.length > 0 && (
          <thead>
            {headerRows.map((row, ri) => (
              <tr key={ri} className="bg-slate-50 dark:bg-slate-800/50">
                {row.map((cell, ci) => (
                  <th
                    key={ci}
                    className="px-3 py-2 text-left font-semibold border border-slate-200 dark:border-slate-700"
                  >
                    {parseInline(cell)}
                  </th>
                ))}
              </tr>
            ))}
          </thead>
        )}
        <tbody>
          {bodyRows.map((row, ri) => (
            <tr key={ri} className="even:bg-slate-50/50 dark:even:bg-slate-800/25">
              {row.map((cell, ci) => (
                <td key={ci} className="px-3 py-2 border border-slate-200 dark:border-slate-700">
                  {parseInline(cell)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/**
 * Parse content into blocks: paragraphs, lists, tables
 */
function parseBlocks(content: string): ReactNode[] {
  const blocks: ReactNode[] = [];
  const lines = content.split('\n');
  let i = 0;

  while (i < lines.length) {
    const line = lines[i];

    // Skip empty lines
    if (line.trim() === '') {
      i++;
      continue;
    }

    // Table block (lines starting with |)
    if (line.trim().startsWith('|')) {
      const tableLines: string[] = [];
      while (i < lines.length && lines[i].trim().startsWith('|')) {
        tableLines.push(lines[i].trim());
        i++;
      }
      blocks.push(<Fragment key={`table-${i}`}>{parseTable(tableLines)}</Fragment>);
      continue;
    }

    // Unordered list (lines starting with - )
    if (line.trim().startsWith('- ')) {
      const items: string[] = [];
      while (i < lines.length && lines[i].trim().startsWith('- ')) {
        items.push(lines[i].trim().slice(2));
        i++;
      }
      blocks.push(
        <ul key={`list-${i}`} className="list-disc pl-6 my-2 space-y-1">
          {items.map((item, idx) => (
            <li key={idx}>{parseInline(item)}</li>
          ))}
        </ul>
      );
      continue;
    }

    // Ordered list (lines starting with number.)
    if (/^\d+\.\s/.test(line.trim())) {
      const items: string[] = [];
      while (i < lines.length && /^\d+\.\s/.test(lines[i].trim())) {
        items.push(lines[i].trim().replace(/^\d+\.\s/, ''));
        i++;
      }
      blocks.push(
        <ol key={`olist-${i}`} className="list-decimal pl-6 my-2 space-y-1">
          {items.map((item, idx) => (
            <li key={idx}>{parseInline(item)}</li>
          ))}
        </ol>
      );
      continue;
    }

    // Sub-headings: ### → <h4>, #### → <h5>
    // Heading hierarchy: <h1> = page title, <h2> = accordion section header,
    // <h3> = unused (accordion trigger styling), <h4>/<h5> = content sub-headings
    if (line.trim().startsWith('### ')) {
      blocks.push(
        <h4 key={`h4-${i}`} className="font-semibold text-foreground mt-4 mb-2">
          {parseInline(line.trim().slice(4))}
        </h4>
      );
      i++;
      continue;
    }

    if (line.trim().startsWith('#### ')) {
      blocks.push(
        <h5 key={`h5-${i}`} className="font-medium text-foreground mt-3 mb-1">
          {parseInline(line.trim().slice(5))}
        </h5>
      );
      i++;
      continue;
    }

    // Regular paragraph — collect consecutive non-special lines
    const paragraphLines: string[] = [];
    while (
      i < lines.length &&
      lines[i].trim() !== '' &&
      !lines[i].trim().startsWith('|') &&
      !lines[i].trim().startsWith('- ') &&
      !lines[i].trim().startsWith('### ') &&
      !lines[i].trim().startsWith('#### ') &&
      !/^\d+\.\s/.test(lines[i].trim())
    ) {
      paragraphLines.push(lines[i].trim());
      i++;
    }

    if (paragraphLines.length > 0) {
      blocks.push(
        <p key={`p-${i}`} className="my-2">
          {parseInline(paragraphLines.join(' '))}
        </p>
      );
    }
  }

  return blocks;
}

export function LegalMarkdown({ content, className }: LegalMarkdownProps) {
  if (!content) return null;

  return (
    <div
      className={cn('text-slate-600 dark:text-slate-300 leading-relaxed legal-content', className)}
    >
      {parseBlocks(content)}
    </div>
  );
}
