// apps/web/src/components/admin/knowledge-base/explorer/preview/__tests__/KbDocPreviewPanel.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockPdfInlineViewer = vi.fn();
vi.mock('@/components/pdf/PdfInlineViewer', () => ({
  PdfInlineViewer: (props: Record<string, unknown>) => {
    mockPdfInlineViewer(props);
    return <div data-testid="pdf-inline-viewer-mock" />;
  },
}));

import { KbDocPreviewPanel } from '../KbDocPreviewPanel';

describe('KbDocPreviewPanel', () => {
  beforeEach(() => {
    mockPdfInlineViewer.mockReset();
  });

  it('renders PdfInlineViewer with documentId from docId prop', () => {
    render(<KbDocPreviewPanel docId="doc-abc" />);
    expect(screen.getByTestId('pdf-inline-viewer-mock')).toBeInTheDocument();
    expect(mockPdfInlineViewer).toHaveBeenCalledWith(
      expect.objectContaining({ documentId: 'doc-abc' })
    );
  });

  it('passes admin features { download, openInTab, jumpToPage, zoom } = true, antiLeak omitted', () => {
    render(<KbDocPreviewPanel docId="doc-abc" />);
    const props = mockPdfInlineViewer.mock.calls[0][0] as { features: Record<string, boolean> };
    expect(props.features.download).toBe(true);
    expect(props.features.openInTab).toBe(true);
    expect(props.features.jumpToPage).toBe(true);
    expect(props.features.zoom).toBe(true);
    expect(props.features.antiLeak ?? false).toBe(false);
  });

  it('renders nothing when docId is empty string', () => {
    const { container } = render(<KbDocPreviewPanel docId="" />);
    expect(container.firstChild).toBeNull();
  });
});
