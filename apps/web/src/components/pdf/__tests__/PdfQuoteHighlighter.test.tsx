/** @vitest-environment jsdom */
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('../PdfInlineViewer', () => ({
  PdfInlineViewer: ({ onQuoteMatch }: { onQuoteMatch?: (f: boolean) => void }) => {
    onQuoteMatch?.(false); // simulate "not found" → banner should show
    return <div data-testid="pdf-inline-viewer" />;
  },
}));

import { PdfQuoteHighlighter } from '../PdfQuoteHighlighter';

describe('PdfQuoteHighlighter', () => {
  it('shows the fallback banner when the quote is not matched', () => {
    render(<PdfQuoteHighlighter open onOpenChange={() => {}} documentId="d1" page={4} quote="x" />);
    expect(screen.getByTestId('pdf-inline-viewer')).toBeInTheDocument();
    expect(screen.getByTestId('pdf-quote-fallback')).toHaveTextContent(/verifica manualmente/i);
  });
});
