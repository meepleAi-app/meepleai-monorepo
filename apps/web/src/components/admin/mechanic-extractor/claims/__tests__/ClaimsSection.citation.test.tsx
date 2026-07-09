/** @vitest-environment jsdom */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const mockGetClaims = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({ getMechanicAnalysisClaims: mockGetClaims }),
}));
const MockHttpClient = vi.hoisted(() => class MockHttpClient {});
vi.mock('@/lib/api/core/httpClient', () => ({ HttpClient: MockHttpClient }));
const mockHighlighter = vi.hoisted(() => vi.fn());
vi.mock('@/components/pdf/PdfQuoteHighlighter', () => ({
  PdfQuoteHighlighter: (props: Record<string, unknown>) => {
    mockHighlighter(props);
    return props.open ? <div data-testid="highlighter-open" /> : null;
  },
}));

import { ClaimsSection } from '../ClaimsSection';

const claim = {
  id: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd',
  analysisId: 'a',
  section: 1,
  text: 't',
  displayOrder: 0,
  status: 0,
  reviewedBy: null,
  reviewedAt: null,
  rejectionNote: null,
  reviewNote: null,
  validations: [],
  citations: [{ id: 'c1', pdfPage: 4, quote: 'score one point', displayOrder: 0 }],
};

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('ClaimsSection citation viewer', () => {
  it('opens PdfQuoteHighlighter with documentId/page/quote on citation click', async () => {
    mockGetClaims.mockResolvedValue([claim]);
    render(<ClaimsSection analysisId="a" pdfDocumentId="pdf-99" />, { wrapper: Wrapper });
    // expand citations then click
    fireEvent.click(await screen.findByTestId(`claim-citations-toggle-${claim.id}`));
    fireEvent.click(screen.getByTestId('claim-citation-open-c1'));
    expect(mockHighlighter).toHaveBeenCalledWith(
      expect.objectContaining({
        documentId: 'pdf-99',
        page: 4,
        quote: 'score one point',
        open: true,
      })
    );
  });

  it('renders citations as plain text (no button) when pdfDocumentId is absent', async () => {
    mockGetClaims.mockResolvedValue([claim]);
    render(<ClaimsSection analysisId="a" />, { wrapper: Wrapper });
    fireEvent.click(await screen.findByTestId(`claim-citations-toggle-${claim.id}`));
    expect(screen.queryByTestId('claim-citation-open-c1')).not.toBeInTheDocument();
    expect(screen.getByText(/score one point/)).toBeInTheDocument();
  });
});
