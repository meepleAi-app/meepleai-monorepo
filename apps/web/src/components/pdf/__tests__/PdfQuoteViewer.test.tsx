/**
 * PdfQuoteViewer — unit test del wiring verso PdfInlineViewer.
 *
 * SP0 (#3404, epic #3403): il viewer deve accettare un `features` opzionale così
 * da poter essere montato con antiLeak nel percorso chat (CitationPdfTab), pur
 * mantenendo il default storico {jumpToPage, zoom} per mechanic-card/admin.
 */
import { render } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const inlineProps: any[] = [];

vi.mock('../PdfInlineViewer', () => ({
  PdfInlineViewer: (props: any) => {
    inlineProps.push(props);
    return <div data-testid="inline-viewer" />;
  },
}));

import { PdfQuoteViewer } from '../PdfQuoteViewer';

describe('PdfQuoteViewer features', () => {
  beforeEach(() => {
    inlineProps.length = 0;
  });

  it('defaults features to {jumpToPage, zoom} for backward compat', () => {
    render(<PdfQuoteViewer documentId="d1" page={3} quote="regola" />);
    expect(inlineProps[0].features).toEqual({ jumpToPage: true, zoom: true });
    expect(inlineProps[0].highlightQuote).toBe('regola');
  });

  it('forwards a custom features prop (e.g. antiLeak) to PdfInlineViewer', () => {
    render(
      <PdfQuoteViewer documentId="d1" page={3} quote="regola" features={{ antiLeak: true }} />
    );
    expect(inlineProps[0].features).toEqual({ antiLeak: true });
    expect(inlineProps[0].highlightQuote).toBe('regola');
  });
});
