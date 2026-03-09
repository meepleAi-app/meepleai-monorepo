/**
 * RuleSourceCard Tests
 * Issue #5526: Unit tests for RuleSourceCard component.
 *
 * Tests:
 * 1. Collapsed by default, shows citation count and gameTitle
 * 2. Click header expands content
 * 3. Single citation: no chip row, quote shown directly
 * 4. Multi citation: chip row with N chips
 * 5. Chip click selects and displays corresponding quote
 * 6. Arrow keys navigate chips
 * 7. Quote displays snippet of selected citation
 * 8. "Vedi nel PDF" button opens PdfPageModal
 * 9. Publisher link has correct href and target="_blank"
 * 10. No publisherUrl: publisher link not rendered
 * 11. Casual mode: no relevance % on chips
 * 12. Power mode: relevance % visible on chips
 * 13. Power mode: color-coded chip borders
 * 14. Dark mode classes applied
 * 15. Accessibility: aria-expanded, role="tablist", role="tab"
 * 16. Empty citations: renders nothing
 * 17. Citation with empty snippet: graceful fallback
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

// Mock PdfPageModal
vi.mock('../PdfPageModal', () => ({
  PdfPageModal: ({
    open,
    citation,
    onClose,
  }: {
    open: boolean;
    citation: { pageNumber: number };
    onClose: () => void;
  }) =>
    open ? (
      <div data-testid="pdf-modal" data-page={citation.pageNumber}>
        <button onClick={onClose}>Close</button>
      </div>
    ) : null,
}));

// Mock useAppMode hook
vi.mock('@/hooks/use-app-mode', () => ({
  useAppMode: () => 'casual',
}));

import { RuleSourceCard } from '../RuleSourceCard';
import type { Citation } from '@/types';

// ─── Helpers ──────────────────────────────────────────────────────────────────

function makeCitation(overrides: Partial<Citation> = {}): Citation {
  return {
    documentId: 'doc-001',
    pageNumber: 12,
    snippet: 'Quando un giocatore costruisce un insediamento, riceve la risorsa corrispondente.',
    relevanceScore: 0.92,
    ...overrides,
  };
}

const multiCitations: Citation[] = [
  makeCitation({ pageNumber: 12, relevanceScore: 0.92 }),
  makeCitation({ documentId: 'doc-002', pageNumber: 23, snippet: 'Seconda citazione dal regolamento.', relevanceScore: 0.67 }),
  makeCitation({ documentId: 'doc-003', pageNumber: 45, snippet: 'Terza citazione dal regolamento.', relevanceScore: 0.41 }),
];

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('RuleSourceCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // --- Rendering ---

  it('renders collapsed by default with citation count and gameTitle', () => {
    render(<RuleSourceCard citations={multiCitations} gameTitle="Catan" />);
    expect(screen.getByText('3 fonti dal regolamento di Catan')).toBeInTheDocument();
    expect(screen.queryByTestId('citation-quote')).not.toBeInTheDocument();
  });

  it('shows singular label for 1 citation', () => {
    render(<RuleSourceCard citations={[makeCitation()]} gameTitle="Catan" />);
    expect(screen.getByText('1 fonte dal regolamento di Catan')).toBeInTheDocument();
  });

  it('renders header without gameTitle', () => {
    render(<RuleSourceCard citations={multiCitations} />);
    expect(screen.getByText('3 fonti dal regolamento')).toBeInTheDocument();
  });

  it('renders nothing when citations is empty', () => {
    const { container } = render(<RuleSourceCard citations={[]} />);
    expect(container.firstChild).toBeNull();
  });

  // --- Expand / Collapse ---

  it('expands on header click and shows quote', () => {
    render(<RuleSourceCard citations={multiCitations} gameTitle="Catan" />);
    fireEvent.click(screen.getByTestId('rule-source-header'));
    expect(screen.getByTestId('citation-quote')).toBeInTheDocument();
  });

  it('collapses on second header click', () => {
    render(<RuleSourceCard citations={multiCitations} gameTitle="Catan" />);
    const header = screen.getByTestId('rule-source-header');
    fireEvent.click(header); // open
    expect(screen.getByTestId('citation-quote')).toBeInTheDocument();
    fireEvent.click(header); // close
    expect(screen.queryByTestId('citation-quote')).not.toBeInTheDocument();
  });

  // --- Single citation ---

  it('single citation: no chip row, shows quote directly', () => {
    render(<RuleSourceCard citations={[makeCitation()]} />);
    fireEvent.click(screen.getByTestId('rule-source-header'));
    expect(screen.queryByTestId('citation-chips')).not.toBeInTheDocument();
    expect(screen.getByTestId('citation-quote')).toBeInTheDocument();
  });

  // --- Multi citation chips ---

  it('multi citation: renders chip row with correct count', () => {
    render(<RuleSourceCard citations={multiCitations} />);
    fireEvent.click(screen.getByTestId('rule-source-header'));
    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(3);
  });

  it('chip click selects and shows corresponding quote', () => {
    render(<RuleSourceCard citations={multiCitations} />);
    fireEvent.click(screen.getByTestId('rule-source-header'));

    // Click second chip
    const tabs = screen.getAllByRole('tab');
    fireEvent.click(tabs[1]);

    expect(screen.getByText(/Seconda citazione dal regolamento/)).toBeInTheDocument();
  });

  it('arrow keys navigate chips', () => {
    render(<RuleSourceCard citations={multiCitations} />);
    fireEvent.click(screen.getByTestId('rule-source-header'));

    const chipList = screen.getByRole('tablist');
    fireEvent.keyDown(chipList, { key: 'ArrowRight' });

    // After ArrowRight from index 0, index 1 is active
    expect(screen.getByText(/Seconda citazione dal regolamento/)).toBeInTheDocument();
  });

  // --- Quote display ---

  it('displays snippet of selected citation', () => {
    render(<RuleSourceCard citations={[makeCitation()]} />);
    fireEvent.click(screen.getByTestId('rule-source-header'));
    expect(screen.getByText(/Quando un giocatore costruisce/)).toBeInTheDocument();
    expect(screen.getByText(/Regolamento, p\.12/)).toBeInTheDocument();
  });

  it('handles citation with empty snippet gracefully', () => {
    render(<RuleSourceCard citations={[makeCitation({ snippet: '' })]} />);
    fireEvent.click(screen.getByTestId('rule-source-header'));
    expect(screen.getByText('Pagina 12')).toBeInTheDocument();
  });

  // --- Actions ---

  it('"Vedi nel PDF" opens PdfPageModal', () => {
    render(<RuleSourceCard citations={[makeCitation({ pageNumber: 7 })]} />);
    fireEvent.click(screen.getByTestId('rule-source-header'));

    expect(screen.queryByTestId('pdf-modal')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('view-pdf-btn'));
    expect(screen.getByTestId('pdf-modal')).toBeInTheDocument();
    expect(screen.getByTestId('pdf-modal')).toHaveAttribute('data-page', '7');
  });

  it('publisher link has correct href and target="_blank"', () => {
    render(
      <RuleSourceCard
        citations={[makeCitation()]}
        publisherUrl="https://www.catan.com/rules"
      />
    );
    fireEvent.click(screen.getByTestId('rule-source-header'));
    const link = screen.getByTestId('publisher-link');
    expect(link).toHaveAttribute('href', 'https://www.catan.com/rules');
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it('publisher link not rendered when publisherUrl is undefined', () => {
    render(<RuleSourceCard citations={[makeCitation()]} />);
    fireEvent.click(screen.getByTestId('rule-source-header'));
    expect(screen.queryByTestId('publisher-link')).not.toBeInTheDocument();
  });

  // --- Mode: casual vs power ---

  it('casual mode: no relevance percentage on chips', () => {
    render(<RuleSourceCard citations={multiCitations} mode="casual" />);
    fireEvent.click(screen.getByTestId('rule-source-header'));
    expect(screen.queryByText('92%')).not.toBeInTheDocument();
    expect(screen.queryByText('67%')).not.toBeInTheDocument();
  });

  it('power mode: relevance percentage visible on chips', () => {
    render(<RuleSourceCard citations={multiCitations} mode="power" />);
    fireEvent.click(screen.getByTestId('rule-source-header'));
    expect(screen.getByText('92%')).toBeInTheDocument();
    expect(screen.getByText('67%')).toBeInTheDocument();
    expect(screen.getByText('41%')).toBeInTheDocument();
  });

  // --- Accessibility ---

  it('header has aria-expanded attribute', () => {
    render(<RuleSourceCard citations={multiCitations} />);
    const header = screen.getByTestId('rule-source-header');
    expect(header).toHaveAttribute('aria-expanded', 'false');
    fireEvent.click(header);
    expect(header).toHaveAttribute('aria-expanded', 'true');
  });

  it('chip row has role="tablist" and chips have role="tab"', () => {
    render(<RuleSourceCard citations={multiCitations} />);
    fireEvent.click(screen.getByTestId('rule-source-header'));
    expect(screen.getByRole('tablist')).toBeInTheDocument();
    const tabs = screen.getAllByRole('tab');
    expect(tabs.length).toBeGreaterThan(1);
  });

  it('quote block has role="blockquote"', () => {
    render(<RuleSourceCard citations={[makeCitation()]} />);
    fireEvent.click(screen.getByTestId('rule-source-header'));
    expect(screen.getByRole('blockquote')).toBeInTheDocument();
  });
});
