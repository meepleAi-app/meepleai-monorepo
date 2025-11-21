import { useMemo } from 'react';
import Prism from 'prismjs';
import 'prismjs/components/prism-json';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';

export interface PrismHighlighterProps {
  code: string;
  language: 'json';
  lineType: 'added' | 'deleted' | 'modified' | 'unchanged';
  className?: string;
}

/**
 * Syntax highlighter using Prism.js for JSON code
 * Applies diff-specific styling based on lineType
 */
export function PrismHighlighter({
  code,
  language,
  lineType,
  className = ''
}: PrismHighlighterProps) {
  const highlightedHtml = useMemo(() => {
    try {
      return Prism.highlight(code, Prism.languages[language] || Prism.languages.json, language);
    } catch (error) {
      logger.error(
        'Prism highlighting error',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('PrismHighlighter', 'highlight', { language, codeLength: code.length })
      );
      return code; // Fallback to plain text
    }
  }, [code, language]);

  return (
    <code
      className={`language-${language} diff-line--${lineType} ${className}`}
      dangerouslySetInnerHTML={{ __html: highlightedHtml }}
    />
  );
}
